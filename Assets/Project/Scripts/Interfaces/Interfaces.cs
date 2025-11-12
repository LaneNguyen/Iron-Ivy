using UnityEngine;

namespace IronIvy.Interfaces
{
    public interface IInteractable { void Interact(); }

    public interface IMinigame
    {
        void StartGame();
        void StopGame();
        bool IsRunning { get; }
    }
}
