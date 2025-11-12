using UnityEngine;
using Unity.Cinemachine;
using IronIvy.Systems.Camera;

namespace IronIvy.Gameplay
{
    public class PlayerControlModeSwitcher : MonoBehaviour
    {
        [Header("Controllers")]
        [SerializeField] private IsoPlayerController isoController;                 // ISO move
        [SerializeField] private PlayerThirdPersonController tpsController;         // TPS move (keep enabled)

        [Header("Prefer ref match")]
        [SerializeField] private CinemachineCamera isoCamRef;                       // drag ISO vcam
        [SerializeField] private CinemachineCamera tpsCamRef;                       // drag TPS vcam

        [Header("Fallback: name contains")]
        [SerializeField] private string isoCameraId = "isocamera";
        [SerializeField] private string tpsCameraId = "3rdcamera";

        [Header("Debug")]
        [SerializeField] private bool logDebug = false;

        void OnEnable()
        {
            // make sure TPS component stays enabled so pivot follow always runs
            if (tpsController && !tpsController.enabled) tpsController.enabled = true;

            if (CameraManager.HasInstance)
                CameraManager.Instance.OnCameraChanged += HandleCameraChanged;

            // sync once with current camera (if any)
            var cur = CameraManager.HasInstance ? CameraManager.Instance.CurrentCamera : null;
            if (cur) ApplyByCamera(cur);
            else DefaultISOIfAmbiguous();
        }

        void OnDisable()
        {
            if (CameraManager.HasInstance)
                CameraManager.Instance.OnCameraChanged -= HandleCameraChanged;
        }

        void HandleCameraChanged(CinemachineCamera oldCam, CinemachineCamera newCam)
        {
            if (logDebug) Debug.Log($"[PCM] OnCameraChanged -> {(newCam ? newCam.name : "null")}");
            if (newCam) ApplyByCamera(newCam);
        }

        void ApplyByCamera(CinemachineCamera cam)
        {
            // 1) ref match
            if (tpsCamRef && cam == tpsCamRef) { SetTPS(); return; }
            if (isoCamRef && cam == isoCamRef) { SetISO(); return; }

            // 2) name contains
            string n = cam.name.ToLowerInvariant();
            if (!string.IsNullOrEmpty(tpsCameraId) && n.Contains(tpsCameraId.ToLowerInvariant())) { SetTPS(); return; }
            if (!string.IsNullOrEmpty(isoCameraId) && n.Contains(isoCameraId.ToLowerInvariant())) { SetISO(); return; }

            if (logDebug) Debug.Log($"[PCM] no match for camera: {cam.name}. keep current.");
        }

        public void SetISO()
        {
            // ISO on; TPS gate closed (but component stays enabled)
            if (isoController) isoController.enabled = true;

            if (tpsController)
            {
                tpsController.SetTPSActive(false);     // close gate (no input, only pivot follow)
                // do NOT disable tpsController here
            }

            if (logDebug) Debug.Log("[PCM] MODE = ISO");
        }

        public void SetTPS()
        {
            // TPS gate open; ISO off
            if (isoController) isoController.enabled = false;

            if (tpsController)
            {
                if (!tpsController.enabled) tpsController.enabled = true; // paranoia
                tpsController.ResyncCameraAnglesFromPivot();
                tpsController.SetTPSActive(true);      // open gate (RMB + move)
            }

            if (logDebug) Debug.Log("[PCM] MODE = TPS");
        }

        void DefaultISOIfAmbiguous()
        {
            // if both controllers on or both off at start, prefer ISO
            bool isoOn = isoController && isoController.enabled;
            bool tpsOn = tpsController && tpsController.enabled;

            if ((isoOn && tpsOn) || (!isoOn && !tpsOn))
            {
                if (isoController) isoController.enabled = true;
                if (tpsController)
                {
                    if (!tpsController.enabled) tpsController.enabled = true; // keep enabled
                    tpsController.SetTPSActive(false);
                }
                if (logDebug) Debug.Log("[PCM] default ISO at start");
            }
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (isoController == tpsController && isoController != null)
                Debug.LogWarning("[PCM] isoController and tpsController reference same component.");
        }
#endif
    }
}
