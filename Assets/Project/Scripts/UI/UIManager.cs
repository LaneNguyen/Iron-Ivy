using System.Collections.Generic;
using IronIvy.Data;
using IronIvy.Gameplay;
using IronIvy.Gameplay.Animals;
using IronIvy.Gameplay.Interaction;
using IronIvy.Gameplay.Rhythm;
using IronIvy.UI;
using UnityEngine;

namespace IronIvy.Core
{
    public class UIManager : BaseManager<UIManager>
    {
        [System.Serializable]
        public class PopupGroup
        {
            public PlantRhythmStartPanel plantRhythmStartPanel;
            public MinigameInteractionPanel animalInteractionPanel;

            public GameObject pauseMenu;
            public GameObject settingsMenu;
        }

        [System.Serializable]
        public class NotifyGroup
        {
            public RhythmHUD rhythmHUD;
            public PlantRhythmRewardPanel plantRewardPanel;
            public AnimalRhythmRewardPanel animalRewardPanel;


        }

        [Header("Refs")]
        public PopupGroup popup;
        public NotifyGroup notify;
        public MainGameUIPanel mainGameUIPanel;

        public ArchivePanel archivePanel;

        private ClickPlantRhythmMinigame _plantRhythmMinigame;
        private ClickAnimalRhythmMinigame _animalRhythmMinigame;

        // =========================
        // LIFECYCLE & EVENT REGISTRATION
        // =========================
        private void Start()
        {
            EnsureMinigameRefs();
        }

        private void OnEnable()
        {
            // Đăng ký lắng nghe sự kiện kết quả từ ListenManager
            if (ListenManager.HasInstance)
            {
                ListenManager.Instance.OnRhythmPlantResult += HandlePlantRhythmResult;
                ListenManager.Instance.OnRhythmAnimalResult += HandleAnimalRhythmResult;

                ListenManager.Instance.OnArchiveOpenRequested += HandleArchiveOpenRequested;

            }
        }

        private void OnDisable()
        {
            // Hủy đăng ký khi Object bị tắt để tránh lỗi bộ nhớ
            if (ListenManager.HasInstance)
            {
                ListenManager.Instance.OnRhythmPlantResult -= HandlePlantRhythmResult;
                ListenManager.Instance.OnRhythmAnimalResult -= HandleAnimalRhythmResult;

                ListenManager.Instance.OnArchiveOpenRequested -= HandleArchiveOpenRequested;
            }
        }

        // =========================
        // EVENT HANDLERS
        // =========================
        private void HandlePlantRhythmResult(ListenManager.RhythmPlantResultPayload payload)
        {
            if (notify.plantRewardPanel != null)
            {
                Debug.Log("<color=cyan>[UIManager]</color> Nhận tín hiệu kết quả Plant Rhythm. Đang mở bảng thưởng...");
                notify.plantRewardPanel.ShowPlantRhythmResult(payload);
            }
            else
            {
                Debug.LogWarning("[UIManager] notify.plantRewardPanel chưa được gán trong Editor!");
            }
        }

        private void HandleAnimalRhythmResult(ListenManager.RhythmAnimalResultPayload payload)
        {
            if (notify.animalRewardPanel != null)
            {
                Debug.Log("<color=cyan>[UIManager]</color> Đang mở bảng thưởng động vật...");

                // SỬA: Không chỉ SetActive, phải gọi hàm Show để nạp data
                notify.animalRewardPanel.gameObject.SetActive(true);
                notify.animalRewardPanel.ShowAnimalRhythmResult(payload);
            }
        }

        private void EnsureMinigameRefs()
        {
            if (_plantRhythmMinigame == null)
                _plantRhythmMinigame = FindObjectOfType<ClickPlantRhythmMinigame>(true);

            if (_animalRhythmMinigame == null)
                _animalRhythmMinigame = FindObjectOfType<ClickAnimalRhythmMinigame>(true);
        }

        // =========================
        // START MINIGAME REQUESTS
        // =========================
        public bool RequestStartPlantRhythm(PlantArea area, List<PlantDefinition> selectedPlants, int energyCost)
        {
            EnsureMinigameRefs();

            if (_plantRhythmMinigame == null || area == null) return false;

            // THÊM LOGIC TRỪ ENERGY GIỐNG ANIMAL
            if (EnergyManager.HasInstance && !EnergyManager.Instance.TrySpend(energyCost))
            {
                Debug.LogWarning("[UIManager] Not enough energy for Plant Rhythm.");
                return false;
            }

            _plantRhythmMinigame.StartSequence(area.plots, selectedPlants, area);

            CloseAllPopups();
            if (ListenManager.HasInstance) ListenManager.Instance.RaiseMinigameStarted();
            return true;
        }

        public bool RequestStartAnimalRhythm(AnimalController animal, FoodItem selectedFood, int energyCost)
        {
            EnsureMinigameRefs();

            if (_animalRhythmMinigame == null)
            {
                Debug.LogWarning("[UIManager] ClickAnimalRhythmMinigame not found.");
                return false;
            }

            if (animal == null)
            {
                Debug.LogWarning("[UIManager] Animal is null.");
                return false;
            }

            if (EnergyManager.HasInstance && !EnergyManager.Instance.TrySpend(energyCost))
            {
                Debug.LogWarning("[UIManager] Not enough energy for Animal Rhythm.");
                return false;
            }

            bool isFavorite = false;

            if (selectedFood != null)
            {
                if (InventoryManager.HasInstance && InventoryManager.Instance.Consume(selectedFood, 1))
                {
                    if (animal.Definition != null && animal.Definition.favoriteFood == selectedFood)
                        isFavorite = true;

                    animal.TryFeed(selectedFood);
                    if (ListenManager.HasInstance) ListenManager.Instance.RaiseInventoryChanged();
                }
            }

            _animalRhythmMinigame.RequestPlay(animal, isFavorite);

            CloseAllPopups();
            if (ListenManager.HasInstance) ListenManager.Instance.RaiseMinigameStarted();
            return true;
        }

        // =========================
        // COMPATIBILITY OVERLOADS
        // =========================



        public bool RequestStartPlantRhythm(object plots, List<PlantDefinition> selectedPlants, PlantArea area)
        {
            return RequestStartPlantRhythm(area, selectedPlants);
        }


        public bool RequestStartPlantRhythm(PlantArea area)
        {
            return RequestStartPlantRhythm(area, new List<PlantDefinition>());
        }

        public bool RequestStartAnimalRhythm(AnimalController animal, int energyCost)
        {
            return RequestStartAnimalRhythm(animal, null, energyCost);
        }

        public bool RequestStartAnimalRhythm(AnimalController animal)
        {
            return RequestStartAnimalRhythm(animal, null, 1);
        }

        public bool RequestStartAnimalRhythm(AnimalController animal, FoodItem selectedFood)
        {
            return RequestStartAnimalRhythm(animal, selectedFood, 1);
        }
        public bool RequestStartPlantRhythm(PlantArea area, List<PlantDefinition> selectedPlants)
        {
            return RequestStartPlantRhythm(area, selectedPlants, 0);
        }

        // =========================
        // UI CONTROL
        // =========================
        public void CloseAllPopups()
        {
            if (popup.plantRhythmStartPanel != null) popup.plantRhythmStartPanel.Hide();
            if (popup.animalInteractionPanel != null) popup.animalInteractionPanel.Hide();

            // Đóng bảng thưởng nếu nó đang mở

            if (notify.plantRewardPanel != null) notify.plantRewardPanel.Hide();

            ShowMainHUD();
        }

        private void HideMainHUD()
        {
            if (mainGameUIPanel != null)
                mainGameUIPanel.gameObject.SetActive(false);
        }

        private void ShowMainHUD()
        {
            if (mainGameUIPanel != null)
                mainGameUIPanel.gameObject.SetActive(true);
        }

        public void ShowAnimalInteraction(AnimalController animal, InteractionTrigger sourceTrigger = null)
        {
            if (popup == null || popup.animalInteractionPanel == null) return;
            ShowMainHUD();
            popup.animalInteractionPanel.ShowForAnimal(animal, sourceTrigger);
        }

        public void OpenSettings()
        {
            if (popup.settingsMenu != null) popup.settingsMenu.SetActive(true);
        }

        public void CloseSettings()
        {
            if (popup.settingsMenu != null) popup.settingsMenu.SetActive(false);
        }


        public void OpenArchiveUI()
        {
            CloseAllPopups();

            if (archivePanel != null)
            {
                archivePanel.Show();
            }
            else
            {
                Debug.LogWarning("[UIManager] archivePanel is NULL");
            }
        }

        private void HandleArchiveOpenRequested()
        {
            // Close popups nhưng KHÔNG được dập luôn ArchivePanel
            // => sẽ chỉnh CloseAllPopups nếu cần
            CloseAllPopups();

            // gọi đúng panel show
            if (archivePanel != null) archivePanel.Show();
        }

        public void CloseArchiveUI()
        {
            CloseAllPopups();
        }
    }
}