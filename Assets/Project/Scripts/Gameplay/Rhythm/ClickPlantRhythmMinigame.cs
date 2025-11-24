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
    // minigame click kiểu mới cho cây
    // mỗi beat spawn 1 target random
    // TAP: click
    // HOLD: giữ đủ time
    // REST: không spawn gì, chỉ chạy beat time
    public class ClickPlantRhythmMinigame : MonoBehaviour, IMinigame
    {
        [Header("Plant")]
        public PlantDefinition plant;
        public Transform root;

        private GameObject stage1;
        private GameObject stage2;
        private GameObject stage3;

        [Header("UI & HUD")]
        public RhythmHUD hud;
        public RectTransform spawnArea;
        public RhythmClickTarget targetPrefab;

        [Header("Timing")]
        public float fallbackBpm = 80f;
        public float holdRequiredSeconds = 0.4f;
        public Vector2 spawnPadding = new Vector2(50f, 50f);

        [Header("Panels optional")]
        public PlantRhythmRewardPanel rewardPanel;

        // runtime state
        public bool IsRunning { get; private set; }

        private readonly List<RhythmPattern> playlist = new List<RhythmPattern>();
        private int playlistIndex = 0;
        private RhythmPattern currentPattern;
        private int currentStepIndex = 0;
        private int beatsLeftInStep = 0;

        private int totalBeatsForProgress = 0;
        private int globalBeatIndex = 0;

        private float currentBeatDuration = 1f;

        private RhythmClickTarget currentTarget;
        private Coroutine restCoroutine;

        // scoring
        private int beatsHit = 0;
        private int beatsMiss = 0;
        private float trust = 0f;

        [Header("Scoring")]
        [SerializeField] private float missPenaltyRatio = 4f / 11f;

        private int totalScorableBeats = 0;
        private float trustPerHit = 0f;
        private float trustPenaltyPerMiss = 0f;

        private int plantBeatIndex = 0;


        // public API cho IMinigame
        public void StartGame()
        {
            if (IsRunning) return;

            if (plant == null)
            {
                Debug.LogWarning("ClickPlantRhythm missing PlantDefinition");
                return;
            }

            // spawn 3 stage giống minigame cũ
            if (plant.prefabStage1) stage1 = Instantiate(plant.prefabStage1, root);
            if (plant.prefabStage2) stage2 = Instantiate(plant.prefabStage2, root);
            if (plant.prefabStage3) stage3 = Instantiate(plant.prefabStage3, root);

            Lower(stage1);
            Lower(stage2);
            Lower(stage3);

            MinigameCameraManager.Instance.ApplyPlantProfile();
            if (plant.musicLoop != null)
                AudioManager.Instance.PlayBGM(plant.musicLoop.name);

            // reset data
            beatsHit = 0;
            beatsMiss = 0;
            trust = 0f;
            plantBeatIndex = 0;
            globalBeatIndex = 0;
            playlistIndex = 0;
            currentPattern = null;
            currentStepIndex = 0;
            beatsLeftInStep = 0;

            // build playlist
            BuildPatternPlaylist(playlist);

            if (playlist.Count == 0)
            {
                Debug.LogWarning("No pattern in playlist");
                return;
            }

            // tính tổng beat để chạy progress + số beat chấm điểm
            totalBeatsForProgress = 0;
            totalScorableBeats = 0;

            foreach (var p in playlist)
            {
                totalBeatsForProgress += CountBeatsInPattern(p);
                totalScorableBeats += CountScorableBeatsInPattern(p);
            }

            // điểm trust cho mỗi beat đúng
            if (totalScorableBeats > 0)
            {
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

            // bắt đầu ở pattern index 0
            SetupPattern(playlist[0]);

            IsRunning = true;
            EventBus.Instance.RaiseMinigameStarted();

            // spawn beat đầu
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

            if (hud != null && hud.hudRoot != null)
                hud.hudRoot.SetActive(false);

            EventBus.Instance.RaiseMinigameStopped();
        }

        private void OnDisable()
        {
            if (IsRunning)
                StopGame();
        }


        // playlist và pattern
        private void BuildPatternPlaylist(List<RhythmPattern> outList)
        {
            outList.Clear();

            if (plant == null || plant.patterns == null)
                return;

            foreach (var p in plant.patterns)
                if (p != null) outList.Add(p);

            switch (plant.playbackMode)
            {
                case RhythmPlaybackMode.Single:
                    if (outList.Count > 1)
                        outList.RemoveRange(1, outList.Count - 1);
                    break;

                case RhythmPlaybackMode.Shuffle:
                    RhythmManager.Shuffle(outList);
                    break;

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

            if (pattern == null || pattern.sequence == null || pattern.sequence.Length == 0)
            {
                Debug.LogWarning("Pattern missing sequence");
                return;
            }

            float bpm = pattern.bpm > 0 ? pattern.bpm : fallbackBpm;
            currentBeatDuration = 60f / Mathf.Max(1f, bpm);

            currentStepIndex = 0;
            beatsLeftInStep = Mathf.Max(1, pattern.sequence[0].beats);

            UpdateKeyHintForCurrentStep();
        }


        // beat flow
        private void StartNextBeat()
        {
            if (!IsRunning) return;

            // check kết pattern
            while (currentPattern != null &&
                  (currentPattern.sequence == null ||
                   currentStepIndex >= currentPattern.sequence.Length))
            {
                playlistIndex++;
                if (playlistIndex >= playlist.Count)
                {
                    OnPlaylistComplete();
                    return;
                }

                SetupPattern(playlist[playlistIndex]);
            }

            if (currentPattern == null ||
                currentPattern.sequence == null ||
                currentPattern.sequence.Length == 0)
            {
                OnPlaylistComplete();
                return;
            }

            // tổng beat xong hết
            if (globalBeatIndex >= totalBeatsForProgress)
            {
                OnPlaylistComplete();
                return;
            }

            RhythmPattern.Step step = currentPattern.sequence[currentStepIndex];

            // REST beat
            if (step.type == RhythmPattern.StepType.Rest)
            {
                if (restCoroutine != null)
                    StopCoroutine(restCoroutine);

                restCoroutine = StartCoroutine(RestBeatRoutine());
            }
            else
            {
                SpawnTargetForStep(step);
            }

            UpdateKeyHintForCurrentStep();
        }

        private IEnumerator RestBeatRoutine()
        {
            yield return new WaitForSeconds(currentBeatDuration);
            ResolveBeat(null);
        }

        private void SpawnTargetForStep(RhythmPattern.Step step)
        {
            if (targetPrefab == null || spawnArea == null)
            {
                Debug.LogWarning("Missing targetPrefab or spawnArea");
                ResolveBeat(false);
                return;
            }

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

            Vector2 areaSize = spawnArea.rect.size;
            Vector2 half = areaSize * 0.5f;
            Vector2 pad = spawnPadding;

            float x = Random.Range(-half.x + pad.x, half.x - pad.x);
            float y = Random.Range(-half.y + pad.y, half.y - pad.y);

            RhythmClickTarget instance = Instantiate(targetPrefab, spawnArea);
            RectTransform rt = instance.transform as RectTransform;
            if (rt != null)
                rt.anchoredPosition = new Vector2(x, y);

            currentTarget = instance;

            bool isHold = step.type == RhythmPattern.StepType.Hold;
            string label = isHold ? "HOLD" : "CLICK";

            instance.Setup(
                isHold,
                currentBeatDuration,
                holdRequiredSeconds,
                label,
                OnTargetResolved
            );
        }

        private void OnTargetResolved(bool hit)
        {
            ResolveBeat(hit);
        }

        // resolve 1 beat
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

            // scoring
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
            else
            {
                if (hud != null)
                    hud.SetStatus("", false);
            }

            UpdatePlantStageVisual(hit ?? false);

            if (hud != null)
            {
                hud.SetTrust01(trust / 100f);
                hud.SetHitMiss(beatsHit, beatsMiss);

                if (totalBeatsForProgress > 0)
                {
                    float prog = (globalBeatIndex + 1) / Mathf.Max(1f, (float)totalBeatsForProgress);
                    hud.SetProgress(prog);
                }

                hud.SetHoldVisual(0f);
            }

            AdvanceStepAfterBeat();
            globalBeatIndex++;

            StartNextBeat();
        }

        private void AdvanceStepAfterBeat()
        {
            if (currentPattern == null ||
                currentPattern.sequence == null ||
                currentPattern.sequence.Length == 0)
                return;

            beatsLeftInStep--;
            if (beatsLeftInStep > 0)
                return;

            currentStepIndex++;
            if (currentStepIndex < currentPattern.sequence.Length)
            {
                beatsLeftInStep = Mathf.Max(1, currentPattern.sequence[currentStepIndex].beats);
            }
        }


        // plant stage visual
        private void UpdatePlantStageVisual(bool good)
        {
            int index = plantBeatIndex % 3;
            plantBeatIndex++;

            GameObject target =
                index == 0 ? stage1 :
                index == 1 ? stage2 :
                             stage3;

            if (good && plant.successVFX)
                Instantiate(plant.successVFX, root.position, Quaternion.identity);

            Toggle(target, good);
        }

        private void OnPlaylistComplete()
        {
            IsRunning = false;

            bool success = trust >= 50f;

            if (hud != null)
            {
                hud.SetStatus(success ? "Success" : "Fail", success);
                hud.SetHitMiss(beatsHit, beatsMiss);
                hud.SetProgress(1f);
                hud.ClearPulseKey(0);
            }

            int yield = (trust >= 90f) ? 3 :
                        (trust >= 60f) ? 2 :
                        (trust >= 30f) ? 1 : 0;

            if (yield > 0 && plant.yieldItem)
            {
                InventoryManager.Instance.AddFood(plant.yieldItem, yield);
            }

            if (rewardPanel != null)
            {
                string itemName = plant != null && plant.yieldItem != null
                    ? plant.yieldItem.name
                    : null;

                rewardPanel.Show(plant, beatsHit, beatsMiss, trust, yield, itemName, null);
            }

            EventBus.Instance.RaiseMinigameStopped();
        }


        // helper
        private void Toggle(GameObject go, bool up)
        {
            if (!go) return;
            go.transform.localPosition = up
                ? new Vector3(0, 0.2f, 0)
                : new Vector3(0, -0.2f, 0);
        }

        private void Lower(GameObject go)
        {
            if (!go) return;
            go.transform.localPosition = new Vector3(0, -0.2f, 0);
        }

        private void UpdateKeyHintForCurrentStep()
        {
            if (hud == null || currentPattern == null ||
                currentPattern.sequence == null ||
                currentPattern.sequence.Length == 0)
                return;

            if (currentStepIndex < 0 ||
                currentStepIndex >= currentPattern.sequence.Length)
                return;

            RhythmPattern.Step step = currentPattern.sequence[currentStepIndex];

            string hint;
            if (step.type == RhythmPattern.StepType.Hold)
                hint = "HOLD (LMB)";
            else if (step.type == RhythmPattern.StepType.Tap)
                hint = "CLICK (LMB)";
            else
                hint = "REST";

            hud.SetKeyHints(new[] { hint });
        }
    }
}
