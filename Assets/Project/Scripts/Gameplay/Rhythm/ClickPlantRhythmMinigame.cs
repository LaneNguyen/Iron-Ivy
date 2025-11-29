using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using IronIvy.Core;
using IronIvy.Data;
using IronIvy.UI;
using IronIvy.Interfaces;

namespace IronIvy.Gameplay.Rhythm
{
    /// <summary>
    /// Plant rhythm minigame kiểu mới:
    /// - Mỗi beat = 1 target UI spawn random trên màn hình
    /// - TAP: click chuột trái vào target trước khi hết thời gian
    /// - HOLD: giữ chuột trái đủ holdRequiredSeconds
    /// - REST: không spawn target, chỉ trôi beat
    /// </summary>
    public class ClickPlantRhythmMinigame : MonoBehaviour, IMinigame
    {
        [Header("Plant")]
        public PlantDefinition plant;
        public Transform root;

        private GameObject stage1;
        private GameObject stage2;
        private GameObject stage3;

        [Header("Stage Cleanup")]
        [Tooltip("Thời gian cây ở lại sau khi minigame kết thúc trước khi biến mất.")]
        [SerializeField] private float stageStayDurationAfterEnd = 1.5f;
        [Tooltip("Prefab hiệu ứng biến mất khi cây rời đi (optional).")]
        [SerializeField] private GameObject disappearVfxPrefab;

        [Header("UI & HUD")]
        public RhythmHUD hud;
        public RectTransform spawnArea;
        public RhythmClickTarget targetPrefab;

        [Header("Beat config")]
        [Tooltip("BPM mặc định cho toàn bộ playlist (nếu pattern.bpm <= 0).")]
        public float bpm = 90f;
        [Tooltip("Thời gian phải giữ cho 1 HOLD (giây).")]
        public float defaultHoldRequiredSeconds = 0.4f;
        [Tooltip("Padding để target không spawn sát mép vùng spawn.")]
        public Vector2 spawnPadding = new Vector2(50f, 50f);

        [Header("Flow Panels (optional)")]
        public PlantRhythmRewardPanel rewardPanel;

        // playlist runtime
        private readonly List<RhythmPattern> playlist = new List<RhythmPattern>();
        private int playlistIndex = 0;

        private RhythmPattern currentPattern;
        private int currentStepIndex = 0;
        private int beatsLeftInStep = 0;
        private int globalBeatIndex = 0;
        private int totalBeatsForProgress = 0;

        private float currentBeatDuration = 1f;

        private RhythmClickTarget currentTarget;
        private Coroutine restCoroutine;

        // scoring
        private int beatsHit = 0;
        private int beatsMiss = 0;
        private float trust = 0f;

        [Header("Scoring")]
        [Tooltip("Tỷ lệ phạt so với 1 hit (giống hệ cũ: 4/11).")]
        [SerializeField] private float missPenaltyRatio = 4f / 11f;

        private int totalScorableBeats = 0;      // chỉ Tap/Hold
        private float trustPerHit = 0f;
        private float trustPenaltyPerMiss = 0f;

        // dùng cho anim 3 stage cây
        private int plantBeatIndex = 0;

        public bool IsRunning { get; private set; }

        //=====================================================
        //  Public API IMinigame
        //=====================================================

        public void StartGame()
        {
            if (IsRunning) return;

            if (plant == null)
            {
                Debug.LogWarning("[ClickPlantRhythm] Missing PlantDefinition.");
                return;
            }

            // Spawn các stage của cây (giống minigame cũ)
            if (plant.prefabStage1) stage1 = Instantiate(plant.prefabStage1, root);
            if (plant.prefabStage2) stage2 = Instantiate(plant.prefabStage2, root);
            if (plant.prefabStage3) stage3 = Instantiate(plant.prefabStage3, root);

            Lower(stage1);
            Lower(stage2);
            Lower(stage3);

            // Camera & BGM
            // note: MinigameCameraManager & AudioManager đã có sẵn trong project
            MinigameCameraManager.Instance.ApplyPlantProfile();

            if (plant.musicLoop != null)
            {
                // tên clip dùng để tra trong AudioManager
                AudioManager.Instance.PlayBGM(plant.musicLoop.name);
            }

            // reset state
            beatsHit = 0;
            beatsMiss = 0;
            trust = 0f;
            plantBeatIndex = 0;
            globalBeatIndex = 0;
            playlistIndex = 0;
            currentPattern = null;
            currentStepIndex = 0;
            beatsLeftInStep = 0;
            currentBeatDuration = 60f / Mathf.Max(1f, bpm);
            totalBeatsForProgress = 0;
            totalScorableBeats = 0;
            trustPerHit = 0f;
            trustPenaltyPerMiss = 0f;

            // build playlist từ PlantDefinition
            BuildPatternPlaylist(playlist);

            if (playlist.Count == 0)
            {
                Debug.LogWarning("[ClickPlantRhythm] No pattern in playlist.");
                return;
            }

            // tính tổng beat cho progress + tổng beat chấm điểm
            foreach (var p in playlist)
            {
                totalBeatsForProgress += CountBeatsInPattern(p);
                totalScorableBeats += CountScorableBeatsInPattern(p);
            }

            if (totalScorableBeats > 0)
            {
                // Perfect => trust sẽ lên 100
                trustPerHit = 100f / totalScorableBeats;
                trustPenaltyPerMiss = trustPerHit * missPenaltyRatio;
            }

            // HUD
            if (hud == null)
                hud = FindObjectOfType<RhythmHUD>();

            if (hud != null)
            {
                if (hud.hudRoot != null)
                    hud.hudRoot.SetActive(true);

                if (hud.titleText != null)
                    hud.titleText.text = plant != null ? plant.name : "Plant Rhythm";

                hud.SetStatus("Ready", false);
                hud.SetTrust01(0f);
                hud.SetProgress(0f);
                hud.SetHitMiss(0, 0);
                hud.SetHoldVisual(0f);
            }

            // bắt đầu từ pattern đầu tiên
            playlistIndex = 0;
            SetupPattern(playlist[playlistIndex]);

            IsRunning = true;
            // báo cho EventBus biết minigame start để hệ khác còn nghe
            IronIvy.Core.EventBus.Instance.RaiseMinigameStarted();

            StartNextBeat();
        }

        public void StopGame()
        {
            if (!IsRunning) return;

            IsRunning = false;

            if (currentTarget != null)
            {
                Destroy(currentTarget.gameObject);
                currentTarget = null;
            }

            if (restCoroutine != null)
            {
                StopCoroutine(restCoroutine);
                restCoroutine = null;
            }

            // tắt nhạc nếu đang fade hoặc đang play
            AudioManager.Instance.FadeOutBGM();

            IronIvy.Core.EventBus.Instance.RaiseMinigameStopped();
        }

        //=====================================================
        //  Playlist & pattern
        //=====================================================

        private void BuildPatternPlaylist(List<RhythmPattern> outList)
        {
            outList.Clear();

            if (plant == null || plant.patterns == null)
                return;

            foreach (var p in plant.patterns)
            {
                if (p != null)
                    outList.Add(p);
            }

            switch (plant.playbackMode)
            {
                case RhythmPlaybackMode.Single:
                    if (outList.Count > 1)
                        outList.RemoveRange(1, outList.Count - 1);
                    break;

                case RhythmPlaybackMode.Shuffle:
                    RhythmManager.Shuffle(outList);
                    break;

                // Sequential: giữ nguyên thứ tự list
                case RhythmPlaybackMode.Sequential:
                default:
                    break;
            }
        }

        private int CountBeatsInPattern(RhythmPattern p)
        {
            if (p == null || p.sequence == null) return 0;

            int total = 0;
            foreach (var st in p.sequence)
                total += Mathf.Max(1, st.beats);

            return total;
        }

        private int CountScorableBeatsInPattern(RhythmPattern p)
        {
            if (p == null || p.sequence == null) return 0;

            int total = 0;
            foreach (var st in p.sequence)
            {
                if (st.type == RhythmPattern.StepType.Tap ||
                    st.type == RhythmPattern.StepType.Hold)
                {
                    total += Mathf.Max(1, st.beats);
                }
            }
            return total;
        }

        private void SetupPattern(RhythmPattern pattern)
        {
            currentPattern = pattern;
            currentStepIndex = 0;

            if (pattern == null || pattern.sequence == null || pattern.sequence.Length == 0)
            {
                Debug.LogWarning("[ClickPlantRhythm] Pattern null or empty.");
                beatsLeftInStep = 0;
                return;
            }

            beatsLeftInStep = Mathf.Max(1, pattern.sequence[0].beats);

            // ưu tiên bpm trong pattern
            int bpmToUse = pattern.bpm > 0 ? pattern.bpm : Mathf.RoundToInt(bpm);
            currentBeatDuration = 60f / Mathf.Max(1, bpmToUse);
        }

        //=====================================================
        //  Beat flow
        //=====================================================

        private void StartNextBeat()
        {
            if (!IsRunning) return;

            if (currentPattern == null || currentPattern.sequence == null || currentPattern.sequence.Length == 0)
            {
                NextPatternOrEnd();
                return;
            }

            if (currentStepIndex >= currentPattern.sequence.Length)
            {
                NextPatternOrEnd();
                return;
            }

            var step = currentPattern.sequence[currentStepIndex];

            if (step.type == RhythmPattern.StepType.Rest)
            {
                if (restCoroutine != null)
                    StopCoroutine(restCoroutine);

                restCoroutine = StartCoroutine(RestBeatRoutine(currentBeatDuration));
            }
            else
            {
                SpawnTargetForStep(step);
            }

            UpdateKeyHintForCurrentStep();
        }

        private IEnumerator RestBeatRoutine(float duration)
        {
            // beat nghỉ, không chấm điểm gì
            yield return new WaitForSeconds(duration);
            restCoroutine = null;

            ResolveBeat(null);
        }

        private void SpawnTargetForStep(RhythmPattern.Step step)
        {
            if (spawnArea == null || targetPrefab == null)
            {
                Debug.LogWarning("[ClickPlantRhythm] Missing spawnArea or targetPrefab.");
                return;
            }

            if (currentTarget != null)
            {
                Destroy(currentTarget.gameObject);
                currentTarget = null;
            }

            RhythmClickTarget target = Instantiate(targetPrefab, spawnArea);
            RectTransform rt = target.GetComponent<RectTransform>();

            // random vị trí trong spawnArea
            Vector2 areaSize = spawnArea.rect.size;
            Vector2 min = spawnPadding;
            Vector2 max = areaSize - spawnPadding;

            float x = Random.Range(min.x, max.x) - areaSize.x * 0.5f;
            float y = Random.Range(min.y, max.y) - areaSize.y * 0.5f;

            rt.anchoredPosition = new Vector2(x, y);

            bool isHold = step.type == RhythmPattern.StepType.Hold;
            float beatDur = currentBeatDuration;
            // hiện tại Step chưa có field overrideHoldSeconds, nên dùng default
            float holdSec = defaultHoldRequiredSeconds;

            string label = isHold ? "HOLD" : "CLICK";

            currentTarget = target;
            target.Setup(isHold, beatDur, holdSec, label, OnTargetResolved);
        }

        private void OnTargetResolved(bool hit)
        {
            ResolveBeat(hit);
        }

        private void ResolveBeat(bool? hit)
        {
            if (!IsRunning) return;

            if (currentTarget != null)
            {
                Destroy(currentTarget.gameObject);
                currentTarget = null;
            }

            if (restCoroutine != null)
            {
                StopCoroutine(restCoroutine);
                restCoroutine = null;
            }

            // chấm điểm
            if (hit.HasValue)
            {
                if (hit.Value)
                {
                    beatsHit++;
                    trust += trustPerHit;

                    if (hud != null)
                        hud.SetStatus("Good", true);
                }
                else
                {
                    beatsMiss++;
                    trust -= trustPenaltyPerMiss;

                    if (hud != null)
                        hud.SetStatus("Miss", false);
                }

                trust = Mathf.Clamp(trust, 0f, 100f);
            }

            // update anim cây + HUD
            UpdatePlantStageVisual(hit ?? false);

            globalBeatIndex++;
            float progress01 = (totalBeatsForProgress > 0)
                ? (float)globalBeatIndex / totalBeatsForProgress
                : 1f;

            if (hud != null)
            {
                hud.SetTrust01(trust / 100f);
                hud.SetHitMiss(beatsHit, beatsMiss);
                hud.SetProgress(progress01);
            }

            // next step / pattern
            if (currentPattern != null && currentPattern.sequence != null && currentPattern.sequence.Length > 0)
            {
                beatsLeftInStep--;
                if (beatsLeftInStep <= 0)
                {
                    currentStepIndex++;
                    if (currentStepIndex < currentPattern.sequence.Length)
                    {
                        beatsLeftInStep = Mathf.Max(1, currentPattern.sequence[currentStepIndex].beats);
                    }
                }
            }

            if (currentPattern == null ||
                currentPattern.sequence == null ||
                currentStepIndex >= currentPattern.sequence.Length)
            {
                NextPatternOrEnd();
            }
            else
            {
                StartNextBeat();
            }
        }

        private void NextPatternOrEnd()
        {
            playlistIndex++;
            if (playlistIndex >= playlist.Count)
            {
                OnPlaylistComplete();
            }
            else
            {
                SetupPattern(playlist[playlistIndex]);
                StartNextBeat();
            }
        }

        //=====================================================
        //  Plant stage visual
        //=====================================================

        private void UpdatePlantStageVisual(bool good)
        {
            plantBeatIndex++;

            int index = 0;
            if (totalBeatsForProgress > 0)
            {
                float t = (float)plantBeatIndex / totalBeatsForProgress;
                if (t > 0.66f) index = 2;
                else if (t > 0.33f) index = 1;
                else index = 0;
            }

            GameObject target =
                (index == 0) ? stage1 :
                (index == 1) ? stage2 :
                               stage3;

            if (good && plant != null && plant.successVFX != null)
                Instantiate(plant.successVFX, root.position, Quaternion.identity);

            Toggle(target, good);
        }

        private void OnPlaylistComplete()
        {
            IsRunning = false;

            // nếu perfect (không miss beat Tap/Hold nào) thì snap luôn trust = 100
            if (beatsMiss == 0 && totalScorableBeats > 0 && beatsHit >= totalScorableBeats)
                trust = 100f;

            bool success = trust >= 50f;

            if (hud != null)
            {
                hud.SetStatus(success ? "Success" : "Fail", success);
                hud.SetHitMiss(beatsHit, beatsMiss);
                hud.SetProgress(1f);
                hud.ClearPulseKey(0);
            }

            // Reward theo trust
            int yield = (trust >= 90f) ? 3 :
                        (trust >= 60f) ? 2 :
                        (trust >= 30f) ? 1 : 0;

            if (yield > 0 && plant != null && plant.yieldItem != null)
            {
                InventoryManager.Instance.AddFood(plant.yieldItem, yield);
            }

            // hiển thị reward panel nếu có
            if (rewardPanel != null)
            {
                string itemName = (plant != null && plant.yieldItem != null)
                    ? plant.yieldItem.name
                    : null;

                rewardPanel.Show(plant, beatsHit, beatsMiss, trust, yield, itemName, null);
            }

            // tắt nhạc nền minigame nếu có
            AudioManager.Instance.FadeOutBGM();

            // báo hệ thống minigame stop
            IronIvy.Core.EventBus.Instance.RaiseMinigameStopped();

            // xử lý cho cây ở lại 1 lúc rồi biến mất
            StartCoroutine(CleanupPlantStages());
        }

        //=====================================================
        //  Helpers stage transform
        //=====================================================

        private IEnumerator CleanupPlantStages()
        {
            // ham nay cho 3 stage dung lai 1 khoang thoi gian roi bien mat
            if (stage1 == null && stage2 == null && stage3 == null)
                yield break;

            if (stageStayDurationAfterEnd > 0f)
                yield return new WaitForSeconds(stageStayDurationAfterEnd);

            DestroyStageWithVfx(stage1);
            DestroyStageWithVfx(stage2);
            DestroyStageWithVfx(stage3);

            stage1 = null;
            stage2 = null;
            stage3 = null;
        }

        private void DestroyStageWithVfx(GameObject go)
        {
            if (!go) return;

            if (disappearVfxPrefab != null)
            {
                var vfx = Instantiate(disappearVfxPrefab, go.transform.position, go.transform.rotation);
                Destroy(vfx, 3f);
            }

            Destroy(go);
        }

        private void Toggle(GameObject go, bool up)
        {
            if (!go) return;
            go.transform.localPosition = up
                ? new Vector3(0, 0, 0)
                : new Vector3(0, -0.2f, 0);
        }

        private void Raise(GameObject go)
        {
            if (!go) return;
            go.transform.localPosition = Vector3.zero;
        }

        private void Lower(GameObject go)
        {
            if (!go) return;
            go.transform.localPosition = new Vector3(0, -0.2f, 0);
        }

        private void UpdateKeyHintForCurrentStep()
        {
            if (hud == null || currentPattern == null || currentPattern.sequence == null || currentPattern.sequence.Length == 0)
                return;

            if (currentStepIndex >= currentPattern.sequence.Length)
                return;

            var step = currentPattern.sequence[currentStepIndex];

            string hint;
            switch (step.type)
            {
                case RhythmPattern.StepType.Hold:
                    hint = "HOLD (LMB)";
                    break;
                case RhythmPattern.StepType.Tap:
                    hint = "CLICK (LMB)";
                    break;
                case RhythmPattern.StepType.Rest:
                default:
                    hint = "REST";
                    break;
            }

            hud.SetKeyHints(new[] { hint });
        }
    }
}
