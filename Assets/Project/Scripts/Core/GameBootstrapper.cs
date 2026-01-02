using System;
using System.Collections;
using TMPro;
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
        [SerializeField] private GameObject loadingOverlay; // Panel gốc (lý tưởng nhất là nằm dưới BootstrapperRoot - DontDestroy)
        [SerializeField] private Image fadeImage;           // Ảnh toàn màn hình (con của overlay)

        [Header("Loading UI (Optional)")]
        [Tooltip("Tùy chọn: Slider hiển thị tiến độ load (0..1).")]
        [SerializeField] private Slider loadingProgressBar;

        [Tooltip("Tùy chọn: Text hiển thị % load. (TMP_Text)")]
        [SerializeField] private TMP_Text loadingPercentText;

        [Header("Timing (Realtime)")]
        [Tooltip("Loading panel phải xuất hiện tối thiểu bấy nhiêu giây trước khi bắt đầu FADE IN.")]
        [SerializeField] private float minShowBeforeFadeInSeconds = 2.5f;

        [Tooltip("Fade IN (sáng lên/đen đặc) để che màn hình trước khi kích hoạt scene.")]
        [SerializeField] private float fadeInSeconds = 0.35f;

        [Tooltip("Fade OUT (mờ dần) để lộ gameplay.")]
        [SerializeField] private float fadeOutSeconds = 0.65f;

        [Header("Progress UX (Realtime)")]
        [Tooltip("Độ mượt khi thanh tiến trình chạy (giây). Tăng lên = chạy chậm/mượt hơn.")]
        [SerializeField] private float progressSmoothTime = 0.20f;

        [Tooltip("Khi scene đã load xong (op.progress>=0.9), thời gian để chạy nốt từ 90% -> 100%.")]
        [SerializeField] private float fillToHundredSeconds = 0.75f;

        [Tooltip("Giới hạn mục tiêu tối đa trước khi 'Sẵn sàng' (để tránh nhảy lên 100% quá sớm).")]
        [Range(0.90f, 0.999f)]
        [SerializeField] private float preReadyCap = 0.99f;

        [Header("Warmup")]
        [SerializeField] private bool warmupAllShaders = true;
        [SerializeField] private int extraSettleFrames = 2;

        [Header("Audio")]
        [SerializeField] private bool pauseAudioDuringLoad = true;

        // NEW: Nếu em xoá AudioManager khỏi các scene, bootstrapper sẽ đảm bảo có AudioManager sống.
        [Tooltip("Optional: Prefab AudioManager (có sẵn AttachBGMSource/AttachSESource). Nếu bỏ trống, sẽ auto-create runtime.")]
        [SerializeField] private AudioManager audioManagerPrefab;

        [Tooltip("BGM mặc định khi vào GameplayScene (tên clip trong Resources/Audio/BGM).")]
        [SerializeField] private string gameplayDefaultBGMName = "";

        [Header("Camera Safety")]
        [SerializeField] private bool ensureTempCameraDuringLoad = true;
        [SerializeField] private bool forceAllCamerasToDisplay1 = true;

        private Camera _loadingCam;

        // Trạng thái làm mượt tiến trình
        private float _displayedProgress01;
        private float _progressVelocity;

        protected override void Awake()
        {
            if (!CheckInstance()) return;
            base.Awake();

            DontDestroyOnLoad(gameObject);

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

            // NEW: đảm bảo AudioManager tồn tại ngay từ đầu (nếu scene menu đã xoá / sau này unload menu)
            EnsureAudioManagerExists();
        }

        public void StartNewGame()
        {
            if (AudioManager.HasInstance)
        AudioManager.Instance.PlayInterfaceSE();
            StartCoroutine(CoStartGameAdditive(isNewGame: true));
             
        }

        public void ContinueGame()
        {
            if (AudioManager.HasInstance)
        AudioManager.Instance.PlayInterfaceSE();
            StartCoroutine(CoStartGameAdditive(isNewGame: false));
        }

        private IEnumerator CoStartGameAdditive(bool isNewGame)
        {
            Time.timeScale = 1f;

            // Ensure audio trước khi làm gì khác (đặc biệt trước khi unload menu)
            EnsureAudioManagerExists();

            Scene menuScene = SceneManager.GetActiveScene();

            if (pauseAudioDuringLoad)
                AudioListener.pause = true;

            if (ensureTempCameraDuringLoad)
                EnsureLoadingCamera();

            if (forceAllCamerasToDisplay1)
                ForceAllCamerasToDisplay1();

            if (loadingOverlay) loadingOverlay.SetActive(true);
            SetFadeAlpha(0f);

            _displayedProgress01 = 0f;
            _progressVelocity = 0f;
            ApplyProgressUI(0f);

            yield return null;

            float showStart = Time.realtimeSinceStartup;

            AsyncOperation op = SceneManager.LoadSceneAsync(gameplaySceneName, LoadSceneMode.Additive);
            op.allowSceneActivation = false;

            bool sceneReady = false;

            while (!sceneReady)
            {
                float raw01 = Mathf.Clamp01(op.progress / 0.9f);

                if (op.progress >= 0.9f)
                    sceneReady = true;

                float target01 = Mathf.Min(raw01, preReadyCap);

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

            float minEnd = showStart + Mathf.Max(0f, minShowBeforeFadeInSeconds);
            while (Time.realtimeSinceStartup < minEnd)
                yield return null;

            bool activated = false;
            yield return FadeTo(1f, fadeInSeconds, onDuringFade: () =>
            {
                if (!activated)
                {
                    activated = true;
                    op.allowSceneActivation = true;
                }
            });

            while (!op.isDone)
                yield return null;

            Scene gameplayScene = SceneManager.GetSceneByName(gameplaySceneName);
            if (gameplayScene.IsValid())
                SceneManager.SetActiveScene(gameplayScene);

            if (forceAllCamerasToDisplay1)
                ForceAllCamerasToDisplay1();

            DynamicGI.UpdateEnvironment();

            for (int i = 0; i < Mathf.Max(0, extraSettleFrames); i++)
                yield return null;

            if (ensureTempCameraDuringLoad)
            {
                yield return WaitForGameplayCameraReady(gameplaySceneName, 5f);

                // Sau khi gameplay camera đã xuất hiện, chỉ giữ đúng 1 listener trên camera gameplay đang enable
                var cams = GameObject.FindObjectsOfType<Camera>(true);
                Camera chosen = null;
                for (int i = 0; i < cams.Length; i++)
                {
                    if (!cams[i]) continue;
                    if (!cams[i].enabled) continue;
                    if (!cams[i].gameObject.activeInHierarchy) continue;

                    // Ưu tiên camera thuộc gameplay scene
                    if (cams[i].gameObject.scene.name == gameplaySceneName)
                    {
                        chosen = cams[i];
                        break;
                    }
                }

                // Sau khi gameplay camera đã xuất hiện: tắt hết listener trước, rồi bật đúng 1 cái trên chosen
                DisableAllAudioListeners();

                if (chosen != null)
                {
                    var gameplayListener = EnsureListenerOnCamera(chosen);
                    DisableAllAudioListenersExcept(gameplayListener);
                }
                else
                {
                    Debug.LogWarning("[Bootstrapper] Không tìm thấy camera gameplay để gắn AudioListener.");
                }


                DisableLoadingCamera();
            }

            Time.timeScale = 1f;

            // NEW: Set BGM của gameplay ngay sau khi scene mới đã active
            EnsureAudioManagerExists();
            if (!string.IsNullOrEmpty(gameplayDefaultBGMName) && AudioManager.Instance != null)
            {
                AudioManager.Instance.RequestSceneDefaultBGM(gameplayDefaultBGMName);
            }

            if (isNewGame && SaveLoadManager.HasInstance)
                SaveLoadManager.Instance.DeleteSaveData();

            if (SaveLoadManager.HasInstance)
                SaveLoadManager.Instance.LoadAll(treatMissingAsNewGame: true);

            if (GameManager.HasInstance)
                GameManager.Instance.InitGameplayCore(isNewGame);

            if (warmupAllShaders && !Application.isEditor)
                Shader.WarmupAllShaders();

            // Ensure audio trước khi unload menu để tránh trường hợp AudioManager còn nằm trong menuScene
            EnsureAudioManagerExists();

            if (menuScene.IsValid())
            {
                AsyncOperation unload = SceneManager.UnloadSceneAsync(menuScene);
                if (unload != null)
                    while (!unload.isDone) yield return null;
            }

            yield return null;

            yield return FadeTo(0f, fadeOutSeconds);

            if (loadingOverlay) loadingOverlay.SetActive(false);

            if (pauseAudioDuringLoad)
                AudioListener.pause = false;
        }

        // =========================
        // NEW: Audio safety for additive flow
        // =========================
        private void EnsureAudioManagerExists()
        {
            if (AudioManager.Instance != null)
            {
                // Nếu có rồi thì đảm bảo nó không bị mất AudioSource reference
                if (AudioManager.Instance.AttachBGMSource == null)
                    AudioManager.Instance.AttachBGMSource = AudioManager.Instance.GetComponentInChildren<AudioSource>(true);

                if (AudioManager.Instance.AttachSESource == null)
                {
                    // nếu chỉ có 1 AudioSource, tạo thêm 1 cái cho SE
                    var sources = AudioManager.Instance.GetComponentsInChildren<AudioSource>(true);
                    if (sources != null && sources.Length >= 2)
                        AudioManager.Instance.AttachSESource = sources[1];
                }

                return;
            }

            // Không có AudioManager -> spawn mới (prefab nếu có, không thì auto-create)
            AudioManager mgr = null;

            if (audioManagerPrefab != null)
            {
                mgr = Instantiate(audioManagerPrefab);
            }
            else
            {
                GameObject go = new GameObject("AudioManager(Runtime)");
                mgr = go.AddComponent<AudioManager>();

                // Tạo 2 AudioSource tối thiểu để AudioManager chạy được
                var bgm = go.AddComponent<AudioSource>();
                bgm.loop = true;
                bgm.playOnAwake = false;

                var se = go.AddComponent<AudioSource>();
                se.loop = false;
                se.playOnAwake = false;

                mgr.AttachBGMSource = bgm;
                mgr.AttachSESource = se;
            }
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

       private void EnsureLoadingCamera()
        {
            if (_loadingCam != null) return;

            // Tắt tai nghe ở scene cũ trước
            DisableAllAudioListeners();

            GameObject go = new GameObject("Bootstrapper_LoadingCamera");
            DontDestroyOnLoad(go);

            _loadingCam = go.AddComponent<Camera>();
            _loadingCam.clearFlags = CameraClearFlags.SolidColor;
            _loadingCam.backgroundColor = Color.black;
            _loadingCam.cullingMask = 0;
            _loadingCam.depth = 999;
            _loadingCam.enabled = true;
            _loadingCam.targetDisplay = 0;

            // --- FIX: THÊM DÒNG NÀY ---
            // Gắn tạm tai nghe vào camera loading để nhạc vẫn nghe được lúc chuyển cảnh
            go.AddComponent<AudioListener>(); 
            // --------------------------
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

            Debug.LogWarning("[Bootstrapper] Timeout chờ camera gameplay. Hãy đảm bảo GameplayScene có một Camera đang bật.");
        }

        private void ForceAllCamerasToDisplay1()
        {
            var cams = GameObject.FindObjectsOfType<Camera>(true);
            foreach (var cam in cams)
            {
                if (!cam) continue;
                cam.targetDisplay = 0;
            }
        }

        private void DisableAllAudioListenersExcept(AudioListener keep)
        {
            var listeners = GameObject.FindObjectsOfType<AudioListener>(true);
            for (int i = 0; i < listeners.Length; i++)
            {
                var l = listeners[i];
                if (!l) continue;
                l.enabled = (l == keep);
            }
        }

        private AudioListener EnsureListenerOnCamera(Camera cam)
        {
            if (!cam) return null;

            // Nếu cam có sẵn listener thì dùng luôn
            var l = cam.GetComponent<AudioListener>();
            if (!l) l = cam.gameObject.AddComponent<AudioListener>();
            l.enabled = true;
            return l;
        }

        private void DisableAllAudioListeners()
        {
            var listeners = GameObject.FindObjectsOfType<AudioListener>(true);
            for (int i = 0; i < listeners.Length; i++)
            {
                var l = listeners[i];
                if (!l) continue;
                l.enabled = false;
            }
        }


    }
}
