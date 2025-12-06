using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using IronIvy.Core;
using IronIvy.Data;
using IronIvy.UI;
using IronIvy.Interfaces;
using IronIvy.Gameplay.Animals;
using IronIvy.Systems.Camera;

namespace IronIvy.Gameplay.Rhythm
{
    // Animal rhythm minigame kieu click/hold target
    public class ClickAnimalRhythmMinigame : MonoBehaviour, IMinigame
    {
        [Header("Root / Focus")]
        [Tooltip("Fallback neu khong co animal focus, dung chinh object nay.")]
        public Transform defaultRoot;

        [Header("UI & Target")]
        public RhythmHUD hud;
        public RectTransform spawnArea;
        public RhythmClickTarget targetPrefab;

        [Header("Reward Panel")]
        [Tooltip("Panel recap rieng cho animal rhythm. Keo prefab panel vao day.")]
        public AnimalRhythmRewardPanel rewardPanel;

        [Header("Playlist / Pattern (fallback)")]
        [Tooltip("Playlist fallback neu AnimalDefinition khong co pattern.")]
        public List<RhythmPattern> fallbackPlaylist = new List<RhythmPattern>();

        [Header("Beat Settings")]
        [Tooltip("Beats Per Minute cho minigame.")]
        public float bpm = 90f;

        [Tooltip("Thoi gian beat (giay) = 60 / bpm.")]
        public float BeatDuration => 60f / Mathf.Max(1f, bpm);

        [Tooltip("Thoi gian can giu chuot de tinh hold (giay).")]
        public float defaultHoldRequiredSeconds = 0.7f;

        [Header("BGM")]
        [Tooltip("Ten key BGM dung cho animal rhythm, de rong neu khong muon auto play.")]
        public string bgmKey = "animal_rhythm_bgm";

        // -------- Runtime state --------
        public bool IsRunning { get; private set; }

        private AnimalController _currentAnimal;
        private Transform _currentFocus;

        private readonly List<RhythmPattern> _playlist = new List<RhythmPattern>();
        private RhythmPattern _currentPattern;
        private int _playlistIndex;
        private int _currentStepIndex;
        private int _beatsLeftInStep;
        private int _globalBeatIndex;
        private int _totalBeatsForProgress;

        private RhythmClickTarget _currentTarget;
        private Coroutine _restCoroutine;
        private float _currentBeatDuration;

        private int _totalHit;
        private int _totalMiss;

        private void Awake()
        {
            if (hud != null) hud.ResetHUD();
        }

        //=====================
        // API cho AnimalController
        //=====================
        public void RequestPlay(AnimalController animal)
        {
            _currentAnimal = animal;
            _currentFocus = (animal != null) ? animal.transform : (defaultRoot != null ? defaultRoot : transform);
            StartGame();
        }

        //=====================
        // IMinigame
        //=====================
        public void StartGame()
        {
            if (IsRunning) return;

            if (_currentFocus == null) _currentFocus = defaultRoot != null ? defaultRoot : transform;

            BuildPlaylist(_playlist);
            if (_playlist.Count == 0)
            {
                Debug.LogWarning("[ClickAnimalRhythm] No pattern in playlist.");
                return;
            }

            _totalBeatsForProgress = 0;
            foreach (var p in _playlist) _totalBeatsForProgress += CountBeatsInPattern(p);

            _playlistIndex = 0;
            _currentPattern = null;
            _currentStepIndex = 0;
            _beatsLeftInStep = 0;
            _globalBeatIndex = 0;
            _currentBeatDuration = BeatDuration;
            _totalHit = 0;
            _totalMiss = 0;

            if (hud != null)
            {
                if (hud.hudRoot != null) hud.hudRoot.SetActive(true);
                if (hud.titleText != null)
                {
                    string title = "Animal Rhythm";
                    if (_currentAnimal != null && _currentAnimal.Definition != null && !string.IsNullOrEmpty(_currentAnimal.Definition.displayName))
                        title = _currentAnimal.Definition.displayName;
                    hud.titleText.text = title;
                }
                hud.SetStatus("Ready", false);
                hud.SetProgress(0f);
                hud.SetHitMiss(0, 0);
                hud.SetHoldVisual(0f);
            }

            if (CameraManager.HasInstance) CameraManager.Instance.ApplyAnimalMinigameProfile(_currentFocus);
            if (AudioManager.HasInstance && !string.IsNullOrEmpty(bgmKey)) AudioManager.Instance.PlayBGM(bgmKey);

            if (ListenManager.HasInstance) ListenManager.Instance.RaiseMinigameStarted();

            IsRunning = true;
            SetupPattern(_playlist[_playlistIndex]);
            StartNextBeat();
        }

        public void StopGame()
        {
            if (!IsRunning) return;
            IsRunning = false;

            if (_currentTarget != null) { Destroy(_currentTarget.gameObject); _currentTarget = null; }
            if (_restCoroutine != null) { StopCoroutine(_restCoroutine); _restCoroutine = null; }

            if (AudioManager.HasInstance) AudioManager.Instance.FadeOutBGM();
            if (CameraManager.HasInstance) CameraManager.Instance.RestoreMinigameCamera();

            if (hud != null)
            {
                hud.UpdateHitMiss(_totalHit, _totalMiss);
                hud.ResetHUD();
            }

            // Tính toán reward và raise event
            float successRatio = ComputeSuccessRatio();
            float finalReward = GrantArchiveReward(successRatio);

            if (ListenManager.HasInstance) ListenManager.Instance.RaiseMinigameStopped();

            if (_currentAnimal != null && rewardPanel != null)
                rewardPanel.ShowAnimalRhythmResult(_currentAnimal, successRatio, finalReward);

            if (_currentAnimal != null) _currentAnimal.MarkMinigamePlayed();

            _currentAnimal = null;
            _currentFocus = null;
        }

        private void OnDisable()
        {
            if (IsRunning) StopGame();
        }

        // ... (Giữ nguyên phần Playlist / Pattern / Beat logic không đổi) ...
        // ... (Để tiết kiệm không gian, tôi chỉ liệt kê các hàm logic beat ở dạng rút gọn, code logic bên trong không đổi) ...
        
        private void BuildPlaylist(List<RhythmPattern> outList)
        {
            outList.Clear();
            AnimalDefinition def = _currentAnimal != null ? _currentAnimal.Definition : null;
            if (def != null && def.patterns != null && def.patterns.Length > 0)
            {
                foreach (var p in def.patterns) if (p != null) outList.Add(p);
            }
            else
            {
                foreach (var p in fallbackPlaylist) if (p != null) outList.Add(p);
            }
        }

        private int CountBeatsInPattern(RhythmPattern p)
        {
            if (p == null || p.sequence == null) return 0;
            int count = 0;
            foreach (var step in p.sequence) count += Mathf.Max(1, step.beats <= 0 ? 1 : step.beats);
            return count;
        }

        private void SetupPattern(RhythmPattern pattern)
        {
            _currentPattern = pattern;
            _currentStepIndex = 0;
            _beatsLeftInStep = 0;
            if (pattern == null || pattern.sequence == null || pattern.sequence.Length == 0)
            {
                OnPlaylistComplete();
                return;
            }
            PrepareNextStep();
        }

        private void PrepareNextStep()
        {
            if (_currentPattern == null || _currentPattern.sequence == null) return;
            if (_currentStepIndex >= _currentPattern.sequence.Length)
            {
                _playlistIndex++;
                if (_playlistIndex >= _playlist.Count) { OnPlaylistComplete(); return; }
                SetupPattern(_playlist[_playlistIndex]);
                return;
            }
            var step = _currentPattern.sequence[_currentStepIndex];
            _beatsLeftInStep = Mathf.Max(1, step.beats <= 0 ? 1 : step.beats);
        }

        private void StartNextBeat()
        {
            if (!IsRunning) return;
            if (_currentPattern == null || _currentPattern.sequence == null) { OnPlaylistComplete(); return; }
            if (_currentStepIndex >= _currentPattern.sequence.Length)
            {
                _playlistIndex++;
                if (_playlistIndex >= _playlist.Count) { OnPlaylistComplete(); return; }
                SetupPattern(_playlist[_playlistIndex]);
                return;
            }

            var step = _currentPattern.sequence[_currentStepIndex];
            _currentBeatDuration = BeatDuration;

            if (step.type == RhythmPattern.StepType.Rest)
            {
                if (_currentTarget != null) { Destroy(_currentTarget.gameObject); _currentTarget = null; }
                if (_restCoroutine != null) StopCoroutine(_restCoroutine);
                _restCoroutine = StartCoroutine(RestBeatCoroutine(_currentBeatDuration));
            }
            else
            {
                SpawnTargetForStep(step);
            }

            _globalBeatIndex++;
            UpdateProgressUI();
            _beatsLeftInStep--;
            if (_beatsLeftInStep <= 0) { _currentStepIndex++; PrepareNextStep(); }
        }

        private IEnumerator RestBeatCoroutine(float duration)
        {
            float timer = 0f;
            while (timer < duration) { timer += Time.deltaTime; yield return null; }
            _restCoroutine = null;
            StartNextBeat();
        }

        private void SpawnTargetForStep(RhythmPattern.Step step)
        {
            if (targetPrefab == null || spawnArea == null) { StartNextBeat(); return; }
            if (_currentTarget != null) { Destroy(_currentTarget.gameObject); _currentTarget = null; }

            RhythmClickTarget target = Instantiate(targetPrefab, spawnArea);
            RectTransform rt = target.GetComponent<RectTransform>();
            if (rt == null) { Destroy(target.gameObject); StartNextBeat(); return; }

            Vector2 areaSize = spawnArea.rect.size;
            float x = Random.Range(-areaSize.x * 0.5f, areaSize.x * 0.5f);
            float y = Random.Range(-areaSize.y * 0.5f, areaSize.y * 0.5f);
            rt.anchoredPosition = new Vector2(x, y);

            bool isHold = step.type == RhythmPattern.StepType.Hold;
            _currentTarget = target;
            target.Setup(isHold, _currentBeatDuration, defaultHoldRequiredSeconds, isHold ? "HOLD" : "CLICK", OnTargetResolved);
        }

        private void OnTargetResolved(bool hit)
        {
            ResolveBeat(hit);
        }

        private void ResolveBeat(bool? hit)
        {
            if (!IsRunning) return;
            if (_currentTarget != null) { Destroy(_currentTarget.gameObject); _currentTarget = null; }

            bool isScorableStep = false;
            if (_currentPattern != null && _currentStepIndex < _currentPattern.sequence.Length)
            {
                var step = _currentPattern.sequence[_currentStepIndex];
                isScorableStep = (step.type == RhythmPattern.StepType.Tap || step.type == RhythmPattern.StepType.Hold);
            }

            if (hit == true && isScorableStep) _totalHit++;
            else if (hit == false && isScorableStep) _totalMiss++;

            if (hud != null)
            {
                hud.SetHitMiss(_totalHit, _totalMiss);
                if (hit == true && isScorableStep) hud.SetStatus("Hit!", true);
                else if (hit == false && isScorableStep) hud.SetStatus("Miss", false);
            }

            if (IsRunning) StartNextBeat();
        }

        private void UpdateProgressUI()
        {
            if (hud == null || _totalBeatsForProgress <= 0) return;
            hud.SetProgress(Mathf.Clamp01((float)_globalBeatIndex / _totalBeatsForProgress));
        }

        private void OnPlaylistComplete()
        {
            StopGame();
        }

        //=====================
        // Archive reward
        //=====================

        private float ComputeSuccessRatio()
        {
            int total = _totalHit + _totalMiss;
            if (total <= 0) return 0f;
            return (float)_totalHit / total;
        }

       // tra ve so % archive thuc su da cong
        private float GrantArchiveReward(float successRatio)
        {
            if (_currentAnimal == null) return 0f;
            if (!ArchiveManager.HasInstance) return 0f;

            var def = _currentAnimal.Definition;
            if (def == null) return 0f;

            // Kiểm tra xem Archive Reward trong data có > 0 không
            float baseReward = def.archiveReward; 
            if (baseReward <= 0f) 
            {
                Debug.LogWarning($"[ClickAnimalRhythm] Animal {def.name} has 0 archive reward configured!");
                return 0f;
            }

            float finalReward = 0f;

            if (successRatio >= 0.99f) finalReward = baseReward;
            else if (successRatio >= 0.5f) finalReward = baseReward * 0.5f;
            else finalReward = 0f;

            if (finalReward > 0f)
            {
                // CHỈ CẦN GỌI DÒNG NÀY, ArchiveManager sẽ tự Raise Event
                ArchiveManager.Instance.AddProgress(finalReward);
            }

            return finalReward;
        }
    }
}