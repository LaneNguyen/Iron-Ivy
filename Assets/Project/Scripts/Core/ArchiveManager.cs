using UnityEngine;

namespace IronIvy.Core
{
    public class ArchiveManager : BaseManager<ArchiveManager>
    {
        [Header("Settings")]
        [Tooltip("Lưu giá trị từ 0 đến 100")]
        [Range(0, 100)]
        public float CurrentPercent;

        protected override void Awake()
        {
            base.Awake();

        }

        private void Start()
        {
            // Khi vào game, bắn ngay event để UI cập nhật theo giá trị trong Inspector
            if (ListenManager.HasInstance)
            {
                ListenManager.Instance.RaiseArchiveChanged(GetPercent());
            }
        }
        // Helper cho các script khác lấy giá trị 0..1
        public float GetPercent()
        {
            return CurrentPercent / 100f;
        }

        public void AddProgress(float delta)
        {
            // delta là số điểm cộng thêm (ví dụ: 5 điểm)
            float before = CurrentPercent;
            CurrentPercent = Mathf.Clamp(CurrentPercent + delta, 0f, 100f);

            Debug.Log($"[ArchiveManager] Added {delta}. New Percent: {CurrentPercent}%");

            // Bắn event cho UI (MainGameUIPanel cần giá trị 0..1 để hiển thị Slider)
            if (ListenManager.HasInstance)
            {
                ListenManager.Instance.RaiseArchiveChanged(CurrentPercent / 100f);
            }

            // Logic mở khóa zone (nếu có)
            if (before < 75f && CurrentPercent >= 75f)
            {
                // if (ListenManager.HasInstance) ListenManager.Instance.RaiseZoneUnlocked();
            }
        }

        // Reset dữ liệu (dùng khi test)
        [ContextMenu("Reset Progress")]
        public void ResetProgress()
        {
            CurrentPercent = 0f;
            if (ListenManager.HasInstance)
                ListenManager.Instance.RaiseArchiveChanged(0f);
        }
    }
}