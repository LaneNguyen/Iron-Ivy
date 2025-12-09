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
    public class ClickAnimalRhythmMinigame : MonoBehaviour, IMinigame
    {
        [Header("Root / Focus")]
        public Transform defaultRoot;

        [Header("UI & Target")]
        public RhythmHUD hud;
        public RectTransform spawnArea;
        public RhythmClickTarget targetPrefab;

        [Header("Reward Panel")]
        public AnimalRhythmRewardPanel rewardPanel;

        [Header("Playlist / Pattern")]
        public List<RhythmPattern> fallbackPlaylist = new List<RhythmPattern>();

        [Header("Beat Settings")]
        public float bpm = 90f;
        public float BeatDuration => 60f / Mathf.Max(1f, bpm);
        public float defaultHoldRequiredSeconds = 0.7f;

        [Header("BGM")]
        public string bgmKey = "animal_rhythm_bgm";

        // Runtime state
        public bool IsRunning { get; private set; }

        private AnimalController _currentAnimal;
        private Transform _currentFocus;

        // buff / safety net
        private bool _hasFavoriteBuff;
        private int _safetyNetRemains;

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

        // trust visual 0–1 cho HUD + dùng luôn làm successRatio
        private float _trust01;

        private void Awake()
        {
            if (hud != null) hud.ResetHUD();
        }

        // ===== PUBLIC API =====
        public void RequestPlay(AnimalController animal, bool isFavoriteBuff = false)
        {
            _currentAnimal = animal;
            _currentFocus = (animal != null) ? animal.transform : (defaultRoot != null ? defaultRoot : transform);

            _hasFavoriteBuff = isFavoriteBuff;

            int baseSafety = 0;
            if (animal != null && animal.Definition != null)
                baseSafety = animal.Definition.buffSafetyNet;
            else
                baseSafety = 3; // fallback

            _safetyNetRemains = _hasFavoriteBuff ? baseSafety : 0;

            Debug.Log($"[AnimalRhythm] Start. Buff: {_hasFavoriteBuff}. SafetyNet: {_safetyNetRemains}");
            StartGame();
        }

        public void StartGame()
        {
            if (IsRunning) return;
            if (_currentFocus == null)
                _currentFocus = defaultRoot != null ? defaultRoot : transform;

            BuildPlaylist(_playlist);
            if (_playlist.Count == 0) return;

            // tổng beat dùng cho progress & trust
            _totalBeatsForProgress = 0;
            foreach (var pat in _playlist)
                _totalBeatsForProgress += CountBeatsInPattern(pat);

            _playlistIndex = 0;
            _currentPattern = null;
            _currentStepIndex = 0;
            _beatsLeftInStep = 0;
            _globalBeatIndex = 0;
            _currentBeatDuration = BeatDuration;

            _totalHit = 0;
            _totalMiss = 0;
            _trust01 = 0f;

            if (hud != null)
            {
                if (hud.hudRoot != null) hud.hudRoot.SetActive(true);

                string title = (_currentAnimal != null && _currentAnimal.Definition != null)
                    ? _currentAnimal.Definition.displayName
                    : "Animal Rhythm";

                // hiện label nếu có buff
                if (_hasFavoriteBuff)
                    title += " <color=green>[Protected]</color>";

                if (hud.titleText != null)
                    hud.titleText.text = title;

                hud.SetStatus("Ready", false);
                hud.SetProgress(0f);
                hud.SetHitMiss(0, 0);
                hud.SetHoldVisual(0f);
                hud.SetTrust01(0f);
            }

            if (CameraManager.HasInstance)
                CameraManager.Instance.ApplyAnimalMinigameProfile(_currentFocus);

            if (AudioManager.HasInstance && !string.IsNullOrEmpty(bgmKey))
                AudioManager.Instance.PlayBGM(bgmKey);

            if (ListenManager.HasInstance)
                ListenManager.Instance.RaiseMinigameStarted();

            IsRunning = true;
            SetupPattern(_playlist[_playlistIndex]);
            StartNextBeat();
        }

        public void StopGame()
        {
            if (!IsRunning) return;
            IsRunning = false;

            if (_currentTarget != null)
            {
                Destroy(_currentTarget.gameObject);
                _currentTarget = null;
            }

            if (_restCoroutine != null)
            {
                StopCoroutine(_restCoroutine);
                _restCoroutine = null;
            }

            if (AudioManager.HasInstance)
                AudioManager.Instance.FadeOutBGM();

            if (CameraManager.HasInstance)
                CameraManager.Instance.RestoreMinigameCamera();

            if (hud != null)
            {
                hud.UpdateHitMiss(_totalHit, _totalMiss);
                hud.ResetHUD();
            }

            // successRatio = trust cuối cùng
            float successRatio = ComputeSuccessRatio();
            float finalReward = GrantArchiveReward(successRatio);

            FoodItem lootItem;
            int lootCount;
            GrantLootReward(successRatio, out lootItem, out lootCount);

            if (ListenManager.HasInstance)
                ListenManager.Instance.RaiseMinigameStopped();

            if (_currentAnimal != null && rewardPanel != null)
            {
                rewardPanel.ShowAnimalRhythmResult(_currentAnimal, successRatio, finalReward, lootItem, lootCount);
            }

            if (_currentAnimal != null)
                _currentAnimal.MarkMinigamePlayed();

            _currentAnimal = null;
            _currentFocus = null;
        }

        // ===== PLAYLIST / PATTERN =====
        private void BuildPlaylist(List<RhythmPattern> outList)
        {
            outList.Clear();
            AnimalDefinition def = _currentAnimal != null ? _currentAnimal.Definition : null;

            if (def != null && def.patterns != null && def.patterns.Length > 0)
            {
                foreach (var pat in def.patterns)
                    if (pat != null) outList.Add(pat);
            }
            else
            {
                foreach (var pat in fallbackPlaylist)
                    if (pat != null) outList.Add(pat);
            }
        }

        private int CountBeatsInPattern(RhythmPattern pattern)
        {
            if (pattern == null || pattern.sequence == null) return 0;
            int count = 0;
            foreach (var step in pattern.sequence)
                count += Mathf.Max(1, step.beats <= 0 ? 1 : step.beats);
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
                if (_playlistIndex >= _playlist.Count)
                {
                    OnPlaylistComplete();
                    return;
                }

                SetupPattern(_playlist[_playlistIndex]);
                return;
            }

            var step = _currentPattern.sequence[_currentStepIndex];
            _beatsLeftInStep = Mathf.Max(1, step.beats <= 0 ? 1 : step.beats);
        }

        private void StartNextBeat()
        {
            if (!IsRunning) return;
            if (_currentPattern == null || _currentPattern.sequence == null)
            {
                OnPlaylistComplete();
                return;
            }

            if (_currentStepIndex >= _currentPattern.sequence.Length)
            {
                _playlistIndex++;
                if (_playlistIndex >= _playlist.Count)
                {
                    OnPlaylistComplete();
                    return;
                }

                SetupPattern(_playlist[_playlistIndex]);
                return;
            }

            var step = _currentPattern.sequence[_currentStepIndex];
            _currentBeatDuration = BeatDuration;

            if (step.type == RhythmPattern.StepType.Rest)
            {
                if (_currentTarget != null)
                {
                    Destroy(_currentTarget.gameObject);
                    _currentTarget = null;
                }

                if (_restCoroutine != null)
                    StopCoroutine(_restCoroutine);

                _restCoroutine = StartCoroutine(RestBeatCoroutine(_currentBeatDuration));
            }
            else
            {
                SpawnTargetForStep(step);
            }

            _globalBeatIndex++;
            UpdateProgressUI();

            _beatsLeftInStep--;
            if (_beatsLeftInStep <= 0)
            {
                _currentStepIndex++;
                PrepareNextStep();
            }
        }

        private IEnumerator RestBeatCoroutine(float duration)
        {
            float timer = 0f;
            while (timer < duration)
            {
                timer += Time.deltaTime;
                yield return null;
            }

            _restCoroutine = null;
            StartNextBeat();
        }

        private void SpawnTargetForStep(RhythmPattern.Step step)
        {
            if (targetPrefab == null || spawnArea == null)
            {
                StartNextBeat();
                return;
            }

            if (_currentTarget != null)
            {
                Destroy(_currentTarget.gameObject);
                _currentTarget = null;
            }

            RhythmClickTarget target = Instantiate(targetPrefab, spawnArea);
            RectTransform rt = target.GetComponent<RectTransform>();
            if (rt == null)
            {
                Destroy(target.gameObject);
                StartNextBeat();
                return;
            }

            Vector2 areaSize = spawnArea.rect.size;
            float x = Random.Range(-areaSize.x * 0.5f, areaSize.x * 0.5f);
            float y = Random.Range(-areaSize.y * 0.5f, areaSize.y * 0.5f);
            rt.anchoredPosition = new Vector2(x, y);

            bool isHold = step.type == RhythmPattern.StepType.Hold;
            _currentTarget = target;
            target.Setup(
                isHold,
                _currentBeatDuration,
                defaultHoldRequiredSeconds,
                isHold ? "HOLD" : "CLICK",
                OnTargetResolved
            );
        }

        private void OnTargetResolved(bool hit)
        {
            ResolveBeat(hit);
        }

        // ===== CORE SCORING + TRUST VISUAL =====
        private void ResolveBeat(bool? hit)
        {
            if (!IsRunning) return;

            if (_currentTarget != null)
            {
                Destroy(_currentTarget.gameObject);
                _currentTarget = null;
            }

            bool isScorableStep = false;
            if (_currentPattern != null && _currentStepIndex < _currentPattern.sequence.Length)
            {
                var step = _currentPattern.sequence[_currentStepIndex];
                isScorableStep = (step.type == RhythmPattern.StepType.Tap || step.type == RhythmPattern.StepType.Hold);
            }

            string statusMsg = "";
            bool statusPositive = false;

            // mỗi beat scorable sẽ ảnh hưởng tới hit/miss + trust
            float trustStep = (_totalBeatsForProgress > 0)
                ? (1f / _totalBeatsForProgress)
                : 0f;

            if (hit == true && isScorableStep)
            {
                _totalHit++;
                statusMsg = "Hit!";

                if (_hasFavoriteBuff)
                    statusMsg = "Perfect! (Buffed)";

                statusPositive = true;

                // hit thì cộng trust
                _trust01 += trustStep;
            }
            else if (hit == false && isScorableStep)
            {
                // [LOGIC] SAFETY NET (Bảo hiểm)
                if (_hasFavoriteBuff && _safetyNetRemains > 0)
                {
                    // được cứu, không trừ trust
                    _safetyNetRemains--;
                    statusMsg = $"Shield! ({_safetyNetRemains} left)";
                    statusPositive = true;
                }
                else
                {
                    _totalMiss++;
                    statusMsg = "Miss";
                    statusPositive = false;

                    // miss thì trừ trust
                    _trust01 -= trustStep;
                }
            }

            // clamp lại cho an toàn
            _trust01 = Mathf.Clamp01(_trust01);

            if (hud != null)
            {
                hud.SetHitMiss(_totalHit, _totalMiss);

                if (isScorableStep)
                    hud.SetStatus(statusMsg, statusPositive);

                // trust hiển thị dạng 0–1
                hud.SetTrust01(_trust01);
            }

            if (IsRunning)
                StartNextBeat();
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

        // ===== REWARD CALC =====
        private float ComputeSuccessRatio()
        {
            // dùng luôn trust cuối game làm successRatio (0–1)
            return Mathf.Clamp01(_trust01);
        }

        private float GrantArchiveReward(float successRatio)
        {
            if (_currentAnimal == null) return 0f;
            if (!ArchiveManager.HasInstance) return 0f;

            var def = _currentAnimal.Definition;
            if (def == null) return 0f;

            float baseReward = def.archiveReward;
            if (baseReward <= 0f) return 0f;

            float finalReward = 0f;

            float multiplier = 1f;
            if (_hasFavoriteBuff)
                multiplier = def.buffTrustMultiplier;

            if (successRatio >= 0.99f)      finalReward = baseReward * multiplier;
            else if (successRatio >= 0.5f) finalReward = baseReward * 0.5f * multiplier;
            else                            finalReward = 0f;

            if (finalReward > 0f)
                ArchiveManager.Instance.AddProgress(finalReward);

            return finalReward;
        }

        private void GrantLootReward(float successRatio, out FoodItem item, out int count)
        {
            item = null;
            count = 0;

            if (_currentAnimal == null || _currentAnimal.Definition == null) return;
            if (!InventoryManager.HasInstance) return;

            // chỉ thưởng loot nếu success >= 50%
            if (successRatio < 0.5f) return;

            var def = _currentAnimal.Definition;
            if (def.dropItem == null || def.dropCount <= 0) return;

            item = def.dropItem;
            count = def.dropCount;

            // buff có thể nhân đôi loot
            if (_hasFavoriteBuff && def.doubleLootOnBuff)
                count *= 2;

            InventoryManager.Instance.AddFood(item, count);
        }
    }
}
