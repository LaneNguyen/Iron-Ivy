using UnityEngine;
using System.Collections.Generic;
using IronIvy.Core;
using IronIvy.Data;

namespace IronIvy.Gameplay.Rhythm
{
    public class AnimalRhythmMinigame : RhythmMinigameBase
    {
        [Header("Animal")]
        public AnimalDefinition animal;
        public Animator animalAnimator;
        public Animator iv17Animator;

        public override void StartGame()
        {
            if (!animal) { Debug.LogWarning("[AnimalRhythm] Missing AnimalDefinition."); return; }
            MinigameCameraManager.Instance.ApplyAnimalProfile();
            if (animal.loopSfx != null)
            {
                // Phát loop như BGM trong lúc chơi minigame
                AudioManager.Instance.PlayBGM(animal.loopSfx.name);
            }
            base.StartGame();
        }

        protected override void BuildPatternPlaylist(List<RhythmPattern> outList)
        {
            if (animal?.patterns == null) return;
            foreach (var p in animal.patterns) if (p) outList.Add(p);

            switch (animal.playbackMode)
            {
                case RhythmPlaybackMode.Single:
                    if (outList.Count > 1) outList.RemoveRange(1, outList.Count - 1);
                    break;
                case RhythmPlaybackMode.Shuffle:
                    RhythmManager.Shuffle(outList);
                    break;
                    // Sequential: giữ nguyên
            }
        }

        protected override void OnStepJudged(RhythmPattern.Step step, bool good)
        {
            if (good)
            {
                // Animal anim
                if (animalAnimator && !string.IsNullOrEmpty(animal.goodAnim))
                    animalAnimator.Play(animal.goodAnim, 0, 0);

                // IV-17 reactions: TÊN STATE trong Animator Controller của IV-17
                if (iv17Animator && animal.iv17Reactions != null && animal.iv17Reactions.Length > 0)
                {
                    string pick = animal.iv17Reactions[Random.Range(0, animal.iv17Reactions.Length)];
                    if (!string.IsNullOrEmpty(pick)) iv17Animator.Play(pick, 0, 0);
                }

                trust += 12f;
            }
            else
            {
                if (animalAnimator && !string.IsNullOrEmpty(animal.badAnim))
                    animalAnimator.Play(animal.badAnim, 0, 0);
                trust -= 5f;
            }
            trust = Mathf.Clamp(trust, 0, 100);
        }

        protected override void OnPlaylistComplete()
        {
            // Kết playlist → chấm Trust = 100? → thưởng Archive theo Zone
            if (trust >= 100f)
            {
                ArchiveManager.Instance.AddProgress(ZoneManager.Instance.GetArchiveReward());
                EventBus.Instance.RaiseTrustSuccess();
            }
            StopGame();
        }
    }
}