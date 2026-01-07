using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IronIvy.Core
{
    public class GameBootstrapper : MonoBehaviour
    {
        public static GameBootstrapper Instance { get; private set; }
        public static bool HasInstance => Instance != null;

        [Header("Scene Config")]
        [SerializeField] private string gameSceneName = "IvyIsland";
        [SerializeField] private string loadingSceneName = "LoadingScreen";

        [Header("Fake Loading")]
        [SerializeField] private float fakeLoadingDuration = 5f;

        [Header("Boot Camera (anti-flicker)")]
        [SerializeField] private string bootEstablishCameraId = "EstablishCam";

        [Header("Intro Handshake")]
        [Tooltip("Giữ màn hình đen tới khi IntroFlow thật sự director.Play(). Timeout để tránh kẹt nếu intro lỗi.")]
        [SerializeField] private float introStartTimeout = 3f;

        public float loadProgress;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void StartNewGame()
        {
            StopAllCoroutines();
            StartCoroutine(LoadGameFlow());
        }

        private IEnumerator LoadGameFlow()
        {
            // Step 1: FadeOut at StartScene (optional, if fader exists)
            yield return TryFade("FadeOut");

            // Step 2: Load Loading scene (Single)
            AsyncOperation loadingOp = SceneManager.LoadSceneAsync(loadingSceneName, LoadSceneMode.Single);
            while (!loadingOp.isDone) yield return null;

            // Show loading UI
            yield return TryFade("FadeIn");

            // Step 3: Load Game scene (Single, hold activation) + fake loading time
            loadProgress = 0f;
            AsyncOperation gameOp = SceneManager.LoadSceneAsync(gameSceneName, LoadSceneMode.Single);
            gameOp.allowSceneActivation = false;

            float timer = 0f;
            while (timer < fakeLoadingDuration || gameOp.progress < 0.9f)
            {
                timer += Time.deltaTime;

                float timeProgress = timer / Mathf.Max(0.01f, fakeLoadingDuration);

                if (timeProgress >= 0.99f && gameOp.progress < 0.9f)
                    loadProgress = 0.99f;
                else
                    loadProgress = Mathf.Clamp01(timeProgress);

                yield return null;
            }

            loadProgress = 1f;
            yield return new WaitForSeconds(0.2f);

            // Step 4: FadeOut to switch (back to black)
            yield return TryFade("FadeOut");

            // Activate Game scene while still black
            gameOp.allowSceneActivation = true;
            while (!gameOp.isDone) yield return null;

            // Make sure active scene is the game scene
            Scene gameScene = SceneManager.GetSceneByName(gameSceneName);
            if (gameScene.IsValid())
                SceneManager.SetActiveScene(gameScene);

            // Give 1–2 frames for scene objects to Awake/OnEnable (still black)
            yield return null;
            yield return null;

            // Set a safe boot camera while still black (NOT gameplay cam)
            if (ListenManager.HasInstance)
            {
                ListenManager.Instance.RaiseCameraSwitchRequested(
                    new ListenManager.CameraSwitchRequestPayload(bootEstablishCameraId, false)
                );
            }

            // IMPORTANT: Tell game scene "entered" while still black,
            // so IntroFlow can switch to intro cam + start timeline deterministically.
            yield return null;

            bool introStarted = false;

            if (ListenManager.HasInstance)
            {
                void MarkIntroStarted() => introStarted = true;

                // Subscribe BEFORE raise, tránh miss signal nếu IntroFlow Play nhanh
                ListenManager.Instance.OnIntroTimelineStarted += MarkIntroStarted;

                ListenManager.Instance.RaiseGameSceneEntered();
                Debug.Log("[Bootstrapper] RaiseGameSceneEntered fired (still black)");

                // Chờ IntroFlow confirm director.Play()
                float t = 0f;
                while (!introStarted && t < introStartTimeout)
                {
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }

                ListenManager.Instance.OnIntroTimelineStarted -= MarkIntroStarted;
            }
            else
            {
                // fallback giữ nguyên như trước: đợi 2 frame rồi fade in
                yield return null;
                yield return null;
            }

            // NOW fade in (timeline should be running already)
            yield return TryFade("FadeIn");
        }

        private IEnumerator TryFade(string methodName)
        {
            // Find any component that looks like a fader (by type name or by method existence)
            MonoBehaviour[] behaviours = FindObjectsOfType<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour b = behaviours[i];
                if (b == null) continue;

                System.Type t = b.GetType();

                // Prefer a component literally named ScreenFader, but allow any fader that has FadeIn/FadeOut
                bool nameMatch = t.Name == "ScreenFader";
                MethodInfo m = t.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (m == null) continue;
                if (!nameMatch && t.GetMethod("FadeIn", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) == null) continue;
                if (!nameMatch && t.GetMethod("FadeOut", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) == null) continue;

                object result = null;

                try
                {
                    // Support FadeIn()/FadeOut() with no params
                    result = m.GetParameters().Length == 0 ? m.Invoke(b, null) : null;
                }
                catch
                {
                    yield break;
                }

                if (result is IEnumerator ie)
                    yield return StartCoroutine(ie);

                yield break;
            }

            yield break;
        }
    }
}
