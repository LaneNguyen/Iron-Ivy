using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Playables;

namespace IronIvy.Core
{
    public class IntroFlowController : MonoBehaviour
    {
        [Header("Director")]
        [SerializeField] private PlayableDirector director;

        [Header("Camera IDs (CameraManager)")]
        [SerializeField] private string introCameraId = "IntroCam";
        [SerializeField] private string establishCameraId = "EstablishCam";

        // NOTE:
        // Nếu bạn muốn gameplay thật sự là TPS/3rd, hãy set gameplayCameraId = "3rdcamera"
        // Nếu bạn muốn một game cam khác, giữ "GameCam" nhưng phải tồn tại trong CameraManager map.
        [SerializeField] private string gameplayCameraId = "GameCam";

        [Header("Timings")]
        [SerializeField, Tooltip("Đợi UI fade out (Timeline CanvasGroup) trước khi switch sang establish cam.")]
        private float uiFadeOutWait = 0.15f;

        [SerializeField, Tooltip("Giữ establishing shot bao lâu trước khi về gameplay cam.")]
        private float establishHold = 1.2f;

        [SerializeField, Tooltip("Nếu project chưa RaiseSystemsReady, dùng wait ngắn để tránh chậm.")]
        private float systemsReadyTimeout = 0.15f;

        [SerializeField, Tooltip("Sau khi show timeline canvas + switch intro cam, đợi 1 frame realtime để tránh canvas bị mờ ở frame đầu.")]
        private bool waitOneFrameBeforeTimelinePlay = true;

        [Header("Behavior")]
        [SerializeField] private bool pauseGameDuringIntro = true;
        [SerializeField] private bool pauseBgmDuringIntro = true;

        [Header("Auto Start")]
        [SerializeField] private bool startOnGameSceneEntered = true;

        [Header("Debug")]
        [SerializeField] private bool logFlow = true;

        private bool _started;
        private bool _ending;
        private float _cachedTimeScale = 1f;

        private void Awake()
        {
            if (director != null)
            {
                director.playOnAwake = false;
                director.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
            }
        }

        private void OnEnable()
        {
            if (ListenManager.HasInstance)
            {
                ListenManager.Instance.OnGameSceneEntered += HandleGameSceneEntered;
                ListenManager.Instance.OnIntroSkipRequested += HandleSkipRequested;
            }

            if (director != null)
            {
                director.stopped += HandleDirectorStopped;
            }
        }

        private void OnDisable()
        {
            if (ListenManager.HasInstance)
            {
                ListenManager.Instance.OnGameSceneEntered -= HandleGameSceneEntered;
                ListenManager.Instance.OnIntroSkipRequested -= HandleSkipRequested;
            }

            if (director != null)
            {
                director.stopped -= HandleDirectorStopped;
            }
        }

        private void HandleGameSceneEntered()
        {
            if (!startOnGameSceneEntered) return;
            if (_started) return;

            _started = true;

            if (logFlow) Debug.Log("[IntroFlow] HandleGameSceneEntered received");

            // ✅ LOCK phase ngay khi bắt đầu Intro để chặn mọi auto-switch sang gameplay cam.
            if (IronIvy.Systems.Camera.CameraManager.HasInstance)
            {
                IronIvy.Systems.Camera.CameraManager.Instance.SetPhaseIntroLocked();
            }

            StartCoroutine(Co_RunIntro());
        }

        private IEnumerator Co_RunIntro()
        {
            yield return WaitSystemsReadyOrTimeoutShort();

            if (pauseGameDuringIntro)
            {
                _cachedTimeScale = Time.timeScale;
                Time.timeScale = 0f;
            }

            if (pauseBgmDuringIntro && AudioManager.Instance != null)
            {
                AudioManager.Instance.PauseBGMRuntime();
            }

            RaiseInputLock(true);
            RaiseGameplayHUDVisible(false);
            RaiseMinimapVisible(false);

            RaiseCameraSwitch(introCameraId, pushHistory: false);
            RaiseTimelineCanvasShow();

            if (waitOneFrameBeforeTimelinePlay)
                yield return null;

            if (director != null)
            {
                try
                {
                    director.time = 0;
                    director.Evaluate();
                    director.Play();

                    if (ListenManager.HasInstance)
                        ListenManager.Instance.RaiseIntroTimelineStarted();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[IntroFlowController] Director play failed: {e.Message}");
                    StartCoroutine(Co_EndIntro(force: true));
                }
            }
            else
            {
                StartCoroutine(Co_EndIntro(force: true));
            }
        }

        private IEnumerator WaitSystemsReadyOrTimeoutShort()
        {
            if (!ListenManager.HasInstance)
            {
                yield return null;
                yield break;
            }

            bool ready = false;
            void MarkReady() => ready = true;

            ListenManager.Instance.OnSystemsReady += MarkReady;

            float t = 0f;
            while (!ready && t < systemsReadyTimeout)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            ListenManager.Instance.OnSystemsReady -= MarkReady;
        }

        private void HandleDirectorStopped(PlayableDirector d)
        {
            if (_ending) return;
            StartCoroutine(Co_EndIntro(force: false));
        }

        private void HandleSkipRequested()
        {
            if (_ending) return;

            _ending = true;

            if (director != null)
            {
                try
                {
                    director.Stop();
                    director.time = 0;
                    director.Evaluate();
                }
                catch { }
            }

            StartCoroutine(Co_EndIntro(force: true));
        }

        private IEnumerator Co_EndIntro(bool force = false)
        {
            if (_ending && !force) yield break;
            _ending = true;

            if (logFlow) Debug.Log("[IntroFlow] Co_EndIntro begin");

            RaiseTimelineCanvasHide();
            if (uiFadeOutWait > 0f)
                yield return new WaitForSecondsRealtime(uiFadeOutWait);

            // 1) Establish shot (vẫn đang IntroLocked)
            RaiseCameraSwitch(establishCameraId, false);
            if (establishHold > 0f)
                yield return new WaitForSecondsRealtime(establishHold);

            // 2) UNLOCK phase đúng thời điểm (trước khi switch gameplay cam)
            if (IronIvy.Systems.Camera.CameraManager.HasInstance)
                IronIvy.Systems.Camera.CameraManager.Instance.SetPhaseGameplay();

            // 3) Resolve gameplay cam id (ưu tiên TPS nếu bạn đang muốn 3rd)
            string resolvedGameplayId = ResolveGameplayCameraId();

            if (logFlow) Debug.Log($"[IntroFlow] Switch to gameplay camera id='{resolvedGameplayId}'");

            RaiseCameraSwitch(resolvedGameplayId, false);

            // settle 1 frame realtime trước khi unlock input/unpause
            yield return null;

            // 4) Unpause + resume audio
            if (pauseGameDuringIntro)
                Time.timeScale = _cachedTimeScale;

            if (pauseBgmDuringIntro && AudioManager.Instance != null)
                AudioManager.Instance.ResumeBGMRuntime();

            // 5) HUD + input + gameplay begin
            if (ListenManager.HasInstance)
            {
                ListenManager.Instance.RaiseGameplayHUDVisibleRequested(true);
                ListenManager.Instance.RaiseMinimapVisibleRequested(true);
                ListenManager.Instance.RaiseInputLockRequested(false);

                ListenManager.Instance.RaiseGameplayBegin();

                // fallback giữ nguyên (không phá logic cũ)
                RaiseCameraSwitch(resolvedGameplayId, false);

                if (IronIvy.Systems.Camera.CameraManager.HasInstance)
                    IronIvy.Systems.Camera.CameraManager.Instance.SwitchCamera(resolvedGameplayId, pushHistory: false);
            }

            if (logFlow) Debug.Log("[IntroFlow] Co_EndIntro end");
        }

        // Ưu tiên 3rdcamera nếu gameplayCameraId đang để "GameCam" nhưng project thật sự muốn TPS.
        // Nếu bạn không muốn TPS, chỉ việc set gameplayCameraId đúng cam bạn muốn và hàm này vẫn trả gameplayCameraId.
        private string ResolveGameplayCameraId()
        {
            // Nếu bạn đã set gameplayCameraId = "3rdcamera" rồi thì thôi khỏi đoán.
            if (string.Equals(gameplayCameraId, "3rdcamera", StringComparison.OrdinalIgnoreCase))
                return gameplayCameraId;

            // Nếu CameraManager có key "3rdcamera" và bạn đang gặp case “xong intro phải về 3rd”
            // thì ưu tiên nó để tránh việc switch nhầm "GameCam" không tồn tại / không phải cam TPS.
            if (IronIvy.Systems.Camera.CameraManager.HasInstance)
            {
                // Trick nhẹ: thử switch “probe” bằng cách gọi SwitchCamera với pushHistory=false
                // nhưng KHÔNG gọi thật ở đây để tránh side effect. Chỉ kiểm tra map tồn tại bằng TrySwitch? (không có)
                // => cách an toàn: nếu bạn muốn chắc chắn, hãy set gameplayCameraId = "3rdcamera" trong Inspector.
            }

            return gameplayCameraId;
        }

        private static void RaiseInputLock(bool locked)
        {
            if (ListenManager.HasInstance)
                ListenManager.Instance.RaiseInputLockRequested(locked);
        }

        private static void RaiseGameplayHUDVisible(bool visible)
        {
            if (ListenManager.HasInstance)
                ListenManager.Instance.RaiseGameplayHUDVisibleRequested(visible);
        }

        private static void RaiseMinimapVisible(bool visible)
        {
            if (ListenManager.HasInstance)
                ListenManager.Instance.RaiseMinimapVisibleRequested(visible);
        }

        private static void RaiseTimelineCanvasShow()
        {
            if (ListenManager.HasInstance)
                ListenManager.Instance.RaiseTimelineCanvasShowRequested();
        }

        private static void RaiseTimelineCanvasHide()
        {
            if (ListenManager.HasInstance)
                ListenManager.Instance.RaiseTimelineCanvasHideRequested();
        }

        private static void RaiseCameraSwitch(string cameraId, bool pushHistory)
        {
            if (!ListenManager.HasInstance) return;
            if (string.IsNullOrWhiteSpace(cameraId)) return;

            ListenManager.Instance.RaiseCameraSwitchRequested(
                new ListenManager.CameraSwitchRequestPayload(cameraId, pushHistory)
            );
        }
    }
}
