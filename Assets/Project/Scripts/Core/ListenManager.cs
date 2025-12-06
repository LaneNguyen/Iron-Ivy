using System;
using UnityEngine;

namespace IronIvy.Core
{
    public class ListenManager : BaseManager<ListenManager>
    {
        public event Action<int> OnEnergyChanged;
        public event Action<float> OnArchiveChanged;

        public event Action OnMinigameStarted;
        public event Action OnMinigameStopped;

        public event Action OnDayEnded;
        public event Action OnTrustSuccess;

        public event Action OnInventoryChanged;
        
        // Event quan trọng để chống Race Condition nè trời ơi má ơi má
        public event Action OnSystemsReady;

        public void RaiseEnergyChanged(int value) => OnEnergyChanged?.Invoke(value);
        public void RaiseArchiveChanged(float value) => OnArchiveChanged?.Invoke(value);
        public void RaiseMinigameStarted() => OnMinigameStarted?.Invoke();
        public void RaiseMinigameStopped() => OnMinigameStopped?.Invoke();
        public void RaiseDayEnded() => OnDayEnded?.Invoke();
        public void RaiseTrustSuccess() => OnTrustSuccess?.Invoke();

        public void RaiseInventoryChanged() => OnInventoryChanged?.Invoke();

        public void RaiseSystemsReady()
        {
            Debug.Log("<color=green>[ListenManager] Tất cả đã vào vị trí zồi Y</color>");
            OnSystemsReady?.Invoke();
        }
    }
}