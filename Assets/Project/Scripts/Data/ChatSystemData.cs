using System;
using System.Collections.Generic;
using UnityEngine;

namespace IronIvy.AIChatSystem.Data
{
    [CreateAssetMenu(fileName = "ChatSystemData", menuName = "IronIvy/AI Chat/Chat System Data")]
    public class ChatSystemData : ScriptableObject
    {
        [Header("Normal Chat Feed (VN-only)")]
        public List<ChatBark> barks = new List<ChatBark>();

        [Header("Pinned Random Tasks (VN-only)")]
        public List<TaskDefinition> tasks = new List<TaskDefinition>();
    }

    [Serializable]
    public class ChatBark
    {
        [Tooltip("ID nội bộ, dùng để debug/trace (không bắt buộc unique tuyệt đối)")]
        public string id;

        [Tooltip("Emoji có thể rỗng nếu muốn text-only")]
        public string emoji;

        [TextArea(2, 4)]
        [Tooltip("Nếu muốn emoji-only, để textVN rỗng")]
        public string textVN;

        [Header("Routing / Constraints")]
        public BarkPriority priority = BarkPriority.Normal;

        [Tooltip("Nếu bật, bark này chỉ bắn khi QuietWindow=false (trừ High/Critical)")]
        public bool suppressInQuietWindow = true;

        [Tooltip("Nếu set, bark chỉ bắn cho những trigger tương ứng")]
        public BarkTrigger trigger = BarkTrigger.Any;
    }

    public enum BarkTrigger
    {
        Any = 0,
        EnergyWarning = 10,
        ArchiveOpen = 20,
        RewardClose = 30,
        MinigameEnd = 40,
        MinigameStreak = 50
    }

    public enum BarkPriority
    {
        Low = 0,
        Normal = 10,
        High = 20,
        Critical = 30
    }

    [Serializable]
    public class TaskDefinition
    {
        [Tooltip("ID task phải unique")]
        public string taskId;

        [Tooltip("Emoji hiển thị trên QuestCard")]
        public string emoji = "📌";

        [TextArea(2, 5)]
        [Tooltip("Text task (VN-only)")]
        public string titleVN;

        [Tooltip("Mô tả ngắn (VN-only). Có thể rỗng.")]
        [TextArea(1, 3)]
        public string descVN;

        [Header("Progress")]
        public int targetCount = 3;

        public TaskProgressType progressType = TaskProgressType.MinigameCount;

        [Tooltip("Nếu true: task sẽ “chọn” animalKey từ lần MinigameEnd đầu tiên và giữ đến khi complete/reset.")]
        public bool lockToFirstAnimal = false;

        [Header("Triggers")]
        public bool triggerOnArchiveOpen = true;
        public bool triggerOnRewardClose = true;

        [Header("Cooldown (seconds)")]
        public float spawnCooldownSeconds = 30f;

        [Header("Priority (khi queue full)")]
        public BarkPriority priority = BarkPriority.Normal;
    }

    public enum TaskProgressType
    {
        MinigameCount = 0,
        MinigameHitTotal = 10,
        MinigamePerfectStreak = 20
    }
}
