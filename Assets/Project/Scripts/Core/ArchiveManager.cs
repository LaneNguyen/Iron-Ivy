using UnityEngine;

namespace IronIvy.Core
{
    public class ArchiveManager : BaseManager<ArchiveManager>
    {
        [Range(0,100)] public float CurrentPercent;

        public void AddProgress(float delta)
        {
            var before = CurrentPercent;
            CurrentPercent = Mathf.Clamp(CurrentPercent + delta, 0, 100);
            EventBus.Instance.RaiseArchiveChanged(CurrentPercent);

            if (before < 75f && CurrentPercent >= 75f)
                EventBus.Instance.RaiseZoneUnlocked();

            if (CurrentPercent >= 100f)
            {
                // TODO: Ending flow
            }
        }
    }
}
