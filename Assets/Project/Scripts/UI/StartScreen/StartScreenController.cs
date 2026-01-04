using UnityEngine;
using UnityEngine.SceneManagement; // Bổ sung để điều khiển chuyển cảnh
using System.Collections;
using IronIvy.UI;

namespace IronIvy.Core
{
    public class StartScreenController : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private GameObject startScreenPanel;
        
        [Header("Scene Config")]
        [SerializeField] private string loadingSceneName = "LoadingScreen"; // Tên scene loading

        [Header("Optional")]
        [SerializeField] private bool pauseAtStart = true;

        // Hàm xử lý khi nhấn nút Start
        public void OnClickStart()
        {
            // Phát âm thanh nếu có AudioManager
            if (AudioManager.HasInstance)
                AudioManager.Instance.PlayInterfaceSE();

            // Ẩn bảng menu hiện tại
            //if (startScreenPanel != null)
                //startScreenPanel.SetActive(false);

    
        StartCoroutine(StartGameRoutine());
            // Kiểm tra và gọi Bootstrapper để quản lý logic load game
            if (GameBootstrapper.HasInstance)
            {
                // Yêu cầu Bootstrapper bắt đầu quá trình load (vào Loading Screen trước)
                GameBootstrapper.Instance.StartNewGame();
            }
            else
            {
                // Nếu không có Bootstrapper, thực hiện load scene cơ bản để tránh kẹt
                Debug.LogWarning("[StartScreenController] Không thấy GameBootstrapper. Đang load scene thủ công.");
                SceneManager.LoadScene(loadingSceneName);
            }
        }

        // Hàm xử lý khi nhấn nút Options
        public void OnClickOptions()
        {
            if (AudioManager.HasInstance)
                AudioManager.Instance.PlayInterfaceSE();

            Debug.Log("Options clicked.");
        }

        // Hàm xử lý khi nhấn nút Quit
        public void OnClickQuit()
        {
            if (AudioManager.HasInstance)
                AudioManager.Instance.PlayInterfaceSE();

            Debug.Log("Quit clicked.");
            
            #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
            #else
                Application.Quit();
            #endif
        }

        private void Awake()
        {
            // Thiết lập trạng thái thời gian khi bắt đầu
            if (pauseAtStart)
                Time.timeScale = 0f;
            else
                Time.timeScale = 1f;
        }
    private IEnumerator StartGameRoutine()
{
    // 1. Chờ màn hình đen hoàn toàn
    if (ScreenFader.Instance != null)
        yield return ScreenFader.Instance.FadeOut();

    // 2. NGAY LÚC NÀY: Ẩn Panel Menu đi để chắc chắn nó không xuất hiện ở scene sau
    if (startScreenPanel != null)
        startScreenPanel.SetActive(false);

    // 3. Gọi Bootstrapper để chuyển scene
    if (GameBootstrapper.HasInstance)
        GameBootstrapper.Instance.StartNewGame();
}
    }
}