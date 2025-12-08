using UnityEngine;
using IronIvy.Interfaces;

namespace IronIvy.Core
{
    public class TreeOfWhispers : MonoBehaviour, IInteractable
    {
        public void Interact()
        {
            DayCycleManager.Instance.EndDay();
        }
    }
}
