using UnityEngine;

namespace IronIvy.Data
{
    public enum ArchiveRewardType
    {
        None,
        MaxEnergy,
        NewSeed,
        NewPlot,
        ZoneUnlock
    }

    [CreateAssetMenu(menuName = "IronIvy/Archive Node Definition")]
    public class ArchiveNodeDefinition : ScriptableObject
    {
        [Header("Thong tin co ban")]
        public string id;
        public string title;
        [TextArea]
        public string description;

        [Header("Visual")]
        [Tooltip("Icon của node (để show trên UI)")]
        public Sprite icon;

        [Header("Yeu cau mo khoa")]
        public float costToUnlock;
        public ArchiveNodeDefinition requiredParent;

        [Header("Phan thuong")]
        public ArchiveRewardType rewardType;
        public int rewardValue;
        public Object rewardObject;

        [Header("Special Flow")]
        [Tooltip("Nếu bật: khi node này được UNLOCK thành công, sẽ trigger flow ScreenFader -> hide UI -> play ending timeline.")]
        public bool triggerEndingTimelineOnUnlock = false;
    }
}
