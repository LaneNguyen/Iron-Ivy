using UnityEngine;
using Unity.Cinemachine;

namespace IronIvy.Systems.Camera
{
    [RequireComponent(typeof(Collider))]
    public class CameraTrigger : MonoBehaviour
    {
        [Header("Player Tag")]
        [SerializeField] private string playerTag = "Player";

        [Header("Đích đến (chọn 1)")]
        [SerializeField] private CinemachineCamera targetCamera;
        [SerializeField] private string targetCameraID;

        [Header("Tùy chọn")]
        [SerializeField] private bool restoreOnExit = true;

        private void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag(playerTag)) return;
            if (!CameraManager.HasInstance)
            {
                Debug.LogWarning("[CameraTrigger] CameraManager chưa sẵn sàng.");
                return;
            }

            if (targetCamera != null)
                CameraManager.Instance.SwitchCamera(targetCamera);
            else if (!string.IsNullOrWhiteSpace(targetCameraID))
                CameraManager.Instance.SwitchCamera(targetCameraID);
            else
                Debug.LogWarning($"[CameraTrigger] Chưa cấu hình target cho {name}");
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag(playerTag)) return;
            if (!restoreOnExit || !CameraManager.HasInstance) return;
            CameraManager.Instance.RestorePreviousCamera();
        }
    }
}
