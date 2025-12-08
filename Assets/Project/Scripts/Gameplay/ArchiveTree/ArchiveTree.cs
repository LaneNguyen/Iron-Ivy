using UnityEngine;
using IronIvy.Core;

namespace IronIvy.Gameplay.World
{
    public class ArchiveTree : MonoBehaviour
    {
        [Header("Settings")]
        public Transform restingPosition;
        public float animDuration = 2f;
        
        [Header("VFX/SFX")]
        public ParticleSystem healEffect;
        
        private bool isResting = false;

        public void OnInteract()
        {
            if (isResting) return;

            Debug.Log("[ArchiveTree] IV-17 bat dau ket noi...");
            StartCoroutine(RestRoutine());
        }

        private System.Collections.IEnumerator RestRoutine()
        {
            isResting = true;
            
            // TODO: Bao cho InteractionTrigger giau UI "Press F" di neu can
            // hoac PlayerController khoa input
            
            // 1. VFX & Animation
            if (healEffect != null) healEffect.Play();
            
            // 2. Cho dien hoat canh
            yield return new WaitForSeconds(animDuration);

            // 3. Logic Gameplay
            if (EnergyManager.HasInstance)
            {
                EnergyManager.Instance.RestoreFullEnergy();
            }

            if (SaveLoadManager.HasInstance)
            {
                SaveLoadManager.Instance.SaveGame();
            }

            Debug.Log("[ArchiveTree] Da hoi phuc & Save game!");
            isResting = false;
        }
    }
}