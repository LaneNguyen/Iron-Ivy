using UnityEngine;
using System.Collections.Generic;
using IronIvy.Gameplay.Rhythm;

namespace IronIvy.Data
{
    [CreateAssetMenu(menuName = "IronIvy/Plant Definition")]
    public class PlantDefinition : ScriptableObject
    {
        public string id;
        public string displayName;

        [System.Serializable]
        public class PlantStageData
        {
            public string stageName = "Stage";
            [Tooltip("Visual hiển thị ở giai đoạn này")]
            public GameObject prefab;
            [Tooltip("Các pattern cần chơi để vượt qua giai đoạn này")]
            public RhythmPattern[] patterns;
        }

        [Header("New Stage Logic")]
        [Tooltip("Danh sách các giai đoạn phát triển (VD: Mầm -> Cây non -> Cây lớn)")]
        public List<PlantStageData> stages = new List<PlantStageData>();

        [Header("Rewards")]
        public FoodItem yieldItem;

        [Header("Audio")]
        public AudioClip musicLoop;

        // =========================================================
        // LEGACY FIELDS (Giữ lại để không bị lỗi code cũ, nhưng không dùng cho logic mới)
        // =========================================================
        [HideInInspector] public RhythmPattern[] patterns; 
        [HideInInspector] public GameObject prefabStage1;
        [HideInInspector] public GameObject prefabStage2;
        [HideInInspector] public GameObject prefabStage3;
        [HideInInspector] public RhythmPlaybackMode playbackMode;
    }
}