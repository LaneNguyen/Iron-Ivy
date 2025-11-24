using UnityEngine;
using Unity.Cinemachine;
using IronIvy.Systems.Camera;
using IronIvy.Gameplay;

namespace IronIvy.Gameplay
{
    // switch giữa 2 chế độ điều khiển:
    // - iso controller: topdown / isometric
    // - tps controller: third person + pivot orbit
    //
    // lưu ý:
    // - TPS luôn enable component => LateUpdate chạy để pivot follow player
    // - bật/tắt input TPS thì để CameraManager + PlayerThirdPersonController lo
    //   (qua autoEnableByCamera + OnCameraChanged)
    public class PlayerControlModeSwitcher : MonoBehaviour
    {
        [Header("Controllers")]
        [Tooltip("script điều khiển iso (topdown)")]
        public IsoPlayerController isoController;

        [Tooltip("script điều khiển third person")]
        public PlayerThirdPersonController tpsController;

        [Header("Preferred cameras (optional)")]
        [Tooltip("camera dùng cho iso view (nếu có)")]
        public CinemachineCamera isoCamRef;

        [Tooltip("camera dùng cho third person view (nếu có)")]
        public CinemachineCamera tpsCamRef;

        [Header("Settings")]
        [Tooltip("start game bằng iso hay không")]
        public bool startWithIso = true;

        [Tooltip("khi đổi mode thì tự switch camera luôn")]
        public bool autoSwitchCamera = true;

        [Tooltip("in log nhỏ nhỏ cho dễ debug")]
        public bool logDebug = false;

        // state hiện tại
        private bool isIsoMode = true;

        private void Awake()
        {
            // đảm bảo TPS controller luôn bật component
            // để pivot follow player mọi lúc
            if (tpsController != null)
                tpsController.enabled = true;
        }

        private void Start()
        {
            if (startWithIso)
                SwitchToIso();
            else
                SwitchToTps();
        }

        private void Update()
        {
            // demo: nhấn Tab để test nhanh
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                if (isIsoMode)
                    SwitchToTps();
                else
                    SwitchToIso();
            }
        }

        public void SwitchToIso()
        {
            isIsoMode = true;

            // bật iso controller
            if (isoController != null)
                isoController.enabled = true;

            // TPS input sẽ tự OFF vì camera sẽ chuyển sang isoCamRef
            if (autoSwitchCamera && CameraManager.HasInstance && isoCamRef != null)
            {
                CameraManager.Instance.SwitchCamera(isoCamRef);
            }

            if (logDebug)
                Debug.Log("[PlayerControlModeSwitcher] switched to ISO mode");
        }

        public void SwitchToTps()
        {
            isIsoMode = false;

            // tắt iso controller
            if (isoController != null)
                isoController.enabled = false;

            // chỉ cần đổi camera, PlayerThirdPersonController sẽ nhận event
            if (autoSwitchCamera && CameraManager.HasInstance && tpsCamRef != null)
            {
                CameraManager.Instance.SwitchCamera(tpsCamRef);
            }

            if (logDebug)
                Debug.Log("[PlayerControlModeSwitcher] switched to TPS mode");
        }
    }
}
