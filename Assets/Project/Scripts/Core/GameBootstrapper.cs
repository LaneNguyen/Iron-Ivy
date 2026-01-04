using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using IronIvy.UI;

namespace IronIvy.Core
{
    public class GameBootstrapper : MonoBehaviour
    {
        public static GameBootstrapper Instance { get; private set; }

        [Header("Scene Config")]
        [SerializeField] private string gameSceneName = "IvyIsland";
        [SerializeField] private string loadingSceneName = "LoadingScreen";
        
        [Header("Fake Loading")]
        [SerializeField] private float fakeLoadingDuration = 5f; // Thời gian loading tối thiểu

        public float loadProgress;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else Destroy(gameObject);
        }

        public static bool HasInstance => Instance != null;

        public void StartNewGame()
        {
            StopAllCoroutines();
            StartCoroutine(LoadGameFlow());
        }

        private IEnumerator LoadGameFlow()
        {
            // BƯỚC 1: Flash trắng tại StartScene
            if (ScreenFader.Instance != null)
                yield return ScreenFader.Instance.FadeOut();

            // BƯỚC 2: Tải scene Loading
            AsyncOperation loadingOp = SceneManager.LoadSceneAsync(loadingSceneName);
            while (!loadingOp.isDone) yield return null;

            if (ScreenFader.Instance != null)
                yield return ScreenFader.Instance.FadeIn();

            // BƯỚC 3: Bắt đầu tải ngầm scene Game và chạy thời gian giả
            loadProgress = 0f;
            AsyncOperation gameOp = SceneManager.LoadSceneAsync(gameSceneName);
            gameOp.allowSceneActivation = false;

            float timer = 0f;

            // Vòng lặp này chạy cho đến khi ĐỦ 5 giây VÀ Scene đã load xong thực tế
            while (timer < fakeLoadingDuration || gameOp.progress < 0.9f)
            {
                timer += Time.deltaTime;
                
                // Tính toán % dựa trên thời gian trôi qua
                float timeProgress = timer / fakeLoadingDuration;
                
                // Nếu load thực tế chưa xong nhưng thời gian giả đã gần hết, giữ ở 99%
                if (timeProgress >= 0.99f && gameOp.progress < 0.9f)
                {
                    loadProgress = 0.99f;
                }
                else
                {
                    loadProgress = Mathf.Clamp01(timeProgress);
                }

                yield return null;
            }

            // Đảm bảo hiện 100% trước khi chuyển
            loadProgress = 1f;
            yield return new WaitForSeconds(0.2f); 

            // BƯỚC 4: Flash trắng để đổi Scene
            if (ScreenFader.Instance != null)
                yield return ScreenFader.Instance.FadeOut();

            gameOp.allowSceneActivation = true;
            while (!gameOp.isDone) yield return null;

            if (ScreenFader.Instance != null)
                yield return ScreenFader.Instance.FadeIn();
        }
    }
}