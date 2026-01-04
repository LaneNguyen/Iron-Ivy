using UnityEngine;

namespace IronIvy.Core
{
    public class EnergyManager : BaseManager<EnergyManager>
    {
        [Header("Config")]
        [SerializeField] private int maxEnergy = 6;
        [SerializeField] private int startingEnergy = 3; // Giá trị năng lượng khi mới bắt đầu game

        // public getter cho UI / logic khác
        public int Current { get; private set; }
        public int MaxEnergy => maxEnergy;

        protected override void Awake()
        {
            base.Awake();
            
            // Khởi tạo giá trị mặc định ngay khi Manager được tạo ra (tại StartScene)
            // Nếu sau đó load file save, giá trị này sẽ bị ghi đè bởi SetLoadedData
            if (Current <= 0)
            {
                Current = startingEnergy;
            }
        }

        // Khởi tạo hệ thống năng lượng khi bắt đầu vào gameplay chính
        public void InitCore()
        {
            // Đảm bảo Current có giá trị hợp lệ nếu Awake chưa xử lý kịp
            if (Current <= 0)
                Current = startingEnergy;

            UpdateUI();
        }

        // Hồi đầy năng lượng (thường dùng khi qua ngày mới)
        public void ResetDaily()
        {
            Current = maxEnergy;
            UpdateUI();
        }

        // Kiểm tra và trừ năng lượng, trả về true nếu đủ năng lượng để thực hiện
        public bool TrySpend(int amount)
        {
            if (Current < amount) return false;

            Current -= amount;
            UpdateUI();
            return true;
        }

        // Hồi toàn bộ năng lượng thông qua Archive Tree hoặc vật phẩm đặc biệt
        public void RestoreFullEnergy()
        {
            Current = maxEnergy;
            UpdateUI();
            Debug.Log("[Energy] Restored to full via Archive Tree.");
        }

        // Nâng cấp giới hạn năng lượng tối đa và hồi đầy
        public void UpgradeMaxEnergy(int amount)
        {
            maxEnergy += amount;
            if (maxEnergy < 1) maxEnergy = 1;

            Current = maxEnergy; 
            UpdateUI();

            Debug.Log($"[Energy] Upgraded Max Energy to {maxEnergy}");
        }

        // Thiết lập dữ liệu từ hệ thống SaveLoad
        public void SetLoadedData(int current, int max)
        {
            maxEnergy = Mathf.Max(1, max);
            Current = Mathf.Clamp(current, 0, maxEnergy);
            UpdateUI();
        }

        // Cập nhật thông tin năng lượng tới các hệ thống lắng nghe (UI)
        private void UpdateUI()
        {
            if (ListenManager.HasInstance)
            {
                ListenManager.Instance.RaiseEnergyChanged(Current);
            }
        }
    }
}