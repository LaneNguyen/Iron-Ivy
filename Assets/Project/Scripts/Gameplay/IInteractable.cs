using UnityEngine;

namespace IronIvy.Gameplay
{
    public interface IInteractable
    {
        string Prompt { get; }
        void Interact(GameObject interactor);
        Vector3 WorldPosition { get; }
    }
}