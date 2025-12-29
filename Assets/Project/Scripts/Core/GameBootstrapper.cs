using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace IronIvy.Core
{
    public class GameBootstrapper : BaseManager<GameBootstrapper>
    {
        [Header("Scenes")]
        [SerializeField] private string gameplaySceneName = "GameplayScene";

        [Header("Overlay (Active/Disable)")]
        [SerializeField] private GameObject loadingOverlay; // Panel root (ideally under BootstrapperRoot - DontDestroy)
        [SerializeField] private Image fadeImage;           // Fullscreen Image (child of overlay)

        [Header("Loading UI (Optional)")]
        [Tooltip("Optional: Slider hiển thị tiến độ load (0..1).")]
        [SerializeField] private Slider loadingProgressBar;

        [Tooltip("Optional: Text hiển thị % load. (TMP_Text)")]
        [SerializeField] private TMP_Text loadingPercentText;

        [Header("Timing (Realtime)")]
        [Tooltip("Loading panel phải xuất hiện tối thiểu bấy nhiêu giây trước khi bắt đầu FADE IN.")]
        [SerializeField] private float minShowBeforeFadeInSeconds = 2.5f;

        [Tooltip("Fade IN (sáng lên) để che màn hình trước khi activate scene.")]
        [SerializeField] private float fadeInSeconds = 0.35f;

        [Tooltip("Fade OUT (mờ dần) để lộ gameplay.")]
        [SerializeField] private float fadeOutSeconds = 0.65f;

        [Header("Progress UX (Realtime)")]
        [Tooltip("Độ mượt khi progress chạy (giây). Tăng lên = chạy chậm/mượt hơn.")]
        [SerializeField] private float progressSmoothTime = 0.20f;

        [Tooltip("Khi scene đã load xong (op.progress>=0.9), thời gian để fill từ 90% -> 100%.")]
        [SerializeField] private float fillToHundredSeconds = 0.75f;

        [Tooltip("Clamp mục tiêu tối đa trước khi 'Ready' (để tránh nhảy 100% sớm).")]
        [Range(0.90f, 0.999f)]
        [SerializeField] private float preReadyCap = 0.99f;

        [Header("Warmup")]
        [SerializeField] private bool warmupAllShaders = true;
        [SerializeField] private int extraSettleFrames = 2;

        [Header("Audio")]
        [SerializeField] private bool pauseAudioDuringLoad = true;

        [Header("Camera Safety")]
        [SerializeField] private bool ensureTempCameraDuringLoad = true;
        [SerializeField] private bool forceAllCamerasToDisplay1 = true;

        private Camera _loadingCam;

        // progress smoothing state
        private float _displayedProgress01;
        private float _progressVelocity;

        protected override void Awake()
        {
            if (!CheckInstance()) return;
            base.Awake();

            DontDestroyOnLoad(gameObject);

            // Auto-wire optional UI if user forgot to assign (safe, non-breaking)
            if (!fadeImage && loadingOverlay)
                fadeImage = loadingOverlay.GetComponentInChildren<Image>(true);

            if (!loadingProgressBar && loadingOverlay)
                loadingProgressBar = loadingOverlay.GetComponentInChildren<Slider>(true);

            if (!loadingPercentText && loadingOverlay)
                loadingPercentText = loadingOverlay.GetComponentInChildren<TMP_Text>(true);

            if (loadingOverlay) loadingOverlay.SetActive(false);
            SetFadeAlpha(0f);

            _displayedProgress01 = 0f;
            _progressVelocity = 0f;
            ApplyProgressUI(0f);
        }

        public void StartNewGame()
        {
            StartCoroutine(CoStartGameAdditive(isNewGame: true));
        }

        public void ContinueGame()
        {
            StartCoroutine(CoStartGameAdditive(isNewGame: false));
        }

        private IEnumerator CoStartGameAdditive(bool isNewGame)
        {
            Time.timeScale = 1f;

            // Cache current menu scene handle BEFORE anything changes
            Scene menuScene = SceneManager.GetActiveScene();

            if (pauseAudioDuringLoad)
                AudioListener.pause = true;

            if (ensureTempCameraDuringLoad)
                EnsureLoadingCamera();

            if (forceAllCamerasToDisplay1)
                ForceAllCamerasToDisplay1();

            // 1) Show loading panel immediately (but do NOT fade-in yet)
            if (loadingOverlay) loadingOverlay.SetActive(true);

            // Ensure fade image starts fully transparent so player can see loading panel
            SetFadeAlpha(0f);

            _displayedProgress01 = 0f;
            _progressVelocity = 0f;
            ApplyProgressUI(0f);

            // Let UI render at least one frame
            yield return null;

            float showStart = Time.realtimeSinceStartup;

            // 2) Begin loading Gameplay ADDITIVE, but hold activation (NotifyLoadingGame style)
            AsyncOperation op = SceneManager.LoadSceneAsync(gameplaySceneName, LoadSceneMode.Additive);
            op.allowSceneActivation = false;

            bool sceneReady = false;

            // 3) While loading (0..0.9), update progress UI smoothly (0..~99%)
            while (!sceneReady)
            {
                // op.progress goes 0..0.9 then waits for allowSceneActivation
                float raw01 = Mathf.Clamp01(op.progress / 0.9f);

                if (op.progress >= 0.9f)
                    sceneReady = true;

                // Target is raw progress, but capped (avoid reaching 100% before "Ready")
                float target01 = Mathf.Min(raw01, preReadyCap);

                // Smooth displayed progress toward target
                _displayedProgress01 = Mathf.SmoothDamp(
                    _displayedProgress01,
                    target01,
                    ref _progressVelocity,
                    Mathf.Max(0.0001f, progressSmoothTime),
                    Mathf.Infinity,
                    Time.unscaledDeltaTime
                );

                ApplyProgressUI(_displayedProgress01);
                yield return null;
            }

            // 4) Now that scene is ready-to-activate (op.progress>=0.9),
            // do a controlled fill to 100% so UX doesn't "teleport".
            float fillStart = Time.realtimeSinceStartup;
            float fillEnd = fillStart + Mathf.Max(0.01f, fillToHundredSeconds);
            float startFillFrom = Mathf.Max(_displayedProgress01, 0.90f);

            while (Time.realtimeSinceStartup < fillEnd)
            {
                float t = Mathf.InverseLerp(fillStart, fillEnd, Time.realtimeSinceStartup);
                _displayedProgress01 = Mathf.Lerp(startFillFrom, 1f, t);
                ApplyProgressUI(_displayedProgress01);
                yield return null;
            }

            _displayedProgress01 = 1f;
            ApplyProgressUI(1f);

            // 5) Enforce minimum time showing the loading panel BEFORE starting fade-in
            float minEnd = showStart + Mathf.Max(0f, minShowBeforeFadeInSeconds);
            while (Time.realtimeSinceStartup < minEnd)
                yield return null;

            // 6) NOW start Fade IN to cover the screen
            // During fade-in, allow scene activation ONCE (only after we displayed 100%)
            bool activated = false;
            yield return FadeTo(1f, fadeInSeconds, onDuringFade: () =>
            {
                if (!activated)
                {
                    activated = true;
                    op.allowSceneActivation = true;
                }
            });

            // 7) Wait until activation completes
            while (!op.isDone)
                yield return null;

            // 8) Set active scene to gameplay
            Scene gameplayScene = SceneManager.GetSceneByName(gameplaySceneName);
            if (gameplayScene.IsValid())
                SceneManager.SetActiveScene(gameplayScene);

            if (forceAllCamerasToDisplay1)
                ForceAllCamerasToDisplay1();

            // Helps URP/ambient sometimes after additive activation
            DynamicGI.UpdateEnvironment();

            // Settle frames for Awake/Start/layout/cameras
            for (int i = 0; i < Mathf.Max(0, extraSettleFrames); i++)
                yield return null;

            if (ensureTempCameraDuringLoad)
            {
                yield return WaitForGameplayCameraReady(gameplaySceneName, 5f);
                DisableLoadingCamera();
            }

            Time.timeScale = 1f;

            // 9) Load data + init while screen is still covered
            if (isNewGame && SaveLoadManager.HasInstance)
                SaveLoadManager.Instance.DeleteSaveData();

            if (SaveLoadManager.HasInstance)
                SaveLoadManager.Instance.LoadAll(treatMissingAsNewGame: true);

            if (GameManager.HasInstance)
                GameManager.Instance.InitGameplayCore(isNewGame);

            // 10) Warmup shaders (avoid in Editor to reduce hitch while testing)
            if (warmupAllShaders && !Application.isEditor)
                Shader.WarmupAllShaders();

            // 11) Unload menu scene (overlay stays because it's under DontDestroy bootstrapper)
            if (menuScene.IsValid())
            {
                AsyncOperation unload = SceneManager.UnloadSceneAsync(menuScene);
                if (unload != null)
                    while (!unload.isDone) yield return null;
            }

            // Ensure one frame with gameplay ready but still covered
            yield return null;

            // 12) Fade OUT smoothly to reveal gameplay
            yield return FadeTo(0f, fadeOutSeconds);

            // 13) Hide loading panel after fade out finishes
            if (loadingOverlay) loadingOverlay.SetActive(false);

            if (pauseAudioDuringLoad)
                AudioListener.pause = false;
        }

        private void ApplyProgressUI(float normalized01)
        {
            normalized01 = Mathf.Clamp01(normalized01);

            if (loadingProgressBar)
                loadingProgressBar.value = normalized01;

            if (loadingPercentText)
                loadingPercentText.text = $"{Mathf.RoundToInt(normalized01 * 100f)}%";
        }

        private void SetFadeAlpha(float a)
        {
            if (!fadeImage) return;
            Color c = fadeImage.color;
            c.a = Mathf.Clamp01(a);
            fadeImage.color = c;
        }

        /// <summary>
        /// Fade fadeImage alpha to targetAlpha in realtime (unscaled).
        /// Optional onDuringFade executes every frame during the fade (useful for allowSceneActivation).
        /// </summary>
        private IEnumerator FadeTo(float targetAlpha, float seconds, Action onDuringFade = null)
        {
            if (!fadeImage) yield break;

            float startAlpha = fadeImage.color.a;

            if (seconds <= 0.0001f)
            {
                SetFadeAlpha(targetAlpha);
                yield break;
            }

            float startTime = Time.realtimeSinceStartup;
            float endTime = startTime + seconds;

            while (Time.realtimeSinceStartup < endTime)
            {
                onDuringFade?.Invoke();

                float t = Mathf.InverseLerp(startTime, endTime, Time.realtimeSinceStartup);
                float a = Mathf.Lerp(startAlpha, targetAlpha, t);
                SetFadeAlpha(a);

                yield return null;
            }

            SetFadeAlpha(targetAlpha);
        }

        // -------------------------
        // Camera safety (fix: Display 1 no cameras rendering)
        // -------------------------

        private void EnsureLoadingCamera()
        {
            if (_loadingCam != null) return;

            GameObject go = new GameObject("Bootstrapper_LoadingCamera");
            DontDestroyOnLoad(go);

            _loadingCam = go.AddComponent<Camera>();
            _loadingCam.clearFlags = CameraClearFlags.SolidColor;
            _loadingCam.backgroundColor = Color.black;
            _loadingCam.cullingMask = 0;   // render nothing
            _loadingCam.depth = 999;
            _loadingCam.enabled = true;
            _loadingCam.targetDisplay = 0; // Display 1
        }

        private void DisableLoadingCamera()
        {
            if (_loadingCam == null) return;
            _loadingCam.enabled = false;
            Destroy(_loadingCam.gameObject);
            _loadingCam = null;
        }

        private IEnumerator WaitForGameplayCameraReady(string sceneName, float timeoutSeconds = 5f)
        {
            float start = Time.realtimeSinceStartup;

            while (Time.realtimeSinceStartup - start < timeoutSeconds)
            {
                Camera[] cams = GameObject.FindObjectsOfType<Camera>(true);
                for (int i = 0; i < cams.Length; i++)
                {
                    Camera cam = cams[i];
                    if (!cam) continue;
                    if (!cam.enabled) continue;
                    if (!cam.gameObject.activeInHierarchy) continue;

                    if (cam.gameObject.scene.name == sceneName)
                        yield break;
                }

                yield return null;
            }

            Debug.LogWarning("[Bootstrapper] Timeout waiting for gameplay camera. Ensure GameplayScene has an enabled Camera.");
        }

        private void ForceAllCamerasToDisplay1()
        {
            var cams = GameObject.FindObjectsOfType<Camera>(true);
            foreach (var cam in cams)
            {
                if (!cam) continue;
                cam.targetDisplay = 0; // Display 1
            }
        }
    }
}
