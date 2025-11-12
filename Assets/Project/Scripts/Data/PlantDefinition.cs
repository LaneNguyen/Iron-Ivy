using UnityEngine;
using IronIvy.Gameplay.Rhythm;

namespace IronIvy.Data
{
    [CreateAssetMenu(menuName = "IronIvy/Plant Definition")]
    public class PlantDefinition : ScriptableObject
    {
        public string id;
        public string displayName;

        [Header("Rhythm (multi-pattern)")]
        public RhythmPattern[] patterns;   //  nhiều pattern
        public RhythmPlaybackMode playbackMode = RhythmPlaybackMode.Sequential;

        //spawm cùng lúc nhung từng cái sẽ đẩy lên theo beat
        [Header("Stages Prefabs (spawned together)")]
        public GameObject prefabStage1;
        public GameObject prefabStage2;
        public GameObject prefabStage3;

        [Header("Rewards food nhan dc")]
        public FoodItem yieldItem;

        [Header("FX & Audio")]
        public GameObject successVFX;
        public AudioClip musicLoop;
    }
}
