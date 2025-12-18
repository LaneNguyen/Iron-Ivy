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

        [Header("Prefabs")]
        public RhythmClickTarget targetPrefab;
        public GameObject disappearVfxPrefab;

        [Header("SFX (Hit / Miss)")]
        public AudioClip hitSfx;
        public AudioClip missSfx;
        [Range(0f, 1f)] public float hitSfxVolume = 1f;
        [Range(0f, 1f)] public float missSfxVolume = 1f;

        [Header("Beat Settings")]
        public float bpm = 90f;
        public float delayBetweenPlots = 0.8f;

        [Header("Hold Settings")]
        public float defaultHoldRequiredSeconds = 0.7f;

        public bool IsRunning { get; private set; }

        private List<PlantPlot> _seqPlots;
        private List<PlantDefinition> _seqPlants;
        private Dictionary<FoodItem, int> _totalRewards = new Dictionary<FoodItem, int>();

        private int _seqTotalHits;
        private int _seqTotalMisses;
        private RhythmClickTarget _currentTarget;

        private int _totalBeatsTimeline;
        private int _totalBeatsScorable;
        private int _totalHitLocal;
        private int _totalMissLocal;

        private float _trust01;
        private int _scorableBeatIndex;
        private bool _waitResolved;
        private bool _waitHit;

        public void SetSpawnArea(RectTransform area) => spawnArea = area;

        public void StartSequence(List<PlantPlot> plots, List<PlantDefinition> plants, PlantArea area = null)
        {
            if (IsRunning) return;
            if (plots == null || plants == null || plots.Count == 0 || plants.Count == 0) return;

            IsRunning = true;
            _seqPlots = plots;
            _seqPlants = plants;
            _totalRewards.Clear();
            _seqTotalHits = 0;
            _seqTotalMisses = 0;

            StartCoroutine(SequenceRoutine());
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
                    yield return new WaitForSeconds(delayBetweenPlots);
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
                ListenManager.Instance.RaiseRhythmHUDShow(new ListenManager.RhythmHUDShowPayload(plant.displayName, true, _totalBeatsTimeline, BeatDuration(), true));
                ListenManager.Instance.RaiseRhythmHUDUpdate(new ListenManager.RhythmHUDUpdatePayload(_seqTotalHits, _seqTotalMisses, 0f, 0f, "Bắt đầu!", true, 0f));
            }

            if (plant.musicLoop && AudioManager.HasInstance)
                AudioManager.Instance.PlayBGM(plant.musicLoop.name);

            if (plant.stages == null || plant.stages.Count == 0) yield break;

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
                            yield return new WaitForSeconds(beatCount * BeatDuration());
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

            // Tính toán Reward dựa trên kết quả cuối cùng của cây này
            int yieldCount = (_trust01 >= 0.9f) ? 3 : (_trust01 >= 0.7f) ? 2 : (_trust01 >= 0.4f) ? 1 : 0;
            if (plant.yieldItem != null && yieldCount > 0)
            {
                if (!_totalRewards.ContainsKey(plant.yieldItem)) _totalRewards[plant.yieldItem] = 0;
                _totalRewards[plant.yieldItem] += yieldCount;
            }
        }

        private void FinishSequence()
        {
            IsRunning = false;

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

        // --- HÀM TÍNH TOÁN AN TOÀN (SỬA LỖI 100% VÀ CRASH) ---

        private int CalculateTotalBeatsForTimeline(PlantDefinition plant)
        {
            if (plant == null || plant.stages == null) return 1;
            int total = 0;
            foreach (var st in plant.stages)
            {
                if (st?.patterns == null) continue;
                foreach (var pat in st.patterns) if (pat != null) total += pat.GetTotalBeats();
            }
            return Mathf.Max(1, total);
        }

        private int CalculateTotalBeatsScorable(PlantDefinition plant)
        {
            if (plant == null || plant.stages == null) return 1;
            int total = 0;
            foreach (var st in plant.stages)
            {
                if (st?.patterns == null) continue;
                foreach (var pat in st.patterns)
                {
                    if (pat?.sequence == null) continue;
                    foreach (var s in pat.sequence)
                    {
                        if (s.type != RhythmPattern.StepType.Rest) total += Mathf.Max(1, s.beats);
                    }
                }
            }
            return Mathf.Max(1, total);
        }

        // --- HÀM PHỤ TRỢ ---

        private void SpawnTarget(bool isHold, float beatDuration)
        {
            if (targetPrefab == null || spawnArea == null) return;
            _waitResolved = false; _waitHit = false;
            _currentTarget = Instantiate(targetPrefab, spawnArea);
            Vector2 size = spawnArea.rect.size;
            _currentTarget.GetComponent<RectTransform>().anchoredPosition = new Vector2(
                Random.Range(-size.x / 2 + 40, size.x / 2 - 40),
                Random.Range(-size.y / 2 + 40, size.y / 2 - 40)
            );

            _currentTarget.Setup(isHold, beatDuration, defaultHoldRequiredSeconds, isHold ? "GIỮ CHUỘT" : "CLICK CHUỘT",
                (hit) => { _waitHit = hit; _waitResolved = true; });
        }

        private IEnumerator WaitPlayerInputRoutine(bool isHoldStep)
        {
            while (!_waitResolved) yield return null;

            // NEW: play sfx ngay lúc chốt hit/miss
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

        private IEnumerator CleanupPlotsRoutine()
        {
            yield return new WaitForSeconds(0.25f);
            if (_seqPlots != null)
                foreach (var p in _seqPlots)
                {
                    p?.PlayDisappearVFX(disappearVfxPrefab);
                    p?.Cleanup();
                }
        }
    }
}
