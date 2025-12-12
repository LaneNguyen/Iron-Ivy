using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using IronIvy.Core;
using IronIvy.Data;
using IronIvy.UI;
using IronIvy.Systems.Camera;
using IronIvy.Gameplay;

namespace IronIvy.Gameplay.Rhythm
{
    // minigame click plant
    // - chơi rhythm theo pattern của plant
    // - mỗi plot tương ứng 1 cây, chơi lần lượt
    // - cuối cùng tổng hợp reward food rồi trả ra
    public class ClickPlantRhythmMinigame : MonoBehaviour
    {
        [Header("Config")]
        public List<PlantDefinition> debugAvailablePlants;
        // list seed test cho chế độ debug

        [Header("Game References")]
        public RhythmHUD hud;
        public RectTransform spawnArea;
        public RhythmClickTarget targetPrefab;
        public PlantRhythmRewardPanel rewardPanel;
        public GameObject disappearVfxPrefab;

        [Header("Settings")]
        public float bpm = 90f;
        public float delayBetweenPlots = 1.0f;
        public float showResultDuration = 3f;

        [Header("Hold Note Settings")]
        public float defaultHoldRequiredSeconds = 0.7f;
        [Range(0.1f, 1.0f)]
        public float holdTimePercentage = 0.8f;

        // --- Runtime State ---
        public bool IsRunning { get; private set; }
        private List<PlantPlot> _seqPlots;
        private List<PlantDefinition> _seqPlants;
        private Dictionary<FoodItem, int> _totalAccumulatedRewards = new Dictionary<FoodItem, int>();

        // Global Stats
        private int _seqTotalHits;
        private int _seqTotalMisses;

        // Rhythm State (Local)
        private PlantDefinition _currentPlant;
        private bool _isRhythmPlaying;
        private bool _isStagePlaying;

        private List<RhythmPattern> _playlist = new List<RhythmPattern>();
        private int _beatsHit, _beatsMiss;
        private float _trust;
        private RhythmClickTarget _currentTarget;
        private Coroutine _restCoroutine;
        private int _totalScorableBeats;
        private float _currentBeatDuration;
        private int _playlistIndex;
        private RhythmPattern _currentPattern;
        private int _currentStepIndex;
        private int _beatsLeftInStep;
        private bool _currentBeatIsScorable;

        [ContextMenu("Test Start Game (Auto Find Plots)")]
        public void StartGame()
        {
            // debug cho nhanh
            // - tự tìm tất cả PlantPlot trong scene
            // - seed lấy từ ArchiveManager nếu có
            Debug.LogWarning("[ClickPlantRhythmMinigame] StartGame() called directly (Debug Mode).");

            var foundPlots = new List<PlantPlot>(FindObjectsOfType<PlantPlot>());
            foundPlots.Sort((a, b) => string.Compare(a.name, b.name));

            if (foundPlots.Count == 0)
            {
                Debug.LogError("No PlantPlot found for Debug Game!");
                return;
            }

            // --- Lấy seed flow từ ArchiveManager ---
            List<PlantDefinition> sourcePlants = null;

            // 1. Ưu tiên lấy từ ArchiveManager (startingPlants + seed đã unlock)
            if (ArchiveManager.HasInstance)
            {
                sourcePlants = ArchiveManager.Instance.GetAvailablePlants();
                if (sourcePlants == null || sourcePlants.Count == 0)
                {
                    Debug.LogWarning("[ClickPlantRhythm] ArchiveManager has no available plants, sẽ fallback qua debugAvailablePlants.");
                    sourcePlants = null;
                }
            }

            // 2. Nếu ArchiveManager chưa có / không có seed nào -> dùng debugAvailablePlants
            if ((sourcePlants == null || sourcePlants.Count == 0)
                && debugAvailablePlants != null
                && debugAvailablePlants.Count > 0)
            {
                sourcePlants = debugAvailablePlants;
            }

            // 3. Nếu vẫn không có seed nào -> bó tay
            if (sourcePlants == null || sourcePlants.Count == 0)
            {
                Debug.LogError("[ClickPlantRhythm] No plants available from ArchiveManager or debugAvailablePlants. Cannot start debug game.");
                return;
            }

            var foundPlants = new List<PlantDefinition>();

            // gán seed cho từng plot
            // - simple: cứ lấy theo index, nếu plot > số seed thì dùng seed cuối
            for (int i = 0; i < foundPlots.Count; i++)
            {
                int idx = Mathf.Clamp(i, 0, sourcePlants.Count - 1);
                var plantDef = sourcePlants[idx];
                foundPlants.Add(plantDef);
            }

            StartSequence(foundPlots, foundPlants, null);
        }


        // giữ tham số PlantArea cho tương thích UI
        // - hiện tại chưa dùng đến, chỉ truyền qua cho vui
        public void StartSequence(List<PlantPlot> plots, List<PlantDefinition> plants, PlantArea area = null)
        {
            if (IsRunning) return;

            IsRunning = true;
            _seqPlots = plots;
            _seqPlants = plants;

            // data-driven camera
            // - camera đặt sẵn trong scene
            // - không tự tính toán offset ở đây nữa

            _totalAccumulatedRewards.Clear();
            _seqTotalHits = 0;
            _seqTotalMisses = 0;

            if (ListenManager.HasInstance)
                ListenManager.Instance.RaiseMinigameStarted();

            StartCoroutine(SequenceRoutine());
        }

        // loop chính cho cả sequence nhiều plot
        // - setup HUD
        // - chạy lần lượt từng plot
        // - nghỉ 1 chút giữa các plot
        private IEnumerator SequenceRoutine()
        {
            if (hud)
            {
                hud.hudRoot.SetActive(true);
                hud.SetMinigameTitle("Trồng cây thôi!");
                hud.SetStatus("Sẵn sàng...", true);
                hud.UpdateHitMiss(0, 0);
                hud.SetProgress(0f);
            }

            for (int i = 0; i < _seqPlots.Count; i++)
            {
                var plot = _seqPlots[i];
                var plant = _seqPlants[i];

                if (plot == null) continue;

                // báo CameraManager chỉ xoay nhìn vào plot này
                if (CameraManager.HasInstance)
                {
                    CameraManager.Instance.ApplyPlantMinigameProfile(plot.transform);
                }

                // để 1 frame cho camera update đã
                yield return null;

                if (plant == null)
                {
                    // nếu slot này chưa gán plant thì skip nhẹ
                    yield return new WaitForSeconds(0.5f);
                    continue;
                }

                if (hud)
                {
                    hud.SetStatus($"Ô đất {i + 1}/{_seqPlots.Count}", true);
                    hud.SetProgress(0f);
                }

                // chơi minigame cho 1 cây
                yield return StartCoroutine(PlayOnePlantRoutine(plot, plant));

                if (hud)
                    hud.SetStatus($"Ô đất {i + 1} đã xong!", true);

                if (i < _seqPlots.Count - 1)
                {
                    yield return new WaitForSeconds(delayBetweenPlots);
                }
            }

            FinishSequence();
        }

        // kết thúc sequence
        // - tắt BGM minigame
        // - trả camera về bình thường
        // - cộng reward vào inventory
        // - show reward panel
        private void FinishSequence()
        {
            IsRunning = false;
            AudioManager.Instance.FadeOutBGM();

            if (CameraManager.HasInstance)
                CameraManager.Instance.RestoreMinigameCamera();

            if (hud)
                hud.ResetHUD();

            foreach (var kvp in _totalAccumulatedRewards)
            {
                InventoryManager.Instance.AddFood(kvp.Key, kvp.Value);
            }

            if (rewardPanel)
            {
                int totalNotes = _seqTotalHits + _seqTotalMisses;
                float finalTrust = (totalNotes > 0) ? ((float)_seqTotalHits / totalNotes) * 100f : 0f;
                rewardPanel.Show(_totalAccumulatedRewards, _seqTotalHits, _seqTotalMisses, finalTrust);
            }

            StartCoroutine(CleanupPlotsRoutine());

            if (ListenManager.HasInstance)
                ListenManager.Instance.RaiseMinigameStopped();
        }

        // sau khi show kết quả 1 lúc thì dọn plot
        // - chơi hiệu ứng biến mất
        // - gọi Cleanup trên plot
        private IEnumerator CleanupPlotsRoutine()
        {
            yield return new WaitForSeconds(showResultDuration);

            if (_seqPlots != null)
            {
                foreach (var plot in _seqPlots)
                {
                    if (plot)
                    {
                        plot.PlayDisappearVFX(disappearVfxPrefab);
                        plot.Cleanup();
                    }
                }
            }
        }

        // CORE GAMEPLAY LOOP (SINGLE PLANT)
        // - setup lại state cho 1 cây
        // - chạy qua từng stage của plant
        // - build playlist từ patterns
        // - tính yield dựa trên trust
        private IEnumerator PlayOnePlantRoutine(PlantPlot plot, PlantDefinition plant)
        {
            _currentPlant = plant;
            _isRhythmPlaying = true;

            if (plant.musicLoop)
                AudioManager.Instance.PlayBGM(plant.musicLoop.name);

            if (_restCoroutine != null)
            {
                StopCoroutine(_restCoroutine);
                _restCoroutine = null;
            }

            if (_currentTarget)
            {
                Destroy(_currentTarget.gameObject);
                _currentTarget = null;
            }

            _beatsHit = 0;
            _beatsMiss = 0;
            _trust = 0f;
            _totalScorableBeats = 0;

            // tính tổng scorable beats (Tap/Hold) cho trust
            CalculateTotalScorableBeatsForAllStages(plant);

            // tính tổng beat của cả màn (bao gồm Rest)
            int totalBeatsForTimeline = CalculateTotalBeatsForTimeline(plant);

            if (hud)
            {
                hud.SetMinigameTitle(plant.displayName);
                hud.SetTrust01(0f);
                hud.SetProgress(0f);
                hud.UpdateHitMiss(_seqTotalHits, _seqTotalMisses);

                // bật mode timeline: thanh chạy từ 0 -> 1 dựa trên tổng số beat
                hud.useTimelineProgress = true;

                // thời gian 1 beat tính theo bpm
                float beatDuration = 60f / Mathf.Max(1f, bpm);

                // config timeline = total beats * beatDuration
                hud.ConfigureTimelineByBeats(totalBeatsForTimeline, beatDuration);
                hud.StartTimeline();
            }

            // cho plot hiện cây từ stage 0
            plot.InitializePlant(plant);
            yield return new WaitForSeconds(0.5f);

            if (plant.stages != null && plant.stages.Count > 0)
            {
                for (int i = 0; i < plant.stages.Count; i++)
                {
                    var stageData = plant.stages[i];

                    // stage sau sẽ animate chuyển stage
                    if (i > 0)
                    {
                        plot.TransitionToStage(i);
                        yield return new WaitForSeconds(0.6f);
                    }

                    BuildPlaylistForStage(stageData);

                    if (_playlist.Count == 0)
                    {
                        // nếu stage này không có pattern thì cho nghỉ 1 xíu
                        yield return new WaitForSeconds(1.0f);
                        continue;
                    }

                    _playlistIndex = 0;
                    SetupPattern(_playlist[0]);

                    _isStagePlaying = true;
                    StartNextBeat();

                    // loop cho tới khi stage xong
                    while (_isStagePlaying && _isRhythmPlaying)
                    {
                        yield return null;
                    }
                }
            }
            else
            {
                _isRhythmPlaying = false;
            }

            _isRhythmPlaying = false;

            // dừng timeline khi xong 1 plant
            if (hud)
            {
                hud.StopTimeline();
            }

            // tính lượng quả drop dựa theo trust
            int yieldCount = (_trust >= 90) ? 3 : (_trust >= 60) ? 2 : (_trust >= 30) ? 1 : 0;
            if (plant.yieldItem && yieldCount > 0)
            {
                if (_totalAccumulatedRewards.ContainsKey(plant.yieldItem))
                    _totalAccumulatedRewards[plant.yieldItem] += yieldCount;
                else
                    _totalAccumulatedRewards[plant.yieldItem] = yieldCount;
            }
        }

        // điều khiển nhịp tiếp theo
        // - handle chuyển pattern
        // - spawn target hoặc rest
        private void StartNextBeat()
        {
            if (!_isRhythmPlaying) return;

            if (_currentPattern == null)
            {
                _isStagePlaying = false;
                return;
            }

            // hết step hiện tại thì nhảy sang pattern tiếp theo
            if (_currentStepIndex >= _currentPattern.sequence.Length)
            {
                _playlistIndex++;

                if (_playlistIndex >= _playlist.Count)
                {
                    _isStagePlaying = false;
                    return;
                }

                SetupPattern(_playlist[_playlistIndex]);
            }

            var step = _currentPattern.sequence[_currentStepIndex];
            _currentBeatDuration = 60f / Mathf.Max(1, bpm);

            int totalBeatsOfStep = Mathf.Max(1, step.beats);
            bool isFirstBeatOfStep = (_beatsLeftInStep == totalBeatsOfStep);

            if (isFirstBeatOfStep)
            {
                // chỉ tính điểm với Tap / Hold
                _currentBeatIsScorable = (step.type == RhythmPattern.StepType.Tap || step.type == RhythmPattern.StepType.Hold);

                if (step.type == RhythmPattern.StepType.Rest)
                {
                    if (_currentTarget)
                        Destroy(_currentTarget.gameObject);

                    if (_restCoroutine != null)
                        StopCoroutine(_restCoroutine);

                    _restCoroutine = StartCoroutine(RestCoroutine(_currentBeatDuration));
                }
                else
                {
                    SpawnTarget(step);
                }
            }
            else
            {
                // các beat giữa của step sẽ chỉ đợi
                if (_restCoroutine != null)
                    StopCoroutine(_restCoroutine);

                _restCoroutine = StartCoroutine(RestCoroutine(_currentBeatDuration));
            }

            _beatsLeftInStep--;

            if (_beatsLeftInStep <= 0)
            {
                _currentStepIndex++;

                if (_currentStepIndex < _currentPattern.sequence.Length)
                    _beatsLeftInStep = Mathf.Max(1, _currentPattern.sequence[_currentStepIndex].beats);
            }
        }

        // spawn 1 target click/hold random trong vùng spawnArea
        private void SpawnTarget(RhythmPattern.Step step)
        {
            if (!targetPrefab || !spawnArea)
            {
                StartNextBeat();
                return;
            }

            if (_currentTarget)
                Destroy(_currentTarget.gameObject);

            var t = Instantiate(targetPrefab, spawnArea);
            RectTransform rt = t.GetComponent<RectTransform>();
            Vector2 size = spawnArea.rect.size * 0.5f;
            rt.anchoredPosition = new Vector2(Random.Range(-size.x, size.x), Random.Range(-size.y, size.y));

            bool isHold = step.type == RhythmPattern.StepType.Hold;
            float totalStepDuration = Mathf.Max(1, step.beats) * _currentBeatDuration;

            float requiredHoldTime = 0f;
            if (isHold)
            {
                float percentTime = totalStepDuration * holdTimePercentage;
                requiredHoldTime = Mathf.Min(defaultHoldRequiredSeconds, percentTime);

                // tránh case phải hold full 100% thời gian
                if (requiredHoldTime >= totalStepDuration)
                    requiredHoldTime = totalStepDuration * 0.9f;
            }

            _currentTarget = t;
            t.Setup(
                isHold,
                totalStepDuration,
                requiredHoldTime,
                isHold ? "HOLD" : "CLICK",
                hit => { ResolveBeat(hit); }
            );
        }

        // xử lý khi beat được hit hoặc miss
        private void ResolveBeat(bool hit)
        {
            if (!_isRhythmPlaying) return;

            if (_currentTarget)
                Destroy(_currentTarget.gameObject);

            if (_currentBeatIsScorable)
            {
                if (hit)
                {
                    _beatsHit++;
                    _seqTotalHits++;
                    _trust += (100f / _totalScorableBeats);
                }
                else
                {
                    _beatsMiss++;
                    _seqTotalMisses++;
                    _trust -= (50f / _totalScorableBeats);
                }

                _trust = Mathf.Clamp(_trust, 0, 100);
            }

            if (hud)
            {
                hud.SetTrust01(_trust / 100f);
                hud.UpdateHitMiss(_seqTotalHits, _seqTotalMisses);
                hud.SetStatus(hit ? "HOÀN HẢO!" : "[ERROR]SAI NHỊP", hit);

                // phần progress cũ theo scorable beats
                // giờ nếu useTimelineProgress = true thì SetProgress tự ignore
                if (_totalScorableBeats > 0)
                {
                    float progress01 = (_beatsHit + _beatsMiss) / (float)_totalScorableBeats;
                    hud.SetProgress(Mathf.Clamp01(progress01));
                }
            }

            StartNextBeat();
        }

        // tính tổng số beat có thể chấm điểm trong toàn bộ stages
        // - dùng để chia trust cho đều
        private void CalculateTotalScorableBeatsForAllStages(PlantDefinition plant)
        {
            _totalScorableBeats = 0;

            if (plant.stages != null)
            {
                foreach (var stage in plant.stages)
                {
                    if (stage.patterns == null) continue;

                    foreach (var p in stage.patterns)
                    {
                        if (p && p.sequence != null)
                        {
                            foreach (var s in p.sequence)
                            {
                                if (s.type != RhythmPattern.StepType.Rest)
                                    _totalScorableBeats += Mathf.Max(1, s.beats);
                            }
                        }
                    }
                }
            }

            if (_totalScorableBeats == 0)
                _totalScorableBeats = 1;
        }

        // tổng số beat của màn cho timeline (bao gồm cả Rest)
        private int CalculateTotalBeatsForTimeline(PlantDefinition plant)
        {
            int totalBeats = 0;

            if (plant.stages != null)
            {
                foreach (var stage in plant.stages)
                {
                    if (stage.patterns == null) continue;

                    foreach (var p in stage.patterns)
                    {
                        if (p && p.sequence != null)
                        {
                            foreach (var s in p.sequence)
                            {
                                // bước nào cũng chiếm beat trên timeline
                                totalBeats += Mathf.Max(1, s.beats);
                            }
                        }
                    }
                }
            }

            if (totalBeats == 0)
                totalBeats = 1;

            return totalBeats;
        }

        // build playlist từ stage
        // - copy patterns hợp lệ vào list dùng cho stage hiện tại
        private void BuildPlaylistForStage(PlantDefinition.PlantStageData stageData)
        {
            _playlist.Clear();

            if (stageData.patterns != null)
            {
                foreach (var pat in stageData.patterns)
                    if (pat) _playlist.Add(pat);
            }
        }

        // setup lại state khi chuyển sang pattern mới
        private void SetupPattern(RhythmPattern p)
        {
            _currentPattern = p;
            _currentStepIndex = 0;

            if (p.sequence != null && p.sequence.Length > 0)
                _beatsLeftInStep = Mathf.Max(1, p.sequence[0].beats);
        }

        // khoảng nghỉ giữa beat
        // - hết thời gian thì gọi StartNextBeat tiếp
        private IEnumerator RestCoroutine(float dur)
        {
            yield return new WaitForSeconds(dur);
            _restCoroutine = null;
            StartNextBeat();
        }
    }
}
