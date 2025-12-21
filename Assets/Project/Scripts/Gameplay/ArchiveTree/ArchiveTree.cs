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

            // 1) VFX
            if (healEffect != null) healEffect.Play();
            yield return new WaitForSecondsRealtime(animDuration);


            // 2) Backend
            if (EnergyManager.HasInstance) EnergyManager.Instance.RestoreFullEnergy();
            if (SaveLoadManager.HasInstance) SaveLoadManager.Instance.SaveGame();

            // 2.5) báo UI refresh (event-driven)
            if (ListenManager.HasInstance && EnergyManager.HasInstance)
                ListenManager.Instance.RaiseEnergyChanged(EnergyManager.Instance.Current);

            Debug.Log("[ArchiveTree] Da hoi phuc & Save game!");

            // 3) Request mở Archive UI bằng event (không gọi UIManager trực tiếp)
            if (ListenManager.HasInstance)
                ListenManager.Instance.RaiseArchiveOpenRequested();

            isResting = false;
        }
    }
}
