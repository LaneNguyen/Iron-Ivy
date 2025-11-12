using UnityEngine;

namespace IronIvy.Core
{
    public class EnergyManager : BaseManager<EnergyManager>
    {
        [SerializeField] int maxEnergy = 6;
        public int Current { get; private set; }

        public void ResetDaily()
        {
            Current = maxEnergy;
            EventBus.Instance.RaiseEnergyChanged(Current);
        }

        public bool TrySpend(int amount)
        {
            if (Current < amount) return false;
            Current -= amount;
            EventBus.Instance.RaiseEnergyChanged(Current);
            return true;
        }
    }
}
