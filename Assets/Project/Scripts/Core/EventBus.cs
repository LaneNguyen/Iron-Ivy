using System;
using UnityEngine;

namespace IronIvy.Core
{
    public class EventBus : BaseManager<EventBus>
    {
        public event Action<int> OnEnergyChanged;
        public event Action<float> OnArchiveChanged;
        public event Action OnZoneUnlocked;
        public event Action OnTrustSuccess;
        public event Action OnDayEnded;
        public event Action OnMinigameStarted;
        public event Action OnMinigameStopped;

        public void RaiseEnergyChanged(int v) => OnEnergyChanged?.Invoke(v);
        public void RaiseArchiveChanged(float v) => OnArchiveChanged?.Invoke(v);
        public void RaiseZoneUnlocked() => OnZoneUnlocked?.Invoke();
        public void RaiseTrustSuccess() => OnTrustSuccess?.Invoke();
        public void RaiseDayEnded() => OnDayEnded?.Invoke();
        public void RaiseMinigameStarted() => OnMinigameStarted?.Invoke();
        public void RaiseMinigameStopped() => OnMinigameStopped?.Invoke();
    }
}
