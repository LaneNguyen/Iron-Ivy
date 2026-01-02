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
            if (AudioManager.HasInstance)
                AudioManager.Instance.PlayInterfaceSE();

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
            if (AudioManager.HasInstance)
                AudioManager.Instance.PlayInterfaceSE();

            Debug.Log("Options clicked.");
        }

        public void OnClickQuit()
        {
            if (AudioManager.HasInstance)
                AudioManager.Instance.PlayInterfaceSE();

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
