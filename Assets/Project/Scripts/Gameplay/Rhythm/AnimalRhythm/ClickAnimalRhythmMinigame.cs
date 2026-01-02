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

        [Header("Target UI")]
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

        [Header("Guide - First time enter Animal Rhythm")]
        [SerializeField] private GameObject guidePanelFirstTime;
        [SerializeField] private string guideStepId = "guide.animal.rhythm.firsttime";
        [SerializeField] private bool pauseGameWhenGuideShown = true;
        [SerializeField] private bool ignorePrefsInEditor = true;
        [SerializeField] private bool disableMarkInEditor = true;

        [Header("Guide Timing")]
        [SerializeField] private float delayBeforeShowGuide = 0.6f; // thời gian HUD animate

        private GuidePanelView _activeGuide;
        private Coroutine _startFlowRoutine;


        [Header("Finish FX Material Fade")]
        public Material finishFxMaterial;
        public float finishFadeDuration = 0.8f;

        // URP Lit thường dùng _BaseColor, shader cũ hay dùng _Color
        public string fadeColorProperty = "_BaseColor";

        // Nếu muốn sau khi fade xong trả về material cũ (hiếm khi cần, vì animal sẽ despawn)
        public bool restoreOriginalAfterFade = false;

        private Coroutine _finishFadeRoutine;

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


        // --- Interface & Legacy Methods ---
        public void SetSpawnArea(RectTransform area) => spawnArea = area;
        public void Play() => StartGame();
        public void Stop() => StopGame();

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

            // Tính toán tổng số beat
            _totalBeatsForProgress = 0;
            foreach (var pat in _playlist) _totalBeatsForProgress += CountScorableBeatsInPattern(pat);

            _totalBeatsForTimeline = 0;
            foreach (var pat in _playlist) _totalBeatsForTimeline += CountBeatsInPattern(pat);

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

            // Audio & HUD
            if (ListenManager.HasInstance)
            {
                string title = (animalDef != null) ? animalDef.displayName : "Animal Rhythm";
                ListenManager.Instance.RaiseRhythmHUDShow(
                    new ListenManager.RhythmHUDShowPayload(title, false, _totalBeatsForTimeline, BeatDuration, true)
                );
            }

            if (CameraManager.HasInstance)
                CameraManager.Instance.ApplyAnimalMinigameProfile(_currentFocus); // Khôi phục tên hàm cũ

            if (AudioManager.HasInstance)
            {
                if (animalDef != null && animalDef.minigameMusicLoop != null)
                    AudioManager.Instance.PushBGM(animalDef.minigameMusicLoop.name); // Khôi phục PushBGM
                else if (!string.IsNullOrEmpty(bgmKey))
                    AudioManager.Instance.PlayBGM(bgmKey);
            }
            // reaction HUD context (light hook)
            if (UIManager.HasInstance && UIManager.Instance != null && UIManager.Instance.notify.rhythmHUD != null)
                UIManager.Instance.notify.rhythmHUD.SetReactionPresenterAnimal(_currentAnimal != null ? _currentAnimal.Definition : null);

            // --- GUIDE FIRST TIME (delay trước khi show) ---
            if (_startFlowRoutine != null) { StopCoroutine(_startFlowRoutine); _startFlowRoutine = null; }

            // Chỉ chạy flow guide nếu "chưa shown" (hoặc editor đang ignore prefs)
            bool shouldTryGuide = (guidePanelFirstTime != null) && GuidePanelManager.HasInstance;

            if (shouldTryGuide)
            {
                _startFlowRoutine = StartCoroutine(ShowGuideAfterDelayThenStart());
                return;
            }



            // Không có guide -> start bình thường


            IsRunning = true;
            SetupPattern(_playlist[_playlistIndex]);
            StartNextBeat();
        }

        private bool TryShowFirstTimeGuide()
        {
            if (guidePanelFirstTime == null) return false;
            if (!GuidePanelManager.HasInstance) return false;

            _activeGuide = GuidePanelManager.Instance.ShowPanelIfNotComplete(
                guideStepId,
                guidePanelFirstTime,
                pauseGameWhenGuideShown,
                true,
                5000,
                ignorePrefsInEditor,
                disableMarkInEditor
            );

            return _activeGuide != null;
        }

        private IEnumerator ShowGuideAfterDelayThenStart()
        {
            yield return new WaitForSeconds(delayBeforeShowGuide);

            TryShowFirstTimeGuide();

            while (_activeGuide != null && _activeGuide.gameObject.activeSelf)
                yield return null;

            _activeGuide = null;

            IsRunning = true;
            SetupPattern(_playlist[_playlistIndex]);
            StartNextBeat();
        }


        private IEnumerator WaitGuideThenStart()
        {
            // 1) Đợi HUD animate xong trước khi show guide
            if (delayBeforeShowGuide > 0f)
                yield return new WaitForSeconds(delayBeforeShowGuide);

            // 2) Lúc này guide đã được show (TryShowFirstTimeGuide đã gọi trước đó)
            // -> chờ user đóng guide
            while (_activeGuide != null && _activeGuide.gameObject.activeSelf)
                yield return null;

            _activeGuide = null;

            // 3) Start minigame thật sự
            IsRunning = true;
            SetupPattern(_playlist[_playlistIndex]);
            StartNextBeat();
        }



        public void StopGame()
        {
            if (!IsRunning) return;
            IsRunning = false;

            KillTarget();
            if (_restCoroutine != null) { StopCoroutine(_restCoroutine); _restCoroutine = null; }

            if (AudioManager.HasInstance)
            {
                AudioManager.Instance.FadeOutBGM();
                AudioManager.Instance.PopBGM(); // Khôi phục PopBGM
            }

            // reaction HUD clear (light hook)
            if (UIManager.HasInstance && UIManager.Instance != null && UIManager.Instance.notify.rhythmHUD != null)
                UIManager.Instance.notify.rhythmHUD.ClearReactionPresenterAnimal();


            if (CameraManager.HasInstance)
                CameraManager.Instance.RestoreMinigameCamera();

            if (ListenManager.HasInstance)
                ListenManager.Instance.RaiseRhythmHUDHide();

            float successRatio = Mathf.Clamp01(_trust01);
            float archiveGained = GrantArchiveReward(successRatio);
            GrantLootReward(successRatio, out FoodItem lootItem, out int lootCount);

            if (ListenManager.HasInstance)
            {
                ListenManager.Instance.RaiseRhythmAnimalResult(
                    new ListenManager.RhythmAnimalResultPayload(_currentAnimal, successRatio, archiveGained, lootItem, lootCount, _totalHit, _totalMiss)
                );
            }

            ApplyFinishMaterialThenFadeOut(finishFadeDuration);

            if (_currentAnimal != null)
            {
                _currentAnimal.MarkMinigamePlayed();
                _currentAnimal.QueueDespawnAfterMinigame(successRatio);
            }

            _currentAnimal = null;
            _currentFocus = null;
        }

        private void StartNextBeat()
        {
            if (!IsRunning) return;

            if (_currentPattern == null || _currentPattern.sequence == null) // Khôi phục dùng sequence
            {
                StopGame();
                return;
            }

            if (_currentStepIndex >= _currentPattern.sequence.Length)
            {
                _playlistIndex++;
                if (_playlistIndex >= _playlist.Count) { StopGame(); return; }
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
                _restCoroutine = StartCoroutine(RestBeatCoroutine(_currentBeatDuration));
            }
            else
            {
                SpawnTargetForActiveStep();
            }
        }

        private IEnumerator RestBeatCoroutine(float duration)
        {
            yield return new WaitForSeconds(duration);
            _restCoroutine = null;
            AdvanceAfterResolve(scored: false);
            StartNextBeat();
        }

        private void SpawnTargetForActiveStep()
        {
            if (targetPrefab == null || spawnArea == null) { AdvanceAfterResolve(scored: false); StartNextBeat(); return; }
            KillTarget();

            _currentTarget = Instantiate(targetPrefab, spawnArea);
            Vector2 areaSize = spawnArea.rect.size;
            _currentTarget.GetComponent<RectTransform>().anchoredPosition = new Vector2(Random.Range(-areaSize.x * 0.5f, areaSize.x * 0.5f), Random.Range(-areaSize.y * 0.5f, areaSize.y * 0.5f));
            _currentTarget.autoDestroyOnResolve = false;

            _currentTarget.Setup(_activeIsHold, _currentBeatDuration, defaultHoldRequiredSeconds, _activeIsHold ? "GIỮ CHUỘT" : "CLICK CHUỘT", OnTargetResolved);
        }

        private void OnTargetResolved(bool hit) => ResolveBeat(hit);

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
                    if (AudioManager.HasInstance) AudioManager.Instance.PlaySEClip(hitSfx, hitSfxVolume); // Khôi phục PlaySEClip
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
                        if (AudioManager.HasInstance) AudioManager.Instance.PlaySEClip(missSfx, missSfxVolume);
                        _totalMiss++;
                        statusMsg = "Miss";
                        statusPositive = false;
                        _trust01 -= trustStep;
                    }
                }
            }

            _trust01 = Mathf.Clamp01(_trust01);
            float progress01 = (_totalBeatsForProgress > 0) ? Mathf.Clamp01((float)(_totalHit + _totalMiss) / _totalBeatsForProgress) : 0f;

            if (ListenManager.HasInstance)
            {
                // Khôi phục đúng thứ tự tham số cũ
                ListenManager.Instance.RaiseRhythmHUDUpdate(
                    new ListenManager.RhythmHUDUpdatePayload(_totalHit, _totalMiss, _trust01, progress01, statusMsg, statusPositive, 0f, _activeBeatIndexSnapshot, _activeStepType.ToString(), _activeIsHold)
                );
            }

            AdvanceAfterResolve(scored: _activeIsScorable);
            if (IsRunning) StartNextBeat();
        }

        private void AdvanceAfterResolve(bool scored)
        {
            if (scored) _globalScorableBeatIndex++;
            _beatsLeftInStep--;
            if (_beatsLeftInStep <= 0) { _currentStepIndex++; PrepareNextStep(); }
        }

        private void KillTarget() { if (_currentTarget != null) { Destroy(_currentTarget.gameObject); _currentTarget = null; } }

        private void BuildPlaylist(List<RhythmPattern> outList)
        {
            outList.Clear();
            AnimalDefinition def = _currentAnimal != null ? _currentAnimal.Definition : null;
            if (def != null)
            {
                if (def.useRandomRhythm) BuildRandomPlaylistForAnimal(def, outList);
                else if (def.playlist != null && def.playlist.Count > 0) outList.AddRange(def.playlist);
                else if (def.patterns != null) outList.AddRange(def.patterns); // Khôi phục patterns mảng
            }
            if (outList.Count == 0) outList.AddRange(fallbackPlaylist);
        }

        private int CountBeatsInPattern(RhythmPattern pattern)
        {
            if (pattern == null || pattern.sequence == null) return 0;
            int total = 0;
            foreach (var s in pattern.sequence) total += (s.beats <= 0 ? 1 : s.beats);
            return total;
        }

        private int CountScorableBeatsInPattern(RhythmPattern pattern)
        {
            if (pattern == null || pattern.sequence == null) return 0;
            int total = 0;
            foreach (var s in pattern.sequence) if (s.type != RhythmPattern.StepType.Rest) total += (s.beats <= 0 ? 1 : s.beats);
            return total;
        }

        private void SetupPattern(RhythmPattern pattern)
        {
            _currentPattern = pattern;
            _currentStepIndex = 0;
            _beatsLeftInStep = 0;
            if (pattern == null || pattern.sequence == null) return;
            PrepareNextStep();
        }

        private void PrepareNextStep()
        {
            if (_currentPattern == null || _currentPattern.sequence == null || _currentStepIndex >= _currentPattern.sequence.Length) return;
            var step = _currentPattern.sequence[_currentStepIndex];
            _beatsLeftInStep = Mathf.Max(1, step.beats);
        }

        private float GrantArchiveReward(float successRatio)
        {
            if (_currentAnimal == null || !ArchiveManager.HasInstance) return 0f;
            var def = _currentAnimal.Definition;

            // Logic cũ: Tính điểm gốc dựa trên phong độ (thắng tuyệt đối hay thắng thường)
            float finalReward = (successRatio >= 0.99f) ? def.archiveReward : (successRatio >= 0.5f ? def.archiveReward * 0.5f : 0f);

            // --- THÊM ĐOẠN NÀY ---
            // Nếu có Buff thức ăn -> Nhân đôi điểm Archive nhận được
            if (_hasFavoriteBuff)
            {
                finalReward *= 2f;
            }
            // ---------------------

            if (finalReward > 0f) ArchiveManager.Instance.AddProgress(finalReward);
            return finalReward;
        }

        private void GrantLootReward(float successRatio, out FoodItem item, out int count)
        {
            item = null; count = 0;
            if (_currentAnimal == null || !InventoryManager.HasInstance || successRatio < 0.5f) return;

            var def = _currentAnimal.Definition;

            // --- SỬA TỪ ĐÂY ---
            // Đổi sang dùng biến MỚI: rewardItem
            if (def.rewardItem == null) return;

            item = def.rewardItem;

            // Logic tính số lượng (random trong khoảng min-max)
            int baseCount = Random.Range(def.rewardMinCount, def.rewardMaxCount + 1);

            // Logic Buff x2 (giữ nguyên logic muốn)
            if (_hasFavoriteBuff && def.doubleLootOnBuff)
            {
                baseCount *= 2;
            }

            count = baseCount;
            // --- KẾT THÚC SỬA ---

            InventoryManager.Instance.AddFood(item, count);
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

        private void ApplyFinishMaterialThenFadeOut(float duration)
        {
            if (_currentAnimal == null) return;
            if (finishFxMaterial == null) return;

            var r = _currentAnimal.GetComponentInChildren<Renderer>(true);
            if (r == null) return;

            // Stop routine cũ nếu đang chạy
            if (_finishFadeRoutine != null)
            {
                StopCoroutine(_finishFadeRoutine);
                _finishFadeRoutine = null;
            }

            _finishFadeRoutine = StartCoroutine(FinishMaterialFadeRoutine(r, duration));
        }

        private IEnumerator FinishMaterialFadeRoutine(Renderer r, float duration)
        {
            if (r == null) yield break;

            // Lưu material gốc (để optional restore)
            var originalShared = r.sharedMaterials;

            // Tạo instance materials riêng cho con này (tránh đổi cả map)
            var mats = r.materials;

            // Replace tất cả slot bằng finishFxMaterial (nếu chỉ muốn slot 0 thì sửa vòng lặp i=0)
            for (int i = 0; i < mats.Length; i++)
                mats[i] = new Material(finishFxMaterial);

            r.materials = mats;

            // Fade alpha của finish mats về 0
            float t = 0f;
            duration = Mathf.Max(0.01f, duration);

            // Determine property fallback
            string prop = fadeColorProperty;
            bool hasProp = mats.Length > 0 && mats[0] != null && mats[0].HasProperty(prop);
            if (!hasProp)
            {
                // fallback phổ biến
                if (mats.Length > 0 && mats[0] != null && mats[0].HasProperty("_Color"))
                    prop = "_Color";
                else
                    prop = ""; // không có property màu để fade
            }

            // Cache start colors
            Color[] startColors = new Color[mats.Length];
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;
                startColors[i] = (!string.IsNullOrEmpty(prop) && mats[i].HasProperty(prop))
                    ? mats[i].GetColor(prop)
                    : Color.white;
            }

            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / duration);
                float a = Mathf.Lerp(1f, 0f, p);

                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null) continue;

                    if (!string.IsNullOrEmpty(prop) && m.HasProperty(prop))
                    {
                        var c = startColors[i];
                        c.a = a;
                        m.SetColor(prop, c);
                    }
                }

                yield return null;
            }

            // đảm bảo alpha = 0
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (m == null) continue;

                if (!string.IsNullOrEmpty(prop) && m.HasProperty(prop))
                {
                    var c = startColors[i];
                    c.a = 0f;
                    m.SetColor(prop, c);
                }
            }

            // Optional restore
            if (restoreOriginalAfterFade)
            {
                r.sharedMaterials = originalShared;
            }

            _finishFadeRoutine = null;
        }


    }
}