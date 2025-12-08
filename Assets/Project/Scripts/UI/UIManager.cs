using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IronIvy.UI; 

namespace IronIvy.Core
{
    // UIManager giờ chỉ lo mấy UI global như pause, settings, loading...
    // HUD chính (energy, archive, minigame state) do MainGameUIPanel xử lý.
    public class UIManager : BaseManager<UIManager>
    {
        [Header("Main HUD (read-only)")]
        public MainGameUIPanel mainGameUIPanel;      // gán MainGameUIPanel trong scene

        [Header("Global UI")]
        public GameObject pauseMenu;                // menu pause
        public GameObject settingsMenu;             // menu cài đặt
        public GameObject loadingScreen;            // màn loading

        // INIT HUD

        // Hàm mới: dùng khi không cần truyền energy/archive nữa
        public void InitHUD()
        {
            // chỉ đảm bảo HUD chính đang bật
            if (mainGameUIPanel != null && !mainGameUIPanel.gameObject.activeSelf)
            {
                mainGameUIPanel.gameObject.SetActive(true);
            }
        }

        // Giữ lại signature cũ để code cũ không bị lỗi
        // nhưng không còn set text Energy / Archive ở đây nữa
        public void InitHUD(int energy, float archive)
        {
            // HUD tự đọc từ EnergyManager + EventBus
            InitHUD();
        }

        // GLOBAL UI CONTROL

        public void ShowPauseMenu(bool show)
        {
            if (pauseMenu != null)
            {
                pauseMenu.SetActive(show);
            }
        }

        public void ShowSettingsMenu(bool show)
        {
            if (settingsMenu != null)
            {
                settingsMenu.SetActive(show);
            }
        }

        public void ShowLoadingScreen(bool show)
        {
            if (loadingScreen != null)
            {
                loadingScreen.SetActive(show);
            }
        }
    }
}
