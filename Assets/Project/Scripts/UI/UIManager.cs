using UnityEngine;
using UnityEngine.UI;
using System.Collections; // Cần cái này để dùng Coroutine
using IronIvy.UI; 

namespace IronIvy.Core
{
    public class UIManager : BaseManager<UIManager>
    {
        [Header("Main HUD (read-only)")]
        public MainGameUIPanel mainGameUIPanel;      

        [Header("Global UI")]
        public GameObject pauseMenu;                
        public GameObject settingsMenu;             
        public GameObject loadingScreen;
        
        [Header("Archive UI")]
        public ArchivePanel archivePanel; 

        [Header("Effects")]
        [Tooltip("Image màu đen full màn hình + CanvasGroup để làm hiệu ứng chuyển cảnh")]
        public CanvasGroup fadeOverlay; 
        public float fadeDuration = 0.5f;

        public void InitHUD()
        {
            if (mainGameUIPanel != null && !mainGameUIPanel.gameObject.activeSelf)
            {
                mainGameUIPanel.gameObject.SetActive(true);
            }
        }
        
        public void InitHUD(int energy, float archive)
        {
            InitHUD();
        }

        // ... (Giữ nguyên ShowPause, ShowSettings...) ...

        // =========================================================
        // LOGIC MỞ ARCHIVE VỚI HIỆU ỨNG FADE
        // =========================================================
        public void OpenArchiveUI()
        {
            StartCoroutine(OpenArchiveRoutine());
        }

        private IEnumerator OpenArchiveRoutine()
        {
            // 1. Fade Tối màn hình (Alpha 0 -> 1)
            yield return StartCoroutine(FadeCanvasGroup(fadeOverlay, 0f, 1f, fadeDuration));

            // 2. Bật Archive Panel lên (lúc này màn hình đang đen thui nên user không thấy nó bật)
            if (archivePanel != null)
            {
                archivePanel.Show();
            }

            // Tạm ẩn HUD đi cho thoáng nếu muốn
            if (mainGameUIPanel) mainGameUIPanel.gameObject.SetActive(false);

            // 3. Fade Sáng màn hình (Alpha 1 -> 0) để lộ ra Archive Panel
            yield return StartCoroutine(FadeCanvasGroup(fadeOverlay, 1f, 0f, fadeDuration));
        }

        public void CloseArchiveUI()
        {
            StartCoroutine(CloseArchiveRoutine());
        }

        private IEnumerator CloseArchiveRoutine()
        {
            // 1. Fade Tối lại
            yield return StartCoroutine(FadeCanvasGroup(fadeOverlay, 0f, 1f, fadeDuration));

            // 2. Tắt Archive Panel
            if (archivePanel != null)
            {
                archivePanel.Hide();
            }

            // Bật lại HUD
            if (mainGameUIPanel) mainGameUIPanel.gameObject.SetActive(true);

            // 3. Fade Sáng lại (về Game)
            yield return StartCoroutine(FadeCanvasGroup(fadeOverlay, 1f, 0f, fadeDuration));
        }

        // Helper Fade chung
        private IEnumerator FadeCanvasGroup(CanvasGroup cg, float start, float end, float duration)
        {
            if (cg == null) yield break;
            
            cg.gameObject.SetActive(true);
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime; // Dùng unscaled để lỡ game có pause vẫn chạy được
                cg.alpha = Mathf.Lerp(start, end, t / duration);
                yield return null;
            }
            cg.alpha = end;
            
            // Nếu alpha về 0 thì tắt object đi cho nhẹ
            if (end == 0f) cg.gameObject.SetActive(false);
        }
    }
}