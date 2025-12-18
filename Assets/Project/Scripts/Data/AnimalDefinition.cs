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
        public Sprite icon;

        // ===== Camera tuning (Option 1) =====
        [Header("Camera (Animal Minigame)")]
        public float cameraOrbitDistance = 0f;
        public float cameraOrbitHeight = 0f;
        public float cameraLookAtHeight = 0f;
        public float cameraOrbitRotateSpeed = 0f;

        [Header("Feeding & Buffs")]
        public FoodItem favoriteFood;
        public int buffSafetyNet = 3;
        public float buffTrustMultiplier = 1.5f;

        [Header("Loot (Drops)")]
        public FoodItem dropItem;
        public int dropCount = 1;
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
        public bool useRandomRhythm = false;
        public RhythmPattern[] randomFragments;
        [Min(1)] public int minRandomBeats = 8;
        [Min(1)] public int maxRandomBeats = 24;
        [Min(1)] public int minRandomFragments = 2;
        [Min(1)] public int maxRandomFragments = 6;

        [Header("Animation names")]
        public string goodAnim = "Good";
        public string badAnim = "Bad";

        [Header("IV-17 Reactions")]
        public string[] iv17Reactions;

        [Header("Archive / Progress")]
        [Range(0f, 100f)] public float archiveReward = 5f;

        [Header("Audio/FX")]
        [Tooltip("BGM riêng khi chơi minigame với con này (giống PlantDefinition.musicLoop). Nếu null thì dùng fallback trong minigame.")]
        public AudioClip minigameMusicLoop;

        [Tooltip("Trước đây là loop sfx, giờ đổi sang ambient one-shot (thêm vào pool ambient).")]
        public AudioClip loopSfx;

        [Tooltip("Nếu trust >= ngưỡng, despawn sẽ dùng SuccessVFX thay vì despawnVfxPrefab.")]
        public GameObject successVFX;
    }
}
