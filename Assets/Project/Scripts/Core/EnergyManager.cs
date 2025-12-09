using UnityEngine;

namespace IronIvy.Core
{
    public class EnergyManager : BaseManager<EnergyManager>
    {
        [Header("Config")]
        [SerializeField] int maxEnergy = 6;

        // public getter cho UI / logic khác
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

        // reset theo ngày
        public void ResetDaily()
        {
            Current = maxEnergy;
            UpdateUI();
        }

        // trừ năng lượng, đủ thì trừ và trả true
        public bool TrySpend(int amount)
        {
            if (Current < amount) return false;

            Current -= amount;
            UpdateUI();
            return true;
        }

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
            if (maxEnergy < 1) maxEnergy = 1;

            Current = maxEnergy; // buff xong hồi đầy luôn
            UpdateUI();

            Debug.Log($"[Energy] Upgraded Max Energy to {maxEnergy}");
        }

        // dùng khi load save từ SaveLoadManager
        public void SetLoadedData(int current, int max)
        {
            maxEnergy = Mathf.Max(1, max);
            Current = Mathf.Clamp(current, 0, maxEnergy);
            UpdateUI();
        }

        // helper để bắn event cho UI
        private void UpdateUI()
        {
            if (ListenManager.HasInstance)
            {
                ListenManager.Instance.RaiseEnergyChanged(Current);
            }
        }
    }
}
