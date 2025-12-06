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

        [Tooltip("Prefab con thu ngoai world, co gan AnimalController tren do.")]
        public GameObject prefab;

        [Header("Spawn (world animals)")]
        [Tooltip("Tong so con toi da cua loai nay tren toan map (0 hoac <0 = ko gioi han).")]
        public int maxCountGlobal = 10;

        [Tooltip("Weight mac dinh khi spawn, dung lam tham khao neu can.")]
        public float spawnWeight = 1f;

        [Tooltip("FX khi con thu spawn xuat hien.")]
        public GameObject spawnVfxPrefab;

        [Tooltip("FX khi con thu despawn / bien mat.")]
        public GameObject despawnVfxPrefab;

        [Header("Movement")]
        [Tooltip("Toc do di bo chinh.")]
        public float walkSpeed = 1.5f;

        [Tooltip("Toc do chay neu sau nay can (chua dung).")]
        public float runSpeed = 3f;

        [Tooltip("Ban kinh di dao quanh anchor (zone).")]
        public float wanderRadius = 6f;

        [Tooltip("Idle thap nhat giua cac lan wander.")]
        public float minIdleTime = 1f;

        [Tooltip("Idle cao nhat giua cac lan wander.")]
        public float maxIdleTime = 3f;

        [Header("Flags")]
        [Tooltip("De sau nay co day phase thi dung (hien tai chua dung).")]
        public bool isNocturnal = false;

        // ---------------------------------------
        // Ambient audio config (ngoai world)
        // ---------------------------------------

        [Header("Ambient audio (world)")]
        [Tooltip("Tieng keu nho nho cua con nay ngoai world.")]
        public AudioClip[] ambientClips;

        [Tooltip("Min time giua cac lan keu (giay).")]
        public float ambientMinInterval = 5f;

        [Tooltip("Max time giua cac lan keu (giay).")]
        public float ambientMaxInterval = 15f;

        [Tooltip("Chi keu neu player nam trong ban kinh nay (0 hoac <0 = tat).")]
        public float ambientSoundRadius = 15f;

        // ---------------------------------------
        // Curious behaviour config
        // ---------------------------------------

        [Header("Curious behaviour (look at player)")]
        [Tooltip("Ban kinh de con thu co the vao trang thai curious (0 hoac <0 = tat).")]
        public float curiousRadius = 10f;

        [Tooltip("Xac suat trigger curious moi lan check (0-1).")]
        [Range(0f, 1f)] public float curiousChancePerCheck = 0.2f;

        [Tooltip("Thoi gian min dung nhin player (giay).")]
        public float curiousMinDuration = 2f;

        [Tooltip("Thoi gian max dung nhin player (giay).")]
        public float curiousMaxDuration = 4f;

        [Tooltip("Khoang thoi gian giua cac lan check curious (giay).")]
        public float curiousCheckInterval = 5f;

        [Tooltip("Trigger animation neu muon play anim dac biet khi curious (co the de trong).")]
        public string curiousAnimTrigger = "";

        // PHAN DUOI LA HE RHYTHM HIEN CO 

        [Header("Rhythm (multi-pattern)")]
        // nhieu pattern cho minigame giao tiep
        public RhythmPattern[] patterns;
        public RhythmPlaybackMode playbackMode = RhythmPlaybackMode.Sequential;

        [Header("Animation names (Animal)")]
        public string goodAnim = "Good";
        public string badAnim = "Bad";

        [Header("IV-17 Reactions")]
        // ten state trong Animator cua IV-17 se duoc play khi GOOD
        public string[] iv17Reactions;


        [Header("Archive / Progress")]
        [Tooltip("So % archive cong them khi hoan thanh minigame (100% success).")]
        [Range(0f, 100f)] public float archiveReward = 5f;

        [Header("Audio/FX (minigame)")]
        public AudioClip loopSfx;
        public GameObject successVFX;
    }
}
