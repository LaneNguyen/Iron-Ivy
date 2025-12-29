using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace IronIvy.Core
{
    public class GameBootstrapper : BaseManager<GameBootstrapper>
    {
        [Header("Scenes")]
        [SerializeField] private string gameplaySceneName = "GameplayScene";

        [Header("Overlay (Active/Disable)")]
        [SerializeField] private GameObject loadingOverlay; // Panel root (must be under BootstrapperRoot - DontDestroy)
        [SerializeField] private Image fadeImage;           // Fullscreen Image (child of overlay)

        [Header("Timing (Realtime)")]
        [Tooltip("Loading panel phải xuất hiện tối thiểu bấy nhiêu giây trước khi bắt đầu FADE IN.")]
        [SerializeField] private float minShowBeforeFadeInSeconds = 2.5f;

        [Tooltip("Fade IN (sáng lên) để che màn hình trước khi activate scene.")]
        [SerializeField] private float fadeInSeconds = 0.35f;

        [Tooltip("Fade OUT (mờ dần) để lộ gameplay.")]
        [SerializeField] private float fadeOutSeconds = 0.65f;

        [Header("Warmup")]
        [SerializeField] private bool warmupAllShaders = true;
        [SerializeField] private int extraSettleFrames = 2;

        [Header("Audio")]
        [SerializeField] private bool pauseAudioDuringLoad = false;

        [Header("Lifecycle")]
[SerializeField] private bool destroyBootstrapperAfterEnterGameplay = false;

        private Camera _loadingCam;

        protected override void Awake()
        {
            if (!CheckInstance()) return;
            base.Awake();

            DontDestroyOnLoad(gameObject);

            if (loadingOverlay) loadingOverlay.SetActive(false);

            // Start with no fade overlay visible
            SetFadeAlpha(0f);
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

            
            EnsureLoadingCamera();

            Time.timeScale = 1f;

            // Cache current menu scene handle BEFORE anything changes
            Scene menuScene = SceneManager.GetActiveScene();

            if (pauseAudioDuringLoad)
                AudioListener.pause = true;

            // 1) Show loading panel immediately (but do NOT fade-in yet)
            if (loadingOverlay) loadingOverlay.SetActive(true);

            // Ensure fade image starts fully transparent so player can see loading panel
            SetFadeAlpha(0f);

            // Let UI render at least one frame
            yield return null;

            float showStart = Time.realtimeSinceStartup;

            // 2) Begin loading Gameplay ADDITIVE, but hold activation
            AsyncOperation op = SceneManager.LoadSceneAsync(gameplaySceneName, LoadSceneMode.Additive);
            op.allowSceneActivation = false;

            // 3) Wait until Unity finishes loading (progress reaches 0.9)
            // (0.9 means "ready to activate", waiting for allowSceneActivation = true)
            while (op.progress < 0.9f)
            {
                yield return null;
            }

            // 4) Enforce minimum time showing the loading panel BEFORE starting fade-in
            float minEnd = showStart + Mathf.Max(0f, minShowBeforeFadeInSeconds);
            while (Time.realtimeSinceStartup < minEnd)
                yield return null;

            // 5) NOW start Fade IN to cover the screen
            // During fade-in, allow scene activation ONCE
            bool activated = false;
            yield return FadeTo(1f, fadeInSeconds, onDuringFade: () =>
            {
                if (!activated)
                {
                    activated = true;
                    op.allowSceneActivation = true;
                }
            });

            // 6) Wait until activation completes
            while (!op.isDone)
                yield return null;

            // 7) Set active scene to gameplay
            Scene gameplayScene = SceneManager.GetSceneByName(gameplaySceneName);
            if (gameplayScene.IsValid())
                SceneManager.SetActiveScene(gameplayScene);

            // Helps URP/ambient sometimes after additive activation
            DynamicGI.UpdateEnvironment();

            // Settle frames for Awake/Start/layout/cameras
            for (int i = 0; i < Mathf.Max(0, extraSettleFrames); i++)
                yield return null;

            yield return WaitForGameplayCameraReady(gameplaySceneName, 5f);
            DisableLoadingCamera();
            Time.timeScale = 1f;

            // 8) Load data + init while screen is still covered
            if (isNewGame && SaveLoadManager.HasInstance)
                SaveLoadManager.Instance.DeleteSaveData();

            if (SaveLoadManager.HasInstance)
                SaveLoadManager.Instance.LoadAll(treatMissingAsNewGame: true);

            if (GameManager.HasInstance)
                GameManager.Instance.InitGameplayCore(isNewGame);

            // 9) Warmup shaders (avoid in Editor to reduce hitch while testing)
            if (warmupAllShaders && !Application.isEditor)
                Shader.WarmupAllShaders();

            // 10) Unload menu scene (overlay stays because it's under DontDestroy bootstrapper)
            if (menuScene.IsValid())
            {
                AsyncOperation unload = SceneManager.UnloadSceneAsync(menuScene);
                if (unload != null)
                    while (!unload.isDone) yield return null;
            }

            // Ensure one frame with gameplay ready but still covered
            yield return null;

            // 11) Fade OUT smoothly to reveal gameplay
            yield return FadeTo(0f, fadeOutSeconds);

         // 12) Hide loading panel after fade out finishes
if (loadingOverlay) loadingOverlay.SetActive(false);

if (pauseAudioDuringLoad)
    AudioListener.pause = false;

// OPTIONAL: destroy bootstrapper object if you want it gone after entering gameplay
if (destroyBootstrapperAfterEnterGameplay)
{
    Destroy(gameObject);
}


            if (pauseAudioDuringLoad)
                AudioListener.pause = false;
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

            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                onDuringFade?.Invoke();

                float a = Mathf.Lerp(startAlpha, targetAlpha, t / seconds);
                SetFadeAlpha(a);

                yield return null;
            }

            SetFadeAlpha(targetAlpha);
        }

        private void EnsureLoadingCamera()
        {
            if (_loadingCam != null) return;

            GameObject go = new GameObject("Bootstrapper_LoadingCamera");
            DontDestroyOnLoad(go);

            _loadingCam = go.AddComponent<Camera>();
            _loadingCam.clearFlags = CameraClearFlags.SolidColor;
            _loadingCam.backgroundColor = Color.black;
            _loadingCam.cullingMask = 0;   // không render world, chỉ cần tồn tại để tránh warning
            _loadingCam.depth = 999;
            _loadingCam.enabled = true;
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
                var cams = GameObject.FindObjectsOfType<Camera>(true);
                for (int i = 0; i < cams.Length; i++)
                {
                    Camera cam = cams[i];
                    if (!cam) continue;
                    if (!cam.enabled) continue;
                    if (!cam.gameObject.activeInHierarchy) continue;

                    // Chỉ cần có 1 camera thuộc gameplay scene là coi như OK
                    if (cam.gameObject.scene.name == sceneName)
                        yield break;
                }
                yield return null;
            }

            Debug.LogWarning("[Bootstrapper] Timeout waiting for gameplay camera. Check GameplayScene camera setup.");
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

private void LogCameras(string tag)
{
    var cams = GameObject.FindObjectsOfType<Camera>(true);
    Debug.Log($"[{tag}] Cameras count = {cams.Length}");
    foreach (var cam in cams)
    {
        if (!cam) continue;
        Debug.Log($"[{tag}] {cam.name} enabled={cam.enabled} active={cam.gameObject.activeInHierarchy} scene={cam.gameObject.scene.name} targetDisplay={cam.targetDisplay} depth={cam.depth}");
    }
}

    }
}
