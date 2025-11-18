using UnityEngine;
using Unity.Cinemachine;
using IronIvy.Systems.Camera;

namespace IronIvy.Gameplay
{
    /// <summary>
    /// Rất đơn giản: quản lý bật tắt 2 controller (ISO / TPS) và ưu tiên camera tương ứng.
    /// - Không disable CharacterController, chỉ enable/disable script controller.
    /// - Không phụ thuộc nhiều vào CameraManager, tránh bị "kẹt" khi camera lạ xuất hiện.
    /// </summary>
    public class PlayerControlModeSwitcher : MonoBehaviour
    {
        [Header("Controllers")]
        [SerializeField] private IsoPlayerController isoController;                 // ISO move (top-down / isometric)
        [SerializeField] private PlayerThirdPersonController tpsController;         // TPS move (over shoulder)

        [Header("Preferred cameras (optional)")]
        [SerializeField] private CinemachineCamera isoCamRef;                       // ISO vcam
        [SerializeField] private CinemachineCamera tpsCamRef;                       // TPS vcam

        [Header("Settings")]
        [Tooltip("Start game bằng ISO mode (true) hay TPS mode (false)")]
        [SerializeField] private bool startWithIso = true;

        [Tooltip("Tự động set Priority cho camera khi switch mode")]
        [SerializeField] private bool autoSwitchCamera = true;

        [Tooltip("In log debug khi switch mode để dễ trace")]
        [SerializeField] private bool logDebug = false;

        // 0 = ISO, 1 = TPS
        private int currentMode = 0;

        private void Awake()
        {
            // Nếu inspector chưa kéo sẵn thì thử tìm trên cùng GameObject
            if (!isoController)
                isoController = GetComponent<IsoPlayerController>();
            if (!tpsController)
                tpsController = GetComponent<PlayerThirdPersonController>();
        }

        private void Start()
        {
            // đảm bảo luôn có ít nhất 1 controller chạy
            if (startWithIso)
                SwitchToIso();
            else
                SwitchToTps();
        }

        /// <summary>
        /// Public API: gọi từ UI / input khác để chuyển sang ISO mode.
        /// </summary>
        public void SwitchToIso()
        {
            currentMode = 0;

            if (isoController)
                isoController.enabled = true;          // bật ISO
            if (tpsController)
                tpsController.enabled = false;         // tắt TPS (script)

            // gate TPS state nội bộ cho chắc
            if (tpsController)
                tpsController.SetTPSActive(false);

            // chỉnh camera nếu có
            if (autoSwitchCamera)
                ActivateCamera(isoCamRef, tpsCamRef);

            if (logDebug) Debug.Log("[PCM] Switched to ISO mode");
        }

        /// <summary>
        /// Public API: gọi từ UI / input khác để chuyển sang TPS mode.
        /// </summary>
        public void SwitchToTps()
        {
            currentMode = 1;

            if (isoController)
                isoController.enabled = false;         // tắt ISO script
            if (tpsController)
            {
                tpsController.enabled = true;          // bật TPS script
                tpsController.SetTPSActive(true);      // mở gate nội bộ
                tpsController.ResyncCameraAnglesFromPivot();
            }

            if (autoSwitchCamera)
                ActivateCamera(tpsCamRef, isoCamRef);

            if (logDebug) Debug.Log("[PCM] Switched to TPS mode");
        }

        /// <summary>
        /// Toggle giữa 2 mode (có thể gán vào phím tắt sau này).
        /// </summary>
        public void ToggleMode()
        {
            if (currentMode == 0) SwitchToTps();
            else SwitchToIso();
        }

        /// <summary>
        /// Helper: set priority cho 2 camera, cameraActive được ưu tiên cao hơn.
        /// </summary>
        private void ActivateCamera(CinemachineCamera cameraActive, CinemachineCamera cameraInactive)
        {
            if (cameraActive != null)
                cameraActive.Priority = 20;
            if (cameraInactive != null)
                cameraInactive.Priority = 5;

            // ❌ GỠ BỎ phần này:
            // if (CameraManager.HasInstance && cameraActive != null)
            // {
            //     CameraManager.Instance.SetCurrentCamera(cameraActive);
            // }
        }


#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!isoController)
                isoController = GetComponent<IsoPlayerController>();
            if (!tpsController)
                tpsController = GetComponent<PlayerThirdPersonController>();

            if (isoController == tpsController && isoController != null)
                Debug.LogWarning("[PCM] isoController and tpsController reference same component.");
        }
#endif
    }
}
