
using UnityEngine;
using Unity.Cinemachine;
using IronIvy.Systems.Camera; 

namespace IronIvy.Gameplay
{
    public class PlayerControlModeSwitcher : MonoBehaviour
    {
        [Header("Controller tham chiếu")]
        [SerializeField] private MonoBehaviour isoController;            // ví dụ IsoPlayerController hoặc PlayerController cũ
        [SerializeField] private PlayerThirdPersonController tpsController;

        [Header("Quy ước id camera")]
        [SerializeField] private string isoCameraId = "iso";
        [SerializeField] private string tpsCameraId = "tps";

        private void OnEnable()
        {
            var camMgr = CameraManager.Instance;
            if (camMgr != null)
                camMgr.OnCameraChanged += HandleCameraChanged;
        }

        private void OnDisable()
        {
            var camMgr = CameraManager.Instance;
            if (camMgr != null)
                camMgr.OnCameraChanged -= HandleCameraChanged;
        }

        // Ham nay duoc goi moi khi camera duoc doi boi CameraManager
        private void HandleCameraChanged(CinemachineCamera oldCam, CinemachineCamera newCam)
        {
            // Idea 1: so khop theo id camera gan tren CameraManager
            // Neu goi SwitchCamera(string id) thi luu lai id gan voi newCam de biet che do
            // Idea 2 là so khop theo ten camera hoac tag
            string camName = newCam != null ? newCam.name.ToLowerInvariant() : string.Empty;

            bool wantTPS =
                camName.Contains(tpsCameraId.ToLowerInvariant()); // vi du camera ten TPS_Follow

            bool wantISO =
                camName.Contains(isoCameraId.ToLowerInvariant()); // vi du camera ten ISO_Ortho

            if (wantTPS) SetModeTPS();
            else if (wantISO) SetModeISO();
            else
            {
                // Mac dinh neu khong ro thi giu che do hien tai
            }

            // Dong bo cameraPivot 
            if (tpsController != null && tpsController.enabled)
                tpsController.ResyncCameraAnglesFromPivot();
        }

        public void SetModeTPS()
        {
            if (isoController != null) isoController.enabled = false;
            if (tpsController != null) tpsController.enabled = true;
        }

        public void SetModeISO()
        {
            if (tpsController != null) tpsController.enabled = false;
            if (isoController != null) isoController.enabled = true;
        }
    }
}
