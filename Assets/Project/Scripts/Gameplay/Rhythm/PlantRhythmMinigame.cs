using UnityEngine;
using System.Collections.Generic;
using IronIvy.Core;
using IronIvy.Data;
using IronIvy.UI;

namespace IronIvy.Gameplay.Rhythm
{
    public class PlantRhythmMinigame : RhythmMinigameBase
    {
        [Header("Plant")]
        public PlantDefinition plant;
        public Transform root;

        // 3 stage object
        private GameObject a, b, c;

        [Header("HUD")]
        public RhythmHUD hud;

        // Progress (tính theo beat)
        private int computedTotalBeats;

        public override void StartGame()
        {
            if (plant == null)
            {
                Debug.LogWarning("[PlantRhythm] Missing plant.");
                return;
            }

            // Spawn 3 stages
            if (plant.prefabStage1) a = Instantiate(plant.prefabStage1, root);
            if (plant.prefabStage2) b = Instantiate(plant.prefabStage2, root);
            if (plant.prefabStage3) c = Instantiate(plant.prefabStage3, root);
            Lower(a); Lower(b); Lower(c);

            // Camera profile
            MinigameCameraManager.Instance.ApplyPlantProfile();

            // BGM
            if (plant.musicLoop != null)
                AudioManager.Instance.PlayBGM(plant.musicLoop.name);

            // Reset scoring
            trust = 0f;

            base.StartGame();

            // HUD
            if (hud == null)
                hud = FindObjectOfType<RhythmHUD>();

            if (hud != null)
            {
                hud.BindMinigame(this);

                // Step đầu → hiển thị TAP / HOLD
                if (pattern != null && pattern.sequence != null && pattern.sequence.Length > 0)
                {
                    var step = pattern.sequence[0];
                    string hint = step.type == RhythmPattern.StepType.Hold ? "HOLD (SPACE)" : "TAP (SPACE)";
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
            }

            computedTotalBeats = totalBeats;
        }

        protected override void BuildPatternPlaylist(List<RhythmPattern> list)
        {
            if (plant?.patterns == null) return;

            foreach (var p in plant.patterns)
                if (p) list.Add(p);

            switch (plant.playbackMode)
            {
                case RhythmPlaybackMode.Single:
                    if (list.Count > 1)
                        list.RemoveRange(1, list.Count - 1);
                    break;

                case RhythmPlaybackMode.Shuffle:
                    RhythmManager.Shuffle(list);
                    break;
                    // Sequential: giữ nguyên
            }
        }

        protected override void OnBeat()
        {
            // Pulse key
            if (hud != null)
                hud.PulseKey(0);
        }

        protected override void OnBeatProgress(float phase, bool inWindow)
        {
            if (hud != null)
            {
                hud.SetBeatPhase(phase, inWindow);

                if (computedTotalBeats > 0)
                    hud.SetProgress((float)beatIndex / Mathf.Max(1, computedTotalBeats));

                hud.SetHitMiss(beatsHit, beatsMiss);
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
            // Toggle plant stage
            int i = seqIndex % 3;
            Toggle(i == 0 ? a : i == 1 ? b : c, good);

            if (good && plant.successVFX)
                Instantiate(plant.successVFX, root.position, Quaternion.identity);

            // Trust
            trust += good ? 11f : -4f;
            trust = Mathf.Clamp(trust, 0f, 100f);

            if (hud != null)
                hud.SetTrust01(trust / 100f);

            // Update TAP/HOLD hint theo step tiếp theo nếu có
            if (pattern != null && pattern.sequence != null && seqIndex + 1 < pattern.sequence.Length)
            {
                var nextStep = pattern.sequence[seqIndex + 1];
                string hint = nextStep.type == RhythmPattern.StepType.Hold ? "HOLD (SPACE)" : "TAP (SPACE)";
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
            }

            // Yield item
            int yield = (trust >= 90f) ? 3 :
                        (trust >= 60f) ? 2 :
                        (trust >= 30f) ? 1 : 0;

            if (yield > 0 && plant.yieldItem)
                InventoryManager.Instance.AddFood(plant.yieldItem, yield);
        }

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
    }
}
