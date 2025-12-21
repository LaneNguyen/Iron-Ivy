using System.Collections;
using System.Collections.Generic;
using IronIvy.Core;
using IronIvy.Data;
using IronIvy.Systems.Camera;
using UnityEngine;

namespace IronIvy.Gameplay.Rhythm
{
    public class ClickPlantRhythmMinigame : MonoBehaviour
    {
        [Header("Debug")]
        public List<PlantDefinition> debugAvailablePlants;

        [Header("UI Spawn")]
        [SerializeField] private RectTransform spawnArea;
        [SerializeField] private RhythmClickTarget targetPrefab;

        [Header("Config")]
        [SerializeField] private float bpm = 75f;
        [SerializeField] private float delayBetweenPlots = 0.7f;
        [SerializeField] private float defaultHoldRequiredSeconds = 0.35f;

        [Header("SFX")]
        [SerializeField] private AudioClip hitSfx;
        [SerializeField] private float hitSfxVolume = 0.8f;
        [SerializeField] private AudioClip missSfx;
        [SerializeField] private float missSfxVolume = 0.8f;

        [Header("VFX")]
        [SerializeField] private GameObject disappearVfxPrefab;

        public bool IsRunning { get; private set; }

        private List<PlantPlot> _seqPlots;
        private List<PlantDefinition> _seqPlants;
        private PlantArea _seqArea;

        private RhythmClickTarget _currentTarget;

        private Dictionary<FoodItem, int> _totalRewards = new Dictionary<FoodItem, int>();
        private int _seqTotalHits;
        private int _seqTotalMisses;

        private int _totalBeatsTimeline;
        private int _totalBeatsScorable;
        private int _totalHitLocal;
        private int _totalMissLocal;

        private float _trust01;
        private int _scorableBeatIndex;
        private bool _waitResolved;
        private bool _waitHit;

        private Coroutine _sequenceCoroutine;
        private bool _isStopping;

        public void SetSpawnArea(RectTransform area) => spawnArea = area;

        public void StartSequence(List<PlantPlot> plots, List<PlantDefinition> plants, PlantArea area = null)
        {
            if (IsRunning) return;
            if (plots == null || plants == null || plots.Count == 0 || plants.Count == 0) return;

            _seqPlots = plots;
            _seqPlants = plants;
            _seqArea = area;

            _totalRewards.Clear();
            _seqTotalHits = 0;
            _seqTotalMisses = 0;

            IsRunning = true;
            _isStopping = false;

            _sequenceCoroutine = StartCoroutine(SequenceRoutine());
        }

        private IEnumerator SequenceRoutine()
        {
            // Reset HUD ban đầu
            if (ListenManager.HasInstance)
                ListenManager.Instance.RaiseRhythmHUDShow(new ListenManager.RhythmHUDShowPayload("Sẵn sàng...", true, 1, BeatDuration(), false));

            for (int i = 0; i < _seqPlots.Count; i++)
            {
                var plot = _seqPlots[i];
                var plant = (i < _seqPlants.Count) ? _seqPlants[i] : null;

                if (plot == null || plant == null) continue;

                // Cập nhật Camera
                if (CameraManager.HasInstance)
                    CameraManager.Instance.ApplyPlantMinigameProfile(plot.transform);

                // Khởi tạo visual cây ngay lập tức để tránh màn hình trống
                plot.InitializePlant(plant);

                yield return StartCoroutine(PlayOnePlantRoutine(plot, plant));

                if (i < _seqPlots.Count - 1)
                    yield return new WaitForSecondsRealtime(delayBetweenPlots);
            }

            FinishSequence();
        }

        private IEnumerator PlayOnePlantRoutine(PlantPlot plot, PlantDefinition plant)
        {
            // 1. Reset dữ liệu cho cây hiện tại
            _totalHitLocal = 0;
            _totalMissLocal = 0;
            _trust01 = 0f;
            _scorableBeatIndex = 0;

            // 2. Tính toán nhịp với hàm an toàn (không bao giờ trả về 0)
            _totalBeatsTimeline = CalculateTotalBeatsForTimeline(plant);
            _totalBeatsScorable = CalculateTotalBeatsScorable(plant);

            // 3. Cập nhật HUD ngay lập tức về 0%
            if (ListenManager.HasInstance)
            {
                ListenManager.Instance.RaiseRhythmHUDShow(new ListenManager.RhythmHUDShowPayload(plant.displayName, true,
                    _totalBeatsTimeline, BeatDuration(), false));
                ListenManager.Instance.RaiseRhythmHUDUpdate(new ListenManager.RhythmHUDUpdatePayload(
                    _seqTotalHits, _seqTotalMisses, _trust01, 0f, "", true, 0f));
            }
            // SỬA LỖI NHẠC: Kiểm tra kỹ hơn ScriptableObject
            if (plant.musicLoop != null && AudioManager.HasInstance)
            {
                Debug.Log($"[PlantMinigame] Playing music: {plant.musicLoop.name}");
                //AudioManager.Instance.PlayBGM(plant.musicLoop.name);
                //LOGIC mới lưu nhạc: 
                 AudioManager.Instance.PushBGM(plant.musicLoop.name);
            }

            // 4. Chạy qua stages/patterns/sequence
            if (plant.stages == null) yield break;

            for (int s = 0; s < plant.stages.Count; s++)
            {
                // CẬP NHẬT VISUAL STAGE: Đưa cây lên mặt đất theo từng giai đoạn
                if (plot != null) plot.TransitionToStage(s);

                var stage = plant.stages[s];
                if (stage == null || stage.patterns == null) continue;

                foreach (var pattern in stage.patterns)
                {
                    if (pattern == null || pattern.sequence == null) continue;

                    foreach (var step in pattern.sequence)
                    {
                        int beatCount = Mathf.Max(1, step.beats);
                        if (step.type == RhythmPattern.StepType.Rest)
                        {
                            // IMPORTANT: dùng realtime để không bị kẹt khi PauseWorld (timeScale = 0)
                            yield return new WaitForSecondsRealtime(beatCount * BeatDuration());
                            continue;
                        }

                        bool isHold = (step.type == RhythmPattern.StepType.Hold);
                        for (int b = 0; b < beatCount; b++)
                        {
                            SpawnTarget(isHold, BeatDuration());
                            yield return StartCoroutine(WaitPlayerInputRoutine(isHold));
                            KillTarget();
                        }
                    }
                }
            }

            // 5. Tính reward cho plant này
            if (plant.yieldItem != null)
            {
                int yieldCount = Mathf.RoundToInt(Mathf.Lerp(1f, 3f, Mathf.Clamp01(_trust01)));
                yieldCount = Mathf.Max(0, yieldCount);

                if (!_totalRewards.ContainsKey(plant.yieldItem))
                    _totalRewards[plant.yieldItem] = 0;

                _totalRewards[plant.yieldItem] += yieldCount;
            }
        }
        private void SpawnTarget(bool isHold, float beatDuration)
        {
            if (spawnArea == null || targetPrefab == null) return;

            _currentTarget = Instantiate(targetPrefab, spawnArea);

            // Random vị trí trong spawnArea (UI RectTransform)
            var rect = spawnArea.rect;
            float x = Random.Range(rect.xMin, rect.xMax);
            float y = Random.Range(rect.yMin, rect.yMax);
            _currentTarget.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, y);

            _waitResolved = false;
            _waitHit = false;

            _currentTarget.Setup(
                isHold,
                beatDuration,
                defaultHoldRequiredSeconds,
                isHold ? "GIỮ CHUỘT" : "CLICK CHUỘT",
                (hit) => { _waitHit = hit; _waitResolved = true; }
            );
        }


        private IEnumerator WaitPlayerInputRoutine(bool isHoldStep)
        {
            while (!_waitResolved) yield return null;

            // play sfx ngay lúc chốt hit/miss
            if (AudioManager.HasInstance)
            {
                if (_waitHit) AudioManager.Instance.PlaySEClip(hitSfx, hitSfxVolume);
                else AudioManager.Instance.PlaySEClip(missSfx, missSfxVolume);
            }

            if (_waitHit) { _totalHitLocal++; _seqTotalHits++; }
            else { _totalMissLocal++; _seqTotalMisses++; }

            float stepWeight = 1f / _totalBeatsScorable;
            _trust01 = Mathf.Clamp01(_trust01 + (_waitHit ? stepWeight : -stepWeight * 0.6f));
            _scorableBeatIndex++;

            if (ListenManager.HasInstance)
                ListenManager.Instance.RaiseRhythmHUDUpdate(new ListenManager.RhythmHUDUpdatePayload(
                    _seqTotalHits, _seqTotalMisses, _trust01, (float)_scorableBeatIndex / _totalBeatsScorable,
                    _waitHit ? "ĐÚNG NHỊP!" : "HỤT NHỊP", _waitHit, 0f));
        }

        private void KillTarget() { if (_currentTarget) Destroy(_currentTarget.gameObject); }
        private float BeatDuration() => 60f / Mathf.Max(1f, bpm);

        // Giống AnimalRhythm: có nút "StopGame" để ép kết thúc ngay lập tức,
        // tránh kẹt coroutine khi PauseWorld (timeScale = 0) hoặc khi cần thoát minigame gấp.
        public void StopGame()
        {
            if (!IsRunning) return;
            if (_isStopping) return;

            _isStopping = true;
            IsRunning = false;

            KillTarget();

            if (_sequenceCoroutine != null)
            {
                StopCoroutine(_sequenceCoroutine);
                _sequenceCoroutine = null;
            }

            // --- BỔ SUNG: Dừng nhạc minigame ---
            if (AudioManager.HasInstance)
                AudioManager.Instance.FadeOutBGM();
            // Khôi phục camera/HUD giống Animal
            if (CameraManager.HasInstance)
                CameraManager.Instance.RestoreMinigameCamera();

            if (ListenManager.HasInstance)
                ListenManager.Instance.RaiseRhythmHUDHide();

            // Tính kết quả + bắn result + cleanup
            FinishSequence();
        }

        private void FinishSequence()
        {
            // FinishSequence có thể được gọi từ coroutine bình thường hoặc từ StopGame().
            IsRunning = false;
            _sequenceCoroutine = null;

            // Nếu không đi qua StopGame(), vẫn cần khôi phục camera/HUD tại đây.
            if (!_isStopping)
            {
                // --- BỔ SUNG: Dừng nhạc minigame ---
                if (AudioManager.HasInstance)
                    AudioManager.Instance.FadeOutBGM();
                if (CameraManager.HasInstance)
                    CameraManager.Instance.RestoreMinigameCamera();
                if (AudioManager.HasInstance)
                {
                    AudioManager.Instance.PopBGM();
                }
                if (ListenManager.HasInstance)
                    ListenManager.Instance.RaiseRhythmHUDHide();
            }

            float totalNotes = _seqTotalHits + _seqTotalMisses;
            float finalTrust = (totalNotes > 0) ? (float)_seqTotalHits / totalNotes : 0f;

            if (ListenManager.HasInstance)
            {
                ListenManager.Instance.RaiseRhythmPlantResult(new ListenManager.RhythmPlantResultPayload(
                    new Dictionary<FoodItem, int>(_totalRewards), _seqTotalHits, _seqTotalMisses, finalTrust));
            }

            if (InventoryManager.HasInstance)
            {
                foreach (var kv in _totalRewards) InventoryManager.Instance.AddFood(kv.Key, kv.Value);
            }

            StartCoroutine(CleanupPlotsRoutine());
        }

        // --- HÀM TÍNH TOÁN AN TOÀN

        private int CalculateTotalBeatsForTimeline(PlantDefinition plant)
        {
            if (plant == null || plant.stages == null)
                return 1;

            int total = 0;

            foreach (var st in plant.stages)
            {
                if (st?.patterns == null) continue;

                foreach (var pat in st.patterns)
                {
                    if (pat == null) continue;
                    total += pat.GetTotalBeats(); // hàm này đã có sẵn trong codebase của em
                }
            }

            return Mathf.Max(1, total);
        }

        private int CalculateTotalBeatsScorable(PlantDefinition plant)
        {
            if (plant == null || plant.stages == null)
                return 1;

            int total = 0;

            foreach (var st in plant.stages)
            {
                if (st?.patterns == null) continue;

                foreach (var pat in st.patterns)
                {
                    if (pat?.sequence == null) continue;

                    foreach (var step in pat.sequence)
                    {
                        if (step.type == RhythmPattern.StepType.Rest) continue;
                        total += Mathf.Max(1, step.beats);
                    }
                }
            }

            return Mathf.Max(1, total);
        }



        private IEnumerator CleanupPlotsRoutine()
        {
            yield return new WaitForSecondsRealtime(0.25f);
            if (_seqPlots != null)
                foreach (var p in _seqPlots)
                {
                    p?.PlayDisappearVFX(disappearVfxPrefab);
                    p?.Cleanup();
                }
        }
    }
}
