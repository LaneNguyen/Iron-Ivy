using UnityEngine;
using System.Collections.Generic;
using IronIvy.Gameplay.Rhythm;

namespace IronIvy.Data
{
    [System.Serializable]
    public struct AnimalReactionVisualSet
    {
        [Header("Sprites")]
        public Sprite neutral;
        public Sprite sad;
        public Sprite angry;
        public Sprite happy;

        [Header("Tuning")]
        [Min(1)] public int happyStreakThreshold;
        [Min(0f)] public float missReactionSeconds;
        [Min(0f)] public float happyHoldSeconds;
        [Min(0f)] public float streakDecaySeconds;

        [Tooltip("Nếu bật, Miss sẽ luân phiên Sad/Angry để tạo cảm giác 'cà khịa' nhẹ.")]
        public bool alternateSadAngry;
    }

    [CreateAssetMenu(menuName = "IronIvy/Animal Definition")]
    public class AnimalDefinition : ScriptableObject
    {
        [Header("Basic")]
        public string id;
        public string displayName;
        [Tooltip("Icon đại diện cho Animal này (dùng trong UI Reward Panel)")]
        public Sprite icon;

        // ===== Camera tuning =====
        [Header("Camera (Legacy - Old System)")]
        public float cameraOrbitDistance = 0f;
        public float cameraOrbitHeight = 0f;
        public float cameraLookAtHeight = 0f;
        public float cameraOrbitRotateSpeed = 0f;

        [Header("Camera (New Orbit System)")]
        [Tooltip("Khoảng cách camera tới pivot (default: 2.8)")]
        public float animalOrbitDistance = 2.8f;
        [Tooltip("Góc pitch (nhìn xuống) (default: 25)")]
        public float animalOrbitPitch = 25f;
        [Tooltip("Yaw offset (xoay ngang) (default: 30)")]
        public float animalOrbitYaw = 30f;
        [Tooltip("Pivot height (nhích lên cao để nhìn vào đầu thay vì chân) (default: 1.2)")]
        public float animalOrbitHeight = 1.2f;
        [Tooltip("Thời gian blend camera (default: 0.35)")]
        public float cameraBlendSeconds = 0.35f;

        // ===== Feeding & Buffs =====
        [Header("Feeding & Buffs (Legacy)")]
        public FoodItem favoriteFood;
        public float buffTrustMultiplier = 1.5f;

        [Header("Feeding & Buffs (New)")]
        [Tooltip("Nếu true, con này có favorite buff (Shield) khi start.")]
        public bool hasFavoriteBuff = false;
        public int buffSafetyNet = 3;

        // ===== Loot & Rewards =====
        [Header("Loot (Legacy)")]
        public FoodItem dropItem;
        public int dropCount = 1;
        public bool doubleLootOnBuff = true;

        [Header("Rewards (New)")]
        [Tooltip("Loot item drop khi success (optional).")]
        public FoodItem rewardItem;
        [Tooltip("Số lượng reward item (min).")]
        public int rewardMinCount = 1;
        [Tooltip("Số lượng reward item (max).")]
        public int rewardMaxCount = 1;

        // ===== World & Spawning (Legacy) =====
        [Header("Prefab & Spawning")]
        public GameObject prefab;
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

        [Header("Curious behaviour")]
        public float curiousRadius = 10f;
        [Range(0f, 1f)] public float curiousChancePerCheck = 0.2f;
        public float curiousMinDuration = 2f;
        public float curiousMaxDuration = 4f;
        public float curiousCheckInterval = 5f;
        public string curiousAnimTrigger = "";

        // ===== Rhythm System =====
        [Header("Rhythm (Legacy Patterns)")]
        public RhythmPattern[] patterns;
        public RhythmPlaybackMode playbackMode = RhythmPlaybackMode.Sequential;
        
        [Header("Rhythm (Random Mix Legacy)")]
        public bool useRandomRhythm = false;
        public RhythmPattern[] randomFragments;
        [Min(1)] public int minRandomBeats = 8;
        [Min(1)] public int maxRandomBeats = 24;
        [Min(1)] public int minRandomFragments = 2;
        [Min(1)] public int maxRandomFragments = 6;

        [Header("Rhythm (New Playlist)")]
        public List<RhythmPattern> playlist = new List<RhythmPattern>();

        // ===== Visuals & Reactions =====
        [Header("Animation (Legacy)")]
        public string goodAnim = "Good";
        public string badAnim = "Bad";
        public string[] iv17Reactions;

        [Header("Rhythm Reaction (New Visual Set)")]
        public AnimalReactionVisualSet reactionVisuals;

        // ===== Progress & Audio =====
        [Header("Archive / Progress")]
        [Range(0f, 100f)] public float archiveReward = 5f;

        [Header("Audio/FX")]
        [Tooltip("BGM riêng khi chơi minigame với con này. Nếu null thì dùng fallback.")]
        public AudioClip minigameMusicLoop;
        [Tooltip("Ambient one-shot (thêm vào pool ambient).")]
        public AudioClip loopSfx;
        [Tooltip("Nếu trust >= ngưỡng, despawn sẽ dùng SuccessVFX thay vì despawnVfxPrefab.")]
        public GameObject successVFX;

        [Header("Ambient Audio (Legacy)")]
        public AudioClip[] ambientClips;
        public float ambientMinInterval = 5f;
        public float ambientMaxInterval = 15f;
        public float ambientSoundRadius = 15f;
    }
}