using UnityEngine;

namespace IronIvy.Core
{
    public class StartScreenController : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private GameObject startScreenPanel;

        [Header("Optional")]
        [SerializeField] private bool pauseAtStart = true;

        public void OnClickStart()
        {
            if (startScreenPanel != null)
                startScreenPanel.SetActive(false);

            // Gọi bootstrapper để load/init đúng flow
            if (GameBootstrapper.HasInstance)
                GameBootstrapper.Instance.StartNewGame();
            else
                Debug.LogError("[StartScreenController] Không thấy GameBootstrapper trong scene!");
        }

        public void OnClickOptions()
        {
            Debug.Log("Options clicked (chưa làm menu options).");
        }

        public void OnClickQuit()
        {
            Debug.Log("Quit clicked.");
            Application.Quit();
        }

        private void Awake()
        {
            //if (pauseAtStart)
                //Time.timeScale = 0f;
        }
    }
}
