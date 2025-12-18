using System.Collections;
using System.Collections.Generic;
using IronIvy.Core;
using IronIvy.Data;
using IronIvy.Gameplay.Animals;
using IronIvy.Interfaces;
using IronIvy.Systems.Camera;
using UnityEngine;

namespace IronIvy.Gameplay.Rhythm
{
    public class ClickAnimalRhythmMinigame : MonoBehaviour, IMinigame
    {
        [Header("Root / Focus")]
        public Transform defaultRoot;

        [Header("Target")]
        [Tooltip("Runtime injected from UIManager.Notify.rhythmSpawnArea")]
        [SerializeField] private RectTransform spawnArea;
        public RhythmClickTarget targetPrefab;

        [Header("Playlist / Pattern")]
        public List<RhythmPattern> fallbackPlaylist = new List<RhythmPattern>();

        [Header("Beat Settings")]
        public float bpm = 90f;
        public float BeatDuration => 60f / Mathf.Max(1f, bpm);
        public float defaultHoldRequiredSeconds = 0.7f;

        [Header("BGM (fallback)")]
        public string bgmKey = "animal_rhythm_bgm";

        [Header("SFX (Hit / Miss)")]
        public AudioClip hitSfx;
        public AudioClip missSfx;
        [Range(0f, 1f)] public float hitSfxVolume = 1f;
        [Range(0f, 1f)] public float missSfxVolume = 1f;

        [Header("Debug")]
        public bool logFlow = false;

        public bool IsRunning { get; private set; }

        private AnimalController _currentAnimal;
        private Transform _currentFocus;

        private bool _hasFavoriteBuff;
        private int _safetyNetRemains;

        private readonly List<RhythmPattern> _playlist = new List<RhythmPattern>();
        private int _playlistIndex;
        private RhythmPattern _currentPattern;

        private int _currentStepIndex;
        private int _beatsLeftInStep;
        private int _globalScorableBeatIndex;

        private int _totalBeatsForProgress;
        private int _totalBeatsForTimeline;

        private RhythmClickTarget _currentTarget;
        private Coroutine _restCoroutine;
        private float _currentBeatDuration;
        private RhythmPattern _runtimeRestPattern;

        private int _totalHit;
        private int _totalMiss;
        private float _trust01;

        private RhythmPattern.StepType _activeStepType;
        private bool _activeIsScorable;
        private bool _activeIsHold;
        private int _activeStepIndexSnapshot;
        private int _activeBeatIndexSnapshot;

        public void SetSpawnArea(RectTransform area) => spawnArea = area;

        public void RequestPlay(AnimalController animal, bool isFavoriteBuff = false)
        {
            _currentAnimal = animal;
            _currentFocus = (animal != null) ? animal.transform : (defaultRoot != null ? defaultRoot : transform);

            _hasFavoriteBuff = isFavoriteBuff;

            int baseSafety = 3;
            var animalDef = (animal != null) ? animal.Definition : null;
            if (animalDef != null)
                baseSafety = animalDef.buffSafetyNet;

            _safetyNetRemains = _hasFavoriteBuff ? baseSafety : 0;

            StartGame();
        }

        public void StartGame()
        {
            if (IsRunning) return;

            if (_currentFocus == null)
                _currentFocus = defaultRoot != null ? defaultRoot : transform;

            var animalDef = _currentAnimal != null ? _currentAnimal.Definition : null;

            BuildPlaylist(_playlist);
            if (_playlist.Count == 0) return;

            _totalBeatsForProgress = 0;
            foreach (var pat in _playlist)
                _totalBeatsForProgress += CountScorableBeatsInPattern(pat);

            _totalBeatsForTimeline = 0;
            foreach (var pat in _playlist)
                _totalBeatsForTimeline += CountBeatsInPattern(pat);

            if (_totalBeatsForTimeline <= 0)
                _totalBeatsForTimeline = Mathf.Max(1, _totalBeatsForProgress);

            _playlistIndex = 0;
            _currentPattern = null;
            _currentStepIndex = 0;
            _beatsLeftInStep = 0;
            _globalScorableBeatIndex = 0;
            _currentBeatDuration = BeatDuration;

            _totalHit = 0;
            _totalMiss = 0;
            _trust01 = 0f;

            bool isRandomMode = (animalDef != null && animalDef.useRandomRhythm);

            if (ListenManager.HasInstance)
            {
                string title = (animalDef != null) ? animalDef.displayName : "Animal Rhythm";
                if (isRandomMode) title += " [Mix]";
                if (_hasFavoriteBuff) title += " <color=green>[Protected]</color>";

                ListenManager.Instance.RaiseRhythmHUDShow(
                    new ListenManager.RhythmHUDShowPayload(
                        title,
                        false,
                        _totalBeatsForTimeline,
                        BeatDuration,
                        true
                    )
                );
            }

            if (CameraManager.HasInstance)
                CameraManager.Instance.ApplyAnimalMinigameProfile(_currentFocus);

            if (AudioManager.HasInstance)
            {
                if (animalDef != null && animalDef.minigameMusicLoop != null)
                    AudioManager.Instance.PlayBGM(animalDef.minigameMusicLoop.name);
                else if (!string.IsNullOrEmpty(bgmKey))
                    AudioManager.Instance.PlayBGM(bgmKey);
            }

            IsRunning = true;

            SetupPattern(_playlist[_playlistIndex]);
            StartNextBeat();
        }

        public void StopGame()
        {
            if (!IsRunning) return;
            IsRunning = false;

            KillTarget();

            if (_restCoroutine != null)
            {
                StopCoroutine(_restCoroutine);
                _restCoroutine = null;
            }

            if (AudioManager.HasInstance)
                AudioManager.Instance.FadeOutBGM();

            if (CameraManager.HasInstance)
                CameraManager.Instance.RestoreMinigameCamera();

            if (ListenManager.HasInstance)
                ListenManager.Instance.RaiseRhythmHUDHide();

            float successRatio = Mathf.Clamp01(_trust01);
            float archiveGained = GrantArchiveReward(successRatio);

            FoodItem lootItem;
            int lootCount;
            GrantLootReward(successRatio, out lootItem, out lootCount);

            if (ListenManager.HasInstance)
            {
                ListenManager.Instance.RaiseRhythmAnimalResult(
                    new ListenManager.RhythmAnimalResultPayload(
                        _currentAnimal,
                        successRatio,
                        archiveGained,
                        lootItem,
                        lootCount,
                        _totalHit,
                        _totalMiss
                    )
                );
            }

            if (_currentAnimal != null)
            {
                _currentAnimal.MarkMinigamePlayed();

                // IMPORTANT: chỉ queue, KHÔNG despawn ngay
                _currentAnimal.QueueDespawnAfterMinigame(successRatio);
            }

            _currentAnimal = null;
            _currentFocus = null;
        }

        private void StartNextBeat()
        {
            if (!IsRunning) return;

            if (_currentPattern == null || _currentPattern.sequence == null)
            {
                StopGame();
                return;
            }

            if (_currentStepIndex >= _currentPattern.sequence.Length)
            {
                _playlistIndex++;
                if (_playlistIndex >= _playlist.Count)
                {
                    StopGame();
                    return;
                }

                SetupPattern(_playlist[_playlistIndex]);
                StartNextBeat();
                return;
            }

            var step = _currentPattern.sequence[_currentStepIndex];
            _currentBeatDuration = BeatDuration;

            _activeStepType = step.type;
            _activeIsHold = (step.type == RhythmPattern.StepType.Hold);
            _activeIsScorable = (step.type == RhythmPattern.StepType.Tap || step.type == RhythmPattern.StepType.Hold);
            _activeStepIndexSnapshot = _currentStepIndex;
            _activeBeatIndexSnapshot = _globalScorableBeatIndex;

            if (step.type == RhythmPattern.StepType.Rest)
            {
                KillTarget();

                if (_restCoroutine != null)
                    StopCoroutine(_restCoroutine);

                _restCoroutine = StartCoroutine(RestBeatCoroutine(_currentBeatDuration));
            }
            else
            {
                SpawnTargetForActiveStep();
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

            AdvanceAfterResolve(scored: false);
            StartNextBeat();
        }

        private void SpawnTargetForActiveStep()
        {
            if (targetPrefab == null || spawnArea == null)
            {
                AdvanceAfterResolve(scored: false);
                StartNextBeat();
                return;
            }

            KillTarget();

            RhythmClickTarget target = Instantiate(targetPrefab, spawnArea);
            RectTransform rt = target.GetComponent<RectTransform>();
            if (rt == null)
            {
                Destroy(target.gameObject);
                AdvanceAfterResolve(scored: false);
                StartNextBeat();
                return;
            }

            Vector2 areaSize = spawnArea.rect.size;
            float x = Random.Range(-areaSize.x * 0.5f, areaSize.x * 0.5f);
            float y = Random.Range(-areaSize.y * 0.5f, areaSize.y * 0.5f);
            rt.anchoredPosition = new Vector2(x, y);

            _currentTarget = target;
            _currentTarget.autoDestroyOnResolve = false;

            target.Setup(
                _activeIsHold,
                _currentBeatDuration,
                defaultHoldRequiredSeconds,
                _activeIsHold ? "GIỮ CHUỘT" : "CLICK CHUỘT",
                OnTargetResolved
            );
        }

        private void OnTargetResolved(bool hit)
        {
            ResolveBeat(hit);
        }

        private void ResolveBeat(bool hit)
        {
            if (!IsRunning) return;

            KillTarget();

            string statusMsg = "";
            bool statusPositive = false;

            float trustStep = (_totalBeatsForProgress > 0) ? (1f / _totalBeatsForProgress) : 0f;

            if (_activeIsScorable)
            {
                if (hit)
                {
                    if (AudioManager.HasInstance)
                        AudioManager.Instance.PlaySEClip(hitSfx, hitSfxVolume);

                    _totalHit++;
                    statusMsg = _hasFavoriteBuff ? "Perfect! (Buffed)" : "Hit!";
                    statusPositive = true;

                    _trust01 += trustStep;
                }
                else
                {
                    if (_hasFavoriteBuff && _safetyNetRemains > 0)
                    {
                        _safetyNetRemains--;
                        statusMsg = $"Shield! ({_safetyNetRemains} left)";
                        statusPositive = true;
                    }
                    else
                    {
                        if (AudioManager.HasInstance)
                            AudioManager.Instance.PlaySEClip(missSfx, missSfxVolume);

                        _totalMiss++;
                        statusMsg = "Miss";
                        statusPositive = false;

                        _trust01 -= trustStep;
                    }
                }
            }

            _trust01 = Mathf.Clamp01(_trust01);

            float progress01 = (_totalBeatsForProgress > 0)
                ? Mathf.Clamp01((float)(_totalHit + _totalMiss) / _totalBeatsForProgress)
                : 0f;

            if (ListenManager.HasInstance)
            {
                ListenManager.Instance.RaiseRhythmHUDUpdate(
                    new ListenManager.RhythmHUDUpdatePayload(
                        _totalHit,
                        _totalMiss,
                        _trust01,
                        progress01,
                        _activeIsScorable ? statusMsg : "",
                        statusPositive,
                        0f,
                        _activeBeatIndexSnapshot,
                        _activeStepType.ToString(),
                        _activeIsHold
                    )
                );
            }

            AdvanceAfterResolve(scored: _activeIsScorable);

            if (IsRunning)
                StartNextBeat();
        }

        private void AdvanceAfterResolve(bool scored)
        {
            if (scored)
                _globalScorableBeatIndex++;

            _beatsLeftInStep--;
            if (_beatsLeftInStep <= 0)
            {
                _currentStepIndex++;
                PrepareNextStep();
            }
        }

        private void KillTarget()
        {
            if (_currentTarget != null)
            {
                Destroy(_currentTarget.gameObject);
                _currentTarget = null;
            }
        }

        private void BuildPlaylist(List<RhythmPattern> outList)
        {
            outList.Clear();

            AnimalDefinition def = _currentAnimal != null ? _currentAnimal.Definition : null;
            bool added = false;

            if (def != null)
            {
                if (def.useRandomRhythm)
                {
                    // IMPORTANT: random OK thì không cần patterns manual nữa
                    added = BuildRandomPlaylistForAnimal(def, outList);
                }
                else
                {
                    added = BuildFixedPlaylistForAnimal(def, outList);
                }
            }

            if (!added)
                BuildFallbackPlaylist(outList);
        }

        private bool BuildFixedPlaylistForAnimal(AnimalDefinition def, List<RhythmPattern> outList)
        {
            if (def == null || def.patterns == null || def.patterns.Length == 0)
                return false;

            for (int i = 0; i < def.patterns.Length; i++)
            {
                var pat = def.patterns[i];
                if (pat != null) outList.Add(pat);
            }

            return outList.Count > 0;
        }

        private void BuildFallbackPlaylist(List<RhythmPattern> outList)
        {
            if (fallbackPlaylist == null) return;
            for (int i = 0; i < fallbackPlaylist.Count; i++)
                if (fallbackPlaylist[i] != null) outList.Add(fallbackPlaylist[i]);
        }

        private bool BuildRandomPlaylistForAnimal(AnimalDefinition def, List<RhythmPattern> outList)
        {
            if (def == null) return false;

            var pool = def.randomFragments;
            if (pool == null || pool.Length == 0) return false;

            List<RhythmPattern> fragmentPool = new List<RhythmPattern>();
            for (int i = 0; i < pool.Length; i++)
            {
                var pat = pool[i];
                if (pat == null) continue;
                if (pat.sequence == null || pat.sequence.Length == 0) continue;

                // FIX: đừng dùng GetTotalBeats() vì hay ra 0 khi beats=0
                if (CountScorableBeatsInPattern(pat) <= 0) continue;

                fragmentPool.Add(pat);
            }

            if (fragmentPool.Count == 0) return false;

            int minBeats = Mathf.Max(1, def.minRandomBeats);
            int maxBeats = Mathf.Max(minBeats, def.maxRandomBeats);
            int minFragments = Mathf.Max(1, def.minRandomFragments);
            int maxFragments = Mathf.Max(minFragments, def.maxRandomFragments);

            int totalBeats = 0;
            int fragmentsUsed = 0;
            int safety = 128;

            List<RhythmPattern> working = new List<RhythmPattern>(fragmentPool);
            ShuffleList(working);

            int idx = 0;
            bool isFirst = true;
            int restBeatsBetween = 1;

            while (safety-- > 0 && fragmentsUsed < maxFragments && totalBeats < maxBeats)
            {
                if (idx >= working.Count)
                {
                    ShuffleList(working);
                    idx = 0;
                }

                var pick = working[idx++];
                int beats = CountScorableBeatsInPattern(pick);
                if (beats <= 0) continue;

                if (!isFirst && restBeatsBetween > 0)
                    outList.Add(GetOrCreateRestPattern(restBeatsBetween));

                outList.Add(pick);
                totalBeats += beats;
                fragmentsUsed++;
                isFirst = false;

                if (fragmentsUsed >= minFragments && totalBeats >= minBeats && totalBeats <= maxBeats)
                    break;
            }

            return outList.Count > 0;
        }

        private void ShuffleList<T>(List<T> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                int j = Random.Range(i, list.Count);
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }

        private int CountBeatsInPattern(RhythmPattern pattern)
        {
            if (pattern == null || pattern.sequence == null) return 0;

            // FIX: beats=0 coi như 1 beat, để randomFragments không bị coi "invalid"
            int total = 0;
            var seq = pattern.sequence;

            for (int i = 0; i < seq.Length; i++)
            {
                int beats = seq[i].beats;
                if (beats <= 0) beats = 1;
                total += beats;
            }

            return total;
        }

        private int CountScorableBeatsInPattern(RhythmPattern pattern)
        {
            if (pattern == null || pattern.sequence == null) return 0;

            int total = 0;
            var seq = pattern.sequence;

            for (int i = 0; i < seq.Length; i++)
            {
                var s = seq[i];
                if (s.type == RhythmPattern.StepType.Tap || s.type == RhythmPattern.StepType.Hold)
                {
                    int beats = s.beats;
                    if (beats <= 0) beats = 1;
                    total += beats;
                }
            }

            return total;
        }

        private void SetupPattern(RhythmPattern pattern)
        {
            _currentPattern = pattern;
            _currentStepIndex = 0;
            _beatsLeftInStep = 0;

            if (pattern == null || pattern.sequence == null || pattern.sequence.Length == 0)
            {
                StopGame();
                return;
            }

            PrepareNextStep();
        }

        private void PrepareNextStep()
        {
            if (_currentPattern == null || _currentPattern.sequence == null) return;
            if (_currentStepIndex >= _currentPattern.sequence.Length) return;

            var step = _currentPattern.sequence[_currentStepIndex];
            _beatsLeftInStep = Mathf.Max(1, step.beats <= 0 ? 1 : step.beats);
        }

        private float GrantArchiveReward(float successRatio)
        {
            if (_currentAnimal == null) return 0f;
            if (!ArchiveManager.HasInstance) return 0f;

            var animalDef = _currentAnimal.Definition;
            if (animalDef == null) return 0f;

            float baseReward = animalDef.archiveReward;
            if (baseReward <= 0f) return 0f;

            float multiplier = _hasFavoriteBuff ? animalDef.buffTrustMultiplier : 1f;

            float finalReward = 0f;
            if (successRatio >= 0.99f) finalReward = baseReward * multiplier;
            else if (successRatio >= 0.5f) finalReward = baseReward * 0.5f * multiplier;

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
            if (successRatio < 0.5f) return;

            var animalDef = _currentAnimal.Definition;
            if (animalDef.dropItem == null || animalDef.dropCount <= 0) return;

            item = animalDef.dropItem;
            count = animalDef.dropCount;

            if (_hasFavoriteBuff && animalDef.doubleLootOnBuff)
                count *= 2;

            InventoryManager.Instance.AddFood(item, count);
        }

        private RhythmPattern GetOrCreateRestPattern(int beats = 1)
        {
            if (_runtimeRestPattern == null)
            {
                _runtimeRestPattern = ScriptableObject.CreateInstance<RhythmPattern>();
                _runtimeRestPattern.patternId = "runtime_rest";
                _runtimeRestPattern.displayName = "Runtime Rest";
                _runtimeRestPattern.bpm = Mathf.RoundToInt(bpm);
                _runtimeRestPattern.hitWindowSeconds = 0.2f;
            }

            _runtimeRestPattern.sequence = new RhythmPattern.Step[]
            {
                new RhythmPattern.Step { type = RhythmPattern.StepType.Rest, beats = Mathf.Max(1, beats) }
            };

            return _runtimeRestPattern;
        }
    }
}
