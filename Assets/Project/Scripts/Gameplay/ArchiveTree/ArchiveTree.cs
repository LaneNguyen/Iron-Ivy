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
            
            // 1. Hiệu ứng hồi phục
            if (healEffect != null) healEffect.Play();
            yield return new WaitForSeconds(animDuration);

            // 2. Logic Backend (Hồi máu + Save)
            if (EnergyManager.HasInstance) EnergyManager.Instance.RestoreFullEnergy();
            if (SaveLoadManager.HasInstance) SaveLoadManager.Instance.SaveGame();

            Debug.Log("[ArchiveTree] Da hoi phuc & Save game!");

            // 3. [NEW] Gọi UIManager để chuyển cảnh sang Archive Panel
            if (UIManager.HasInstance)
            {
                UIManager.Instance.OpenArchiveUI();
            }

            isResting = false;
        }
    }
}