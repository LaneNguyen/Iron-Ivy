using UnityEngine;
using Unity.Cinemachine;
using IronIvy.Systems.Camera;

namespace IronIvy.Gameplay
{
    public class PlayerControlModeSwitcher : MonoBehaviour
    {
        [Header("Controllers")]
        [SerializeField] private IsoPlayerController isoController;   // old iso move
        [SerializeField] private PlayerThirdPersonController tpsController;   // new tps move

        [Header("Prefer ref match (more reliable)")]
        [SerializeField] private CinemachineCamera isoCamRef;   
        [SerializeField] private CinemachineCamera tpsCamRef;   

        [Header("Fallback: match by name contains")]
        [SerializeField] private string isoCameraId = "isocamera";   // vcam name 
        [SerializeField] private string tpsCameraId = "3rdcamera";   // vcam name

        [Header("Options")]
        [SerializeField] private bool logDebug = false;

        private void OnEnable()
        {
            EnforceSingleEnabled();

            TrySyncToCurrentCamera();

            if (CameraManager.HasInstance)
            {
                CameraManager.Instance.OnCameraChanged += HandleCameraChanged;

                // and sync again from manager's current (covers scene start)
                var cur = CameraManager.Instance.CurrentCamera;
                ApplyByCamera(cur);
            }
            else if (logDebug)
            {
                Debug.Log("[PCM Switcher] CameraManager not ready, using current controller state.");
            }
        }

        private void OnDisable()
        {
            if (CameraManager.HasInstance)
                CameraManager.Instance.OnCameraChanged -= HandleCameraChanged;
        }

        private void HandleCameraChanged(CinemachineCamera oldCam, CinemachineCamera newCam)
        {
            if (logDebug) Debug.Log($"[PCM Switcher] OnCameraChanged -> {(newCam ? newCam.name : "null")}");
            ApplyByCamera(newCam);
        }

        private void ApplyByCamera(CinemachineCamera cam)
        {
            if (cam == null)
            {
                if (logDebug) Debug.Log("[PCM Switcher] cam null, keep current mode.");
                return;
            }

            // 1) prefer reference equality (most reliable)
            if (tpsCamRef && cam == tpsCamRef)
            {
                SetTPS();
                return;
            }
            if (isoCamRef && cam == isoCamRef)
            {
                SetISO();
                return;
            }

            // 2) fallback: by name contains
            string nameLower = cam.name.ToLowerInvariant();
            bool hitTPS = !string.IsNullOrEmpty(tpsCameraId) && nameLower.Contains(tpsCameraId.ToLowerInvariant());
            bool hitISO = !string.IsNullOrEmpty(isoCameraId) && nameLower.Contains(isoCameraId.ToLowerInvariant());

            if (hitTPS)
            {
                SetTPS();
            }
            else if (hitISO)
            {
                SetISO();
            }
            else
            {
                if (logDebug) Debug.Log($"[PCM Switcher] no match for camera: {cam.name}. keep mode.");
            }
        }

        public void SetISO()
        {
            // enable iso, disable tps; also gate TPS input
            if (tpsController)
            {
                tpsController.SetTPSActive(false); // gate first so it stops reading input
                tpsController.enabled = false;     // optional: fully disable, since pivot follow runs even when disabled? -> in your TPS we kept it enabled before; now we disable for safety
            }
            if (isoController) isoController.enabled = true;

            if (logDebug) Debug.Log("[PCM Switcher] MODE = ISO");
        }

        public void SetTPS()
        {
            // enable tps, disable iso; resync angles to avoid snap
            if (isoController) isoController.enabled = false;

            if (tpsController)
            {
                tpsController.enabled = true;          // make sure component is ON
                tpsController.ResyncCameraAnglesFromPivot();
                tpsController.SetTPSActive(true);      // open gate after enabled
            }

            if (logDebug) Debug.Log("[PCM Switcher] MODE = TPS");
        }

        private void EnforceSingleEnabled()
        {
            bool isoOn = isoController && isoController.enabled;
            bool tpsOn = tpsController && tpsController.enabled;

            // if both on or both off -> default ISO
            if ((isoOn && tpsOn) || (!isoOn && !tpsOn))
            {
                if (isoController) isoController.enabled = true;
                if (tpsController)
                {
                    tpsController.SetTPSActive(false); // just in case
                    tpsController.enabled = false;
                }
            }
        }

        private void TrySyncToCurrentCamera()
        {
            // best effort even if manager not ready, using refs if assigned
            if (!CameraManager.HasInstance)
            {
                if (tpsCamRef && tpsCamRef.isActiveAndEnabled) { SetTPS(); return; }
                if (isoCamRef && isoCamRef.isActiveAndEnabled) { SetISO(); return; }
                // else keep current state
                return;
            }

            var cur = CameraManager.Instance.CurrentCamera;
            ApplyByCamera(cur);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (isoController == tpsController && isoController != null)
                Debug.LogWarning("[PCM Switcher] isoController and tpsController point to same component. nope.");

            // small guard: empty ids are fine but warn if both empty
            if (string.IsNullOrWhiteSpace(isoCameraId) && string.IsNullOrWhiteSpace(tpsCameraId) && !isoCamRef && !tpsCamRef)
                Debug.LogWarning("[PCM Switcher] no refs and empty ids -> cannot match camera. assign refs or ids.");
        }
#endif
    }
}