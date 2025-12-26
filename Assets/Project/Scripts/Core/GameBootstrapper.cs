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

        [Header("Overlay refs")]
        [SerializeField] private GameObject loadingOverlay; // Panel root
        [SerializeField] private Image fadeImage;           // Fullscreen Image (child of overlay)

        [Header("Timing (Realtime)")]
        [SerializeField] private float delayBeforeFadeInSeconds = 2.5f; // muốn chờ 2-3s trước khi fade in
        [SerializeField] private float fadeInSeconds = 0.35f;
        [SerializeField] private float fadeOutSeconds = 0.65f;

        [Header("Options")]
        [SerializeField] private bool pauseAudioDuringLoad = true;
        [SerializeField] private bool warmupAllShaders = true;

        protected override void Awake()
        {
            if (!CheckInstance()) return;
            base.Awake();

            DontDestroyOnLoad(gameObject);

            AutoWireOverlay();

            if (loadingOverlay) loadingOverlay.SetActive(false);
            SetFadeAlpha(0f);

            Debug.Log("[Bootstrapper] Awake OK");
        }

        public void StartNewGame()
        {
            Debug.Log("[Bootstrapper] StartNewGame called");
            StartCoroutine(CoStartGameAdditive(isNewGame: true));
        }

        public void ContinueGame()
        {
            Debug.Log("[Bootstrapper] ContinueGame called");
            StartCoroutine(CoStartGameAdditive(isNewGame: false));
        }

        private void AutoWireOverlay()
        {
            // Nếu em quên assign, tự tìm theo name phổ biến
            if (!loadingOverlay)
            {
                var found = GameObject.Find("LoadingOverlay");
                if (found) loadingOverlay = found;
            }

            if (!fadeImage && loadingOverlay)
            {
                // Tìm Image fullscreen trong children (kể cả inactive)
                fadeImage = loadingOverlay.GetComponentInChildren<Image>(true);
            }

            if (!loadingOverlay)
                Debug.LogError("[Bootstrapper] loadingOverlay is NULL. Assign it in Inspector or name it 'LoadingOverlay' in scene.");

            if (!fadeImage)
                Debug.LogError("[Bootstrapper] fadeImage is NULL. Assign FadeImage (UI Image) under LoadingOverlay.");
        }

        private IEnumerator CoStartGameAdditive(bool isNewGame)
        {
            Time.timeScale = 1f;

            Scene menuScene = SceneManager.GetActiveScene();

            if (pauseAudioDuringLoad)
                AudioListener.pause = true;

            AutoWireOverlay();

            // 1) Bật overlay ngay
            if (loadingOverlay) loadingOverlay.SetActive(true);

            // Ép alpha = 1 trong 1 frame để đảm bảo em NHÌN THẤY overlay đang bật.
            // Sau đó mới đưa về 0 và chạy delay/fade theo đúng ý.
            SetFadeAlpha(1f);
            yield return null;

            // 2) Bây giờ đưa về 0 để chuẩn bị fade-in
            SetFadeAlpha(0f);

            // 3) Chờ 2-3s trước khi bắt đầu fade-in (đúng yêu cầu)
            if (delayBeforeFadeInSeconds > 0f)
                yield return WaitRealtime(delayBeforeFadeInSeconds);

            // 4) Fade IN che màn hình
            yield return FadeTo(1f, fadeInSeconds);

            // 5) Load gameplay ADDITIVE (backstage)
            AsyncOperation loadOp = SceneManager.LoadSceneAsync(gameplaySceneName, LoadSceneMode.Additive);
            loadOp.allowSceneActivation = true;
            while (!loadOp.isDone)
                yield return null;

            Scene gameplayScene = SceneManager.GetSceneByName(gameplaySceneName);
            if (gameplayScene.IsValid())
                SceneManager.SetActiveScene(gameplayScene);

            // settle 2 frames
            yield return null;
            yield return null;

            // 6) Load data + init
            if (isNewGame && SaveLoadManager.HasInstance)
                SaveLoadManager.Instance.DeleteSaveData();

            if (SaveLoadManager.HasInstance)
                SaveLoadManager.Instance.LoadAll(treatMissingAsNewGame: true);

            if (GameManager.HasInstance)
                GameManager.Instance.InitGameplayCore(isNewGame);

            // 7) Warmup shader (tắt trong Editor để đỡ hitch)
            if (warmupAllShaders && !Application.isEditor)
                Shader.WarmupAllShaders();

            // 8) Unload menu scene
            if (menuScene.IsValid())
            {
                var unloadOp = SceneManager.UnloadSceneAsync(menuScene);
                if (unloadOp != null)
                    while (!unloadOp.isDone) yield return null;
            }

            // 9) Fade OUT từ từ để lộ gameplay (đúng yêu cầu)
            yield return FadeTo(0f, fadeOutSeconds);

            if (loadingOverlay) loadingOverlay.SetActive(false);

            if (pauseAudioDuringLoad)
                AudioListener.pause = false;
        }

        private void SetFadeAlpha(float a)
        {
            if (!fadeImage) return;

            var c = fadeImage.color;
            c.a = Mathf.Clamp01(a);
            fadeImage.color = c;
        }

        private IEnumerator FadeTo(float targetAlpha, float seconds)
        {
            if (!fadeImage) yield break;

            float start = fadeImage.color.a;

            if (seconds <= 0.0001f)
            {
                SetFadeAlpha(targetAlpha);
                yield break;
            }

            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                float a = Mathf.Lerp(start, targetAlpha, t / seconds);
                SetFadeAlpha(a);
                yield return null;
            }

            SetFadeAlpha(targetAlpha);
        }

        private IEnumerator WaitRealtime(float seconds)
        {
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < seconds)
                yield return null;
        }
    }
}
