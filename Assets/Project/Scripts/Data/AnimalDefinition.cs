using UnityEngine;
using IronIvy.Gameplay.Rhythm;

namespace IronIvy.Data
{
    [CreateAssetMenu(menuName = "IronIvy/Animal Definition")]
    public class AnimalDefinition : ScriptableObject
    {
        [Header("Basic")]
        public string id;
        public string displayName;

        [Tooltip("Icon đại diện cho Animal này (dùng trong UI Reward Panel)")]
        public Sprite icon;     // >>> NEW FIELD ADDED <<<

        //   Feeding System & Buffs
        [Header("Feeding & Buffs")]
        [Tooltip("Món ăn yêu thích. Nếu cho ăn trước khi chơi -> Kích hoạt Buff.")]
        public FoodItem favoriteFood;

        [Tooltip("Số mạng bảo hiểm được cộng thêm khi có Buff (Safety Net).")]
        public int buffSafetyNet = 3;

        [Tooltip("Hệ số nhân điểm Trust khi có Buff (Trust Multiplier).")]
        public float buffTrustMultiplier = 1.5f;

        [Header("Loot (Drops)")]
        [Tooltip("Vật phẩm rớt ra khi hoàn thành minigame (Ví dụ: Lông, Sữa...).")]
        public FoodItem dropItem;

        [Tooltip("Số lượng rớt ra mặc định.")]
        public int dropCount = 1;

        [Tooltip("Có nhân đôi Loot khi có Buff không?")]
        public bool doubleLootOnBuff = true;

        [Header("Prefab")]
        public GameObject prefab;

        [Header("Spawn (world animals)")]
        public int maxCountGlobal = 10;
        public float spawnWeight = 1f;
        public GameObject spawnVfxPrefab;
        public GameObject despawnVfxPrefab;

        [Header("Movement")]
        public float walkSpeed = 1.5f;
        public float runSpeed = 3f;
        public float wanderRadius = 6f;
        public float minIdleTime = 1f;
        public float maxIdleTime = 3f;
        public bool isNocturnal = false;

        [Header("Ambient audio")]
        public AudioClip[] ambientClips;
        public float ambientMinInterval = 5f;
        public float ambientMaxInterval = 15f;
        public float ambientSoundRadius = 15f;

        [Header("Curious behaviour")]
        public float curiousRadius = 10f;
        [Range(0f, 1f)] public float curiousChancePerCheck = 0.2f;
        public float curiousMinDuration = 2f;
        public float curiousMaxDuration = 4f;
        public float curiousCheckInterval = 5f;
        public string curiousAnimTrigger = "";

        [Header("Rhythm (multi-pattern)")]
        public RhythmPattern[] patterns;
        public RhythmPlaybackMode playbackMode = RhythmPlaybackMode.Sequential;

        [Header("Rhythm (random mix)")]
        [Tooltip("Bật lên để animal dùng random mix thay vì pattern cố định.")]
        public bool useRandomRhythm = false;

        [Tooltip("Pool các pattern nhỏ đại diện style con này.")]
        public RhythmPattern[] randomFragments;

        [Tooltip("Tổng số beat tối thiểu cho bài random.")]
        [Min(1)] public int minRandomBeats = 8;

        [Tooltip("Tổng số beat tối đa cho bài random.")]
        [Min(1)] public int maxRandomBeats = 24;

        [Tooltip("Số fragment tối thiểu trong playlist random.")]
        [Min(1)] public int minRandomFragments = 2;

        [Tooltip("Số fragment tối đa trong playlist random.")]
        [Min(1)] public int maxRandomFragments = 6;

        [Header("Animation names")]
        public string goodAnim = "Good";
        public string badAnim = "Bad";

        [Header("IV-17 Reactions")]
        public string[] iv17Reactions;

        [Header("Archive / Progress")]
        [Range(0f, 100f)] public float archiveReward = 5f;

        [Header("Audio/FX")]
        public AudioClip loopSfx;
        public GameObject successVFX;
    }
}
