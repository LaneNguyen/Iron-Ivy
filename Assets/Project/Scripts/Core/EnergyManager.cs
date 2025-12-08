using UnityEngine;

namespace IronIvy.Core
{
    public class EnergyManager : BaseManager<EnergyManager>
    {
        [Header("Config")]
        [SerializeField] int maxEnergy = 6;
        
        // Public getter for UI/Logic
        public int Current { get; private set; }
        public int MaxEnergy => maxEnergy;

        protected override void Awake()
        {
            base.Awake(); 
            Current = maxEnergy;
        }

        private void Start()
        {
            UpdateUI();
        }

        public void ResetDaily()
        {
            Current = maxEnergy;
            UpdateUI();
        }

        public bool TrySpend(int amount)
        {
            if (Current < amount) return false;
            Current -= amount;
            UpdateUI();
            return true;
        }

        // NEW API FOR ARCHIVE SYSTEM
        // Gọi bởi ArchiveTree khi người chơi nghỉ ngơi
        public void RestoreFullEnergy()
        {
            Current = maxEnergy;
            UpdateUI();
            Debug.Log("[Energy] Restored to full via Archive Tree.");
        }

        // Gọi bởi ArchiveManager khi mở khóa node tăng năng lượng
        public void UpgradeMaxEnergy(int amount)
        {
            maxEnergy += amount;
            Current = maxEnergy; // Hồi đầy luôn như một phần thưởng
            UpdateUI();
            Debug.Log($"[Energy] Upgraded Max Energy to {maxEnergy}");
        }

        // Helper để update UI đỡ phải viết lặp lại
        private void UpdateUI()
        {
            if (ListenManager.HasInstance)
            {
                ListenManager.Instance.RaiseEnergyChanged(Current);
            }
        }
    }
}