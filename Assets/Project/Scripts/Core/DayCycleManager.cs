using UnityEngine;

namespace IronIvy.Core
{
    public class DayCycleManager : BaseManager<DayCycleManager>
    {
        public void EndDay()
        {
            EventBus.Instance.RaiseDayEnded();
            EnergyManager.Instance.ResetDaily();
            AnimalManager.Instance.RerollTodayEncounter();
            SaveLoadManager.Instance.SaveAll();
        }
    }
}
