using UnityEngine;
using System.Collections;
using IronIvy.UI;

namespace IronIvy.Core
{
    public class UIManager : BaseManager<UIManager>
    {

        [Header("Main HUD")]
        public MainGameUIPanel mainGameUIPanel;

        [Header("Archive UI")]
        public ArchivePanel archivePanel;

        [Header("Pause / Settings")]
        [SerializeField] private GameObject pauseMenu;
        [SerializeField] private GameObject settingsMenu;

        [Header("Fade Overlay")]
        public CanvasGroup fadeOverlay;
        public float fadeDuration = 0.5f;

        private bool _isTransitioning;

        // kiểu mở settings rồi mở thêm pause (hoặc ngược lại) vẫn không bị timeScale bật/tắt sai
        private int _timeScaleLockCount;

        // =========================
        // HUD init (GameManager gọi)
        // =========================
        public void InitHUD(int currentEnergy, float archivePercent)
        {
            if (mainGameUIPanel == null) return;

            // bật HUD
            if (!mainGameUIPanel.gameObject.activeSelf)
                mainGameUIPanel.gameObject.SetActive(true);

            // ép refresh 1 phát cho chắc, khỏi phụ thuộc event timing
            mainGameUIPanel.ForceRefresh();
        }

        // =========================
        // Archive (screen swap)
        // =========================
        public void OpenArchiveUI()
        {
            if (_isTransitioning) return;
            StartCoroutine(OpenArchiveRoutine());
        }

        private IEnumerator OpenArchiveRoutine()
        {
            _isTransitioning = true;

            // fade tối
            yield return StartCoroutine(FadeCanvasGroup(fadeOverlay, 0f, 1f, fadeDuration));

            // tắt main panel (screen swap)
            if (mainGameUIPanel != null)
                mainGameUIPanel.gameObject.SetActive(false);

            // bật archive
            if (archivePanel != null)
                archivePanel.Show();

            // fade sáng
            yield return StartCoroutine(FadeCanvasGroup(fadeOverlay, 1f, 0f, fadeDuration));

            _isTransitioning = false;
        }

        public void CloseArchiveUI()
        {
            if (_isTransitioning) return;
            StartCoroutine(CloseArchiveRoutine());
        }

        private IEnumerator CloseArchiveRoutine()
        {
            _isTransitioning = true;

            // fade tối
            yield return StartCoroutine(FadeCanvasGroup(fadeOverlay, 0f, 1f, fadeDuration));

            // tắt archive
            if (archivePanel != null)
                archivePanel.Hide();

            // bật lại main panel
            if (mainGameUIPanel != null)
                mainGameUIPanel.gameObject.SetActive(true);

            // ép refresh luôn cho chắc
            if (mainGameUIPanel != null)
                mainGameUIPanel.ForceRefresh();

            // fade sáng
            yield return StartCoroutine(FadeCanvasGroup(fadeOverlay, 1f, 0f, fadeDuration));

            _isTransitioning = false;
        }

        // =========================
        // Pause lock helpers
        // =========================
        private void LockPause()
        {
            _timeScaleLockCount++;
            if (_timeScaleLockCount == 1)
                Time.timeScale = 0f;
        }

        private void UnlockPause()
        {
            _timeScaleLockCount = Mathf.Max(0, _timeScaleLockCount - 1);
            if (_timeScaleLockCount == 0)
                Time.timeScale = 1f;
        }

        // =========================
        // Pause Menu (overlay)
        // =========================
        public void ShowPause()
        {
            if (pauseMenu != null) pauseMenu.SetActive(true);
            LockPause();
        }

        public void ClosePause()
        {
            if (pauseMenu != null) pauseMenu.SetActive(false);
            UnlockPause();
        }

        // =========================
        // Settings Menu (overlay)
        // =========================
        public void ShowSettings()
        {
            if (settingsMenu != null) settingsMenu.SetActive(true);
            LockPause();
        }

        public void CloseSettings()
        {
            if (settingsMenu != null) settingsMenu.SetActive(false);
            UnlockPause();
        }

        // =========================
        // Fade Overlay
        // =========================
        private IEnumerator FadeCanvasGroup(CanvasGroup cg, float start, float end, float duration)
        {
            if (cg == null) yield break;

            cg.gameObject.SetActive(true);

            // overlay bật lên thì block click cho chắc
            cg.blocksRaycasts = true;
            cg.interactable = true;

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                cg.alpha = Mathf.Lerp(start, end, t / duration);
                yield return null;
            }

            cg.alpha = end;

            // alpha về 0 thì tắt overlay + nhả raycast
            if (end <= 0f)
            {
                cg.blocksRaycasts = false;
                cg.interactable = false;
                cg.gameObject.SetActive(false);
            }
        }
    }
}
