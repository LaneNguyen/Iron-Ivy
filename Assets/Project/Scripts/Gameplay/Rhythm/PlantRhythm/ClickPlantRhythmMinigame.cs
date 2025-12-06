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
    public class ClickPlantRhythmMinigame : MonoBehaviour
    {
        [Header("Config")]
        public List<PlantDefinition> debugAvailablePlants; 
        
        [Header("Game References")]
        public RhythmHUD hud;
        public RectTransform spawnArea;
        public RhythmClickTarget targetPrefab;
        public PlantRhythmRewardPanel rewardPanel;
        public GameObject disappearVfxPrefab;

        [Header("Settings")]
        public float bpm = 90f; 
        public float delayBetweenPlots = 2.0f;
        public float delayBetweenStages = 1.0f; 
        public float showResultDuration = 3f;

        [Header("Hold Note Settings")]
        public float defaultHoldRequiredSeconds = 0.7f;
        [Range(0.1f, 1.0f)]
        public float holdTimePercentage = 0.8f; 

        // === [FIX COMPATIBILITY] ===
        public void StartGame() { Debug.LogWarning("Legacy StartGame() called."); IsRunning = true; }

        // --- Runtime State ---
        public bool IsRunning { get; private set; }
        private List<PlantPlot> _seqPlots;
        private List<PlantDefinition> _seqPlants;
        private Dictionary<FoodItem, int> _totalAccumulatedRewards = new Dictionary<FoodItem, int>();

        // --- Global Stats (Tổng kết toàn bộ sequence) ---
        private int _seqTotalHits;
        private int _seqTotalMisses;

        // --- Rhythm State (Cục bộ từng cây) ---
        private PlantDefinition _currentPlant;
        private bool _isRhythmPlaying;
        private bool _isStagePlaying;
        
        private List<RhythmPattern> _playlist = new List<RhythmPattern>();
        private int _beatsHit, _beatsMiss; // Hit/Miss của cây hiện tại
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

        public void StartSequence(List<PlantPlot> plots, List<PlantDefinition> plants)
        {
            if (IsRunning) return;
            IsRunning = true;
            _seqPlots = plots;
            _seqPlants = plants;
            
            // Reset Global Data
            _totalAccumulatedRewards.Clear();
            _seqTotalHits = 0;
            _seqTotalMisses = 0;

            if (ListenManager.HasInstance) ListenManager.Instance.RaiseMinigameStarted();

            StartCoroutine(SequenceRoutine());
        }

        private IEnumerator SequenceRoutine()
        {
            // Setup HUD ban đầu
            if (hud) {
                hud.hudRoot.SetActive(true);
                hud.SetMinigameTitle("Farm Sequence");
                hud.SetStatus("Starting...", true);
                hud.UpdateHitMiss(0, 0); // Reset UI Text
            }

            for (int i = 0; i < _seqPlots.Count; i++)
            {
                var plot = _seqPlots[i];
                var plant = _seqPlants[i];

                if (plot == null || plant == null) continue;

                if (hud) 
                {
                    hud.SetStatus($"Moving to Plot {i + 1}/{_seqPlots.Count}...", true);
                    hud.UpdateProgress(0); 
                }

                if (CameraManager.HasInstance) CameraManager.Instance.ApplyPlantMinigameProfile(plot.transform);
                
                yield return StartCoroutine(PlayOnePlantRoutine(plot, plant));

                if (hud) hud.SetStatus($"Plot {i + 1} Complete!", true);
                yield return new WaitForSeconds(delayBetweenPlots);
            }

            FinishSequence();
        }

        private void FinishSequence()
        {
            IsRunning = false;
            AudioManager.Instance.FadeOutBGM();
            if (CameraManager.HasInstance) CameraManager.Instance.RestoreMinigameCamera();
            if (hud) hud.ResetHUD();

            // Cộng đồ vào Inventory
            foreach (var kvp in _totalAccumulatedRewards)
            {
                InventoryManager.Instance.AddFood(kvp.Key, kvp.Value); 
            }

            // Show Reward Panel
            if (rewardPanel) 
            {
                PlantDefinition reprPlant = null;
                foreach(var p in _seqPlants) if(p) { reprPlant = p; break; }
                
                int totalCount = 0;
                foreach(var v in _totalAccumulatedRewards.Values) totalCount += v;

                int totalNotes = _seqTotalHits + _seqTotalMisses;
                float finalTrust = (totalNotes > 0) ? ((float)_seqTotalHits / totalNotes) * 100f : 0f;

                rewardPanel.Show(_totalAccumulatedRewards, _seqTotalHits, _seqTotalMisses, finalTrust);
            }

            StartCoroutine(CleanupPlotsRoutine());
            if (ListenManager.HasInstance) ListenManager.Instance.RaiseMinigameStopped();
        }

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

        private IEnumerator PlayOnePlantRoutine(PlantPlot plot, PlantDefinition plant)
        {
            _currentPlant = plant;
            _isRhythmPlaying = true;
            
            if (plant.musicLoop) AudioManager.Instance.PlayBGM(plant.musicLoop.name);

            // [FIX SAFETY] Dọn dẹp coroutine cũ nếu còn sót lại
            if (_restCoroutine != null) { StopCoroutine(_restCoroutine); _restCoroutine = null; }
            if (_currentTarget) { Destroy(_currentTarget.gameObject); _currentTarget = null; }

            // Reset Local Data
            _beatsHit = 0; _beatsMiss = 0; _trust = 0f;
            _totalScorableBeats = 0;
            CalculateTotalScorableBeatsForAllStages(plant);
            
            // [FIX DISPLAY] Setup HUD nhưng hiển thị điểm TỔNG (Cumulative) thay vì 0
            if (hud) {
                hud.SetMinigameTitle(plant.displayName);
                hud.SetTrust01(0f);
                hud.SetProgress(0f);
                // Hiển thị điểm đã tích lũy từ các cây trước
                hud.UpdateHitMiss(_seqTotalHits, _seqTotalMisses); 
            }

            plot.InitializePlant(plant);
            yield return new WaitForSeconds(0.5f);

            if (plant.stages != null && plant.stages.Count > 0)
            {
                for (int i = 0; i < plant.stages.Count; i++)
                {
                    var stageData = plant.stages[i];

                    if (i > 0)
                    {
                        plot.TransitionToStage(i);
                        yield return new WaitForSeconds(0.6f); 
                    }

                    BuildPlaylistForStage(stageData);

                    if (_playlist.Count == 0)
                    {
                        yield return new WaitForSeconds(1.0f);
                        continue; 
                    }

                    _playlistIndex = 0;
                    SetupPattern(_playlist[0]);

                    _isStagePlaying = true; 
                    StartNextBeat();

                    while (_isStagePlaying && _isRhythmPlaying)
                    {
                        yield return null;
                    }
                }
            }
            else
            {
                Debug.LogWarning("No stages config!");
                _isRhythmPlaying = false;
            }

            _isRhythmPlaying = false; 

            int yieldCount = (_trust >= 90) ? 3 : (_trust >= 60) ? 2 : (_trust >= 30) ? 1 : 0;
            if (plant.yieldItem && yieldCount > 0)
            {
                if (_totalAccumulatedRewards.ContainsKey(plant.yieldItem))
                    _totalAccumulatedRewards[plant.yieldItem] += yieldCount;
                else
                    _totalAccumulatedRewards[plant.yieldItem] = yieldCount;
            }
        }

        private void StartNextBeat()
        {
            if (!_isRhythmPlaying) return;

            if (_currentPattern == null) { _isStagePlaying = false; return; }

            if (_currentStepIndex >= _currentPattern.sequence.Length) 
            {
                _playlistIndex++;
                if (_playlistIndex >= _playlist.Count) { 
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
                _currentBeatIsScorable = (step.type == RhythmPattern.StepType.Tap || step.type == RhythmPattern.StepType.Hold);
                
                if (step.type == RhythmPattern.StepType.Rest) {
                    if (_currentTarget) Destroy(_currentTarget.gameObject);
                    if (_restCoroutine != null) StopCoroutine(_restCoroutine);
                    _restCoroutine = StartCoroutine(RestCoroutine(_currentBeatDuration));
                } else {
                    SpawnTarget(step);
                }
            }
            else
            {
                if (_restCoroutine != null) StopCoroutine(_restCoroutine);
                _restCoroutine = StartCoroutine(RestCoroutine(_currentBeatDuration));
            }

            _beatsLeftInStep--;
            if (_beatsLeftInStep <= 0) {
                _currentStepIndex++;
                if(_currentStepIndex < _currentPattern.sequence.Length)
                     _beatsLeftInStep = Mathf.Max(1, _currentPattern.sequence[_currentStepIndex].beats);
            }
        }

        // --- Helpers ---

        private void SpawnTarget(RhythmPattern.Step step) {
            if (!targetPrefab || !spawnArea) { StartNextBeat(); return; }
            if (_currentTarget) Destroy(_currentTarget.gameObject);

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
                if (requiredHoldTime >= totalStepDuration) requiredHoldTime = totalStepDuration * 0.9f;
            }

            _currentTarget = t;
            t.Setup(isHold, totalStepDuration, requiredHoldTime, isHold ? "HOLD" : "CLICK", (hit) => {
                ResolveBeat(hit);
            });
        }

        private void ResolveBeat(bool hit) {
            if (!_isRhythmPlaying) return;
            if (_currentTarget) Destroy(_currentTarget.gameObject);

            if (_currentBeatIsScorable) {
                if (hit) { 
                    _beatsHit++; 
                    _seqTotalHits++; // Cộng vào tổng sequence
                    _trust += (100f/_totalScorableBeats); 
                }
                else { 
                    _beatsMiss++; 
                    _seqTotalMisses++; // Cộng vào tổng sequence
                    _trust -= (50f/_totalScorableBeats); 
                }
                _trust = Mathf.Clamp(_trust, 0, 100);
            }
            
            if(hud) 
            {
                hud.SetTrust01(_trust/100f);
                // [FIX DISPLAY] Hiển thị tổng số Hit/Miss của cả chuỗi (Farm Sequence)
                hud.UpdateHitMiss(_seqTotalHits, _seqTotalMisses); 
                hud.SetStatus(hit ? "Perfect!" : "Miss!", hit);
            }
            
            StartNextBeat();
        }

        // ... (Giữ nguyên các hàm helper khác: CalculateTotalScorableBeatsForAllStages, BuildPlaylistForStage, SetupPattern, RestCoroutine) ...
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
                        if(p && p.sequence != null) 
                            foreach(var s in p.sequence) 
                                if(s.type != RhythmPattern.StepType.Rest) 
                                    _totalScorableBeats += Mathf.Max(1, s.beats);
                    }
                }
            }
            if (_totalScorableBeats == 0) _totalScorableBeats = 1;
        }

        private void BuildPlaylistForStage(PlantDefinition.PlantStageData stageData)
        {
            _playlist.Clear();
            if (stageData.patterns != null) foreach (var pat in stageData.patterns) if(pat) _playlist.Add(pat);
        }

        private void SetupPattern(RhythmPattern p) {
            _currentPattern = p;
            _currentStepIndex = 0;
            if (p.sequence != null && p.sequence.Length > 0)
                _beatsLeftInStep = Mathf.Max(1, p.sequence[0].beats);
        }

        private IEnumerator RestCoroutine(float dur) {
            yield return new WaitForSeconds(dur);
            _restCoroutine = null;
            StartNextBeat();
        }
    }
}