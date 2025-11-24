using UnityEngine;
using System.Collections.Generic;
using IronIvy.Core;
using IronIvy.Data;
using IronIvy.UI;

namespace IronIvy.Gameplay.Rhythm
{
    // Minigame rhythm cho cây
    // dựa trên RhythmMinigameBase v4
    public class PlantRhythmMinigame : RhythmMinigameBase
    {
        [Header("Plant")]
        public PlantDefinition plant;
        public Transform root;

        private GameObject stage1;
        private GameObject stage2;
        private GameObject stage3;

        [Header("HUD")]
        public RhythmHUD hud;

        // lưu tổng beat của playlist cho progress bar
        private int totalBeatsForProgress;

        private bool lastInWindow = false;

        public override void StartGame()
        {
            if (plant == null)
            {
                Debug.LogWarning("Missing PlantDefinition");
                return;
            }

            // spawn các stage của cây
            if (plant.prefabStage1) stage1 = Instantiate(plant.prefabStage1, root);
            if (plant.prefabStage2) stage2 = Instantiate(plant.prefabStage2, root);
            if (plant.prefabStage3) stage3 = Instantiate(plant.prefabStage3, root);

            Lower(stage1);
            Lower(stage2);
            Lower(stage3);

            // camera + bgm cho minigame
            MinigameCameraManager.Instance.ApplyPlantProfile();
            if (plant.musicLoop != null)
                AudioManager.Instance.PlayBGM(plant.musicLoop.name);

            // reset trust
            trust = 0f;
            lastInWindow = false;

            // base sẽ gọi BuildPatternPlaylist rồi PreparePattern
            base.StartGame();

            // tính progress total từ playlist
            totalBeatsForProgress = playlistTotalBeats;

            if (hud == null)
                hud = FindObjectOfType<RhythmHUD>();

            if (hud != null)
            {
                hud.BindMinigame(this);

                // set hint theo bước đầu tiên
                if (pattern != null && pattern.sequence != null && pattern.sequence.Length > 0)
                {
                    var step0 = pattern.sequence[0];
                    string hint = step0.type == RhythmPattern.StepType.Hold ? "HOLD (SPACE)" : "TAP (SPACE)";
                    hud.SetKeyHints(new[] { hint });
                }
                else
                {
                    hud.SetKeyHints(new[] { "SPACE" });
                }

                hud.SetStatus("Ready", false);
                hud.SetTrust01(0f);
                hud.SetProgress(0f);
                hud.SetHitMiss(0, 0);
                hud.SetHoldVisual(0f);

                hud.SetBeatWindow(targetCenter01, targetHalfWidth01);
                hud.SetBeatPhase(0f, false);
            }
        }

        protected override void BuildPatternPlaylist(List<RhythmPattern> outList)
        {
            if (plant == null || plant.patterns == null)
                return;

            foreach (var p in plant.patterns)
                if (p != null) outList.Add(p);

            // xử lý playback mode
            switch (plant.playbackMode)
            {
                case RhythmPlaybackMode.Single:
                    if (outList.Count > 1)
                        outList.RemoveRange(1, outList.Count - 1);
                    break;

                case RhythmPlaybackMode.Shuffle:
                    RhythmManager.Shuffle(outList);
                    break;
                    // Sequential: để nguyên list
            }
        }

        // callbacks cho beat

        protected override void OnBeat()
        {
            lastInWindow = false;

            if (hud != null)
            {
                hud.SetBeatWindow(targetCenter01, targetHalfWidth01);
                hud.SetHoldVisual(0f);
                hud.ClearPulseKey(0);
            }
        }

        protected override void OnBeatProgress(float phase, bool inWindow)
        {
            bool prev = lastInWindow;
            lastInWindow = inWindow;

            if (hud != null)
            {
                hud.SetBeatPhase(phase, inWindow);

                // vào window thì pulse một cái
                if (!prev && inWindow)
                    hud.PulseKey(0);

                // ra khỏi window thì ngưng
                if (prev && !inWindow)
                    hud.ClearPulseKey(0);

                // global progress theo playlist
                if (totalBeatsForProgress > 0)
                {
                    float progress = (playlistBeatIndex + phase) / Mathf.Max(1, totalBeatsForProgress);
                    hud.SetProgress(progress);
                }

                hud.SetHitMiss(beatsHit, beatsMiss);

                // hold visual
                RhythmPattern.Step current = GetCurrentStep();
                if (current.type == RhythmPattern.StepType.Hold)
                {
                    float required = Mathf.Max(0.01f, holdRequiredSeconds);
                    float hold01 = Mathf.Clamp01(holdTimer / required);
                    hud.SetHoldVisual(hold01);
                }
                else
                {
                    hud.SetHoldVisual(0f);
                }
            }
        }

        protected override void OnBeatHit()
        {
            if (hud != null)
                hud.SetStatus("Good", true);
        }

        protected override void OnBeatMissed()
        {
            if (hud != null)
                hud.SetStatus("Miss", false);
        }

        protected override void OnStepJudged(RhythmPattern.Step step, bool good)
        {
            // lấy stage theo index đơn giản
            int idx = currentStepIndex % 3;
            GameObject target =
                idx == 0 ? stage1 :
                idx == 1 ? stage2 :
                           stage3;

            // spawn VFX nếu có
            if (good && plant.successVFX)
                Instantiate(plant.successVFX, root.position, Quaternion.identity);

            Toggle(target, good);

            // tính trust mỗi beat
            trust += good ? 11f : -4f;
            trust = Mathf.Clamp(trust, 0f, 100f);

            if (hud != null)
                hud.SetTrust01(trust / 100f);

            // hint cho step kế
            if (pattern != null && pattern.sequence != null && pattern.sequence.Length > 0)
            {
                RhythmPattern.Step current = GetCurrentStep();
                string hint = current.type == RhythmPattern.StepType.Hold ? "HOLD (SPACE)" : "TAP (SPACE)";
                hud.SetKeyHints(new[] { hint });
            }
        }

        protected override void OnPlaylistComplete()
        {
            bool success = trust >= 50f;

            if (hud != null)
            {
                hud.SetStatus(success ? "Success" : "Fail", success);
                hud.SetHitMiss(beatsHit, beatsMiss);
                hud.SetProgress(1f);
                hud.ClearPulseKey(0);
            }

            // reward theo trust
            int yield = (trust >= 90f) ? 3 :
                        (trust >= 60f) ? 2 :
                        (trust >= 30f) ? 1 : 0;

            if (yield > 0 && plant.yieldItem)
                InventoryManager.Instance.AddFood(plant.yieldItem, yield);
        }

        private void Toggle(GameObject go, bool up)
        {
            if (!go) return;
            go.transform.localPosition = up ? new Vector3(0, 0.2f, 0) : new Vector3(0, -0.2f, 0);
        }

        private void Lower(GameObject go)
        {
            if (!go) return;
            go.transform.localPosition = new Vector3(0, -0.2f, 0);
        }
    }
}
