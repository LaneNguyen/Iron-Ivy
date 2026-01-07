using UnityEngine;
using Unity.Cinemachine;
using IronIvy.Systems.Camera;

namespace IronIvy.Gameplay
{
    // switch giữa 2 chế độ điều khiển:
    // - iso controller: topdown / isometric
    // - tps controller: third person + pivot orbit
    //
    // cinematic-safe:
    // - KHÔNG auto switch camera trong Start() nếu đang intro lock
    // - chỉ bắt đầu setup mode khi InputLock(false) (Enter Gameplay)
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

        // opening intro lock (event-driven)
        private bool _inputLocked = false;

        // để đảm bảo init chỉ chạy 1 lần khi unlock
        private bool _didInitialApply = false;

        private void Awake()
        {
            // đảm bảo TPS controller luôn bật component
            // để pivot follow player mọi lúc
            if (tpsController != null)
                tpsController.enabled = true;
        }

        private void OnEnable()
        {
            if (IronIvy.Core.ListenManager.HasInstance)
                IronIvy.Core.ListenManager.Instance.OnInputLockRequested += HandleInputLockRequested;
        }

        private void OnDisable()
        {
            if (IronIvy.Core.ListenManager.HasInstance)
                IronIvy.Core.ListenManager.Instance.OnInputLockRequested -= HandleInputLockRequested;
        }

        private void Start()
        {
            // BEFORE: auto SwitchToIso/Tps ngay ở Start -> gây flash camera
            // NOW: chỉ apply ngay nếu không locked, còn locked thì đợi unlock.
            if (!_inputLocked)
            {
                ApplyInitialModeOnce();
            }
        }

        private void Update()
        {
            // cinematic lock: không cho đổi mode khi đang intro
            if (_inputLocked) return;

            // demo: nhấn Tab để test nhanh
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                if (isIsoMode)
                    SwitchToTps();
                else
                    SwitchToIso();
            }
        }

        private void HandleInputLockRequested(bool locked)
        {
            _inputLocked = locked;

            // Khi unlock lần đầu -> apply mode startup
            if (!locked)
            {
                ApplyInitialModeOnce();
            }
        }

        private void ApplyInitialModeOnce()
        {
            if (_didInitialApply) return;
            _didInitialApply = true;

            if (startWithIso)
                SwitchToIso();
            else
                SwitchToTps();

            if (logDebug)
                Debug.Log("[PlayerControlModeSwitcher] initial mode applied after unlock");
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
