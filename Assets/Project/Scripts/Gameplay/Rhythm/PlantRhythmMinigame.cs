using UnityEngine;
using System.Collections.Generic;
using IronIvy.Core;
using IronIvy.Data;

namespace IronIvy.Gameplay.Rhythm
{
    public class PlantRhythmMinigame : RhythmMinigameBase
    {
        [Header("Plant")]
        public PlantDefinition plant;
        public Transform root;
        public Vector3 loweredOffset = new Vector3(0, -0.5f, 0);
        public Vector3 raisedOffset = new Vector3(0, 0.0f, 0);
        private GameObject a, b, c;

        public override void StartGame()
        {
            if (plant == null) { Debug.LogWarning("[PlantRhythm] Missing PlantDefinition."); return; }

            // Spawn 3 stages trước
            if (plant.prefabStage1) a = Instantiate(plant.prefabStage1, root);
            if (plant.prefabStage2) b = Instantiate(plant.prefabStage2, root);
            if (plant.prefabStage3) c = Instantiate(plant.prefabStage3, root);
            Lower(a); Lower(b); Lower(c);

            MinigameCameraManager.Instance.ApplyPlantProfile();

            // AudioManager mới dùng tên clip
            if (plant.musicLoop != null)
            {
                AudioManager.Instance.PlayBGM(plant.musicLoop.name);
            }

            base.StartGame();
        }
        protected override void BuildPatternPlaylist(List<RhythmPattern> outList)
        {
            if (plant?.patterns == null) return;
            foreach (var p in plant.patterns) if (p) outList.Add(p);

            switch (plant.playbackMode)
            {
                case RhythmPlaybackMode.Single:
                    if (outList.Count > 1) outList.RemoveRange(1, outList.Count - 1);
                    break;
                case RhythmPlaybackMode.Shuffle:
                    RhythmManager.Shuffle(outList);
                    break;
                    // Sequential: giữ nguyên thứ tự
            }
        }

        protected override void OnStepJudged(RhythmPattern.Step step, bool good)
        {
            // Step i: mỗi GOOD sẽ raise 1 stage theo vòng 0→1→2
            int i = seqIndex % 3;
            if (i == 0) Toggle(a, good);
            else if (i == 1) Toggle(b, good);
            else Toggle(c, good);

            if (good && plant.successVFX)
                Instantiate(plant.successVFX, root.position, Quaternion.identity);

            // trust ở đây là "điểm chăm cây" (tích luỹ toàn playlist)
            trust += good ? 11f : -4f;
            trust = Mathf.Clamp(trust, 0, 100);
        }

        protected override void OnPlaylistComplete()
        {
            // Điểm → yield 0..3
            int yield = (trust >= 90) ? 3 : (trust >= 60) ? 2 : (trust >= 30) ? 1 : 0;
            if (yield > 0 && plant.yieldItem)
                InventoryManager.Instance.AddFood(plant.yieldItem, yield);

            StopGame();
        }

        void Toggle(GameObject go, bool raise)
        {
            if (!go) return;
            go.transform.localPosition = raise ? raisedOffset : loweredOffset;
        }
        void Lower(GameObject go) { if (!go) return; go.transform.localPosition = loweredOffset; }
    }
}