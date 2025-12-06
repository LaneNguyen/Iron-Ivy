using UnityEngine;

namespace IronIvy.Core
{
    public class EnergyManager : BaseManager<EnergyManager>
    {
        [SerializeField] int maxEnergy = 6;
        public int Current { get; private set; }

        protected override void Awake()
        {
            base.Awake(); // Quan trọng: Gọi hàm cha để setup Singleton Instance trước
            Current = maxEnergy; // Set giá trị ngay lập tức khi object sinh ra
        }

        private void Start()
        {
            // Vẫn bắn event ở Start để đảm bảo UI (nếu load chậm) cập nhật được
            if (ListenManager.HasInstance)
            {
                ListenManager.Instance.RaiseEnergyChanged(Current);
            }
        }

        public void ResetDaily()
        {
            Current = maxEnergy;
            // Đảm bảo ListenManager đã sẵn sàng trước khi gọi
            if (ListenManager.HasInstance)
                ListenManager.Instance.RaiseEnergyChanged(Current);
        }

        public bool TrySpend(int amount)
        {
            if (Current < amount) return false;
            Current -= amount;
            
            if (ListenManager.HasInstance)
                ListenManager.Instance.RaiseEnergyChanged(Current);
                
            return true;
        }
    }
}