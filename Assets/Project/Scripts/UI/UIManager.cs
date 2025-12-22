using System.Collections;
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

        [Header("Fade Settings")]
        [SerializeField] private CanvasGroup fadeOverlay;
        [SerializeField] private float fadeOutTime = 0.18f;
        [SerializeField] private float fadeInTime = 0.18f;
        [SerializeField] private float holdBlack = 0.05f;

        private ClickPlantRhythmMinigame _plantRhythmMinigame;
        private ClickAnimalRhythmMinigame _animalRhythmMinigame;
        private Coroutine _fadeRoutine;

        // =========================
        // LIFECYCLE & EVENT REGISTRATION
        // =========================
        private void Start()
        {
            EnsureMinigameRefs();
            
            // Khởi tạo trạng thái overlay ban đầu
            if (fadeOverlay != null)
            {
                fadeOverlay.alpha = 0f;
                fadeOverlay.gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (ListenManager.HasInstance)
            {
                ListenManager.Instance.OnRhythmPlantResult += HandlePlantRhythmResult;
                ListenManager.Instance.OnRhythmAnimalResult += HandleAnimalRhythmResult;
                ListenManager.Instance.OnArchiveOpenRequested += HandleArchiveOpenRequested;
            }
        }

        private void OnDisable()
        {
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
        }

        private void HandleAnimalRhythmResult(ListenManager.RhythmAnimalResultPayload payload)
        {
            if (notify.animalRewardPanel != null)
            {
                Debug.Log("<color=cyan>[UIManager]</color> Đang mở bảng thưởng động vật...");
                notify.animalRewardPanel.gameObject.SetActive(true);
                notify.animalRewardPanel.ShowAnimalRhythmResult(payload);
            }
        }

        private void HandleArchiveOpenRequested()
        {
            // Khi nhận event từ ListenManager, cũng thực hiện mở kèm hiệu ứng fade
            OpenArchiveUI();
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

            if (EnergyManager.HasInstance && !EnergyManager.Instance.TrySpend(energyCost)) return false;

            _plantRhythmMinigame.StartSequence(area.plots, selectedPlants, area);
            CloseAllPopups();
            if (ListenManager.HasInstance) ListenManager.Instance.RaiseMinigameStarted();
            return true;
        }

        public bool RequestStartAnimalRhythm(AnimalController animal, FoodItem selectedFood, int energyCost)
        {
            EnsureMinigameRefs();
            if (_animalRhythmMinigame == null || animal == null) return false;

            if (EnergyManager.HasInstance && !EnergyManager.Instance.TrySpend(energyCost)) return false;

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
        // UI CONTROL & FADE LOGIC
        // =========================
        
        public void OpenArchiveUI()
        {
            if (archivePanel == null) return;
            
            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
            _fadeRoutine = StartCoroutine(OpenArchiveWithFade());
        }

        private IEnumerator OpenArchiveWithFade()
        {
            // 1) Fade to black
            yield return FadeOverlay(1f, fadeOutTime, blockRaycasts: true);

            // 2) Switch UI (Trong lúc màn hình đang đen)
            CloseAllPopups();
            if (archivePanel != null) archivePanel.Show();

            yield return new WaitForSecondsRealtime(holdBlack);

            // 3) Fade back
            yield return FadeOverlay(0f, fadeInTime, blockRaycasts: false);
            
            _fadeRoutine = null;
        }

        private IEnumerator FadeOverlay(float target, float duration, bool blockRaycasts)
        {
            if (fadeOverlay == null) yield break;

            fadeOverlay.gameObject.SetActive(true);
            fadeOverlay.blocksRaycasts = blockRaycasts;
            fadeOverlay.interactable = blockRaycasts;

            float start = fadeOverlay.alpha;
            float t = 0f;

            duration = Mathf.Max(0.01f, duration);

            while (t < duration)
            {
                t += Time.unscaledDeltaTime; // Dùng unscaled để fade mượt cả khi pause game
                float p = Mathf.Clamp01(t / duration);
                fadeOverlay.alpha = Mathf.Lerp(start, target, p);
                yield return null;
            }

            fadeOverlay.alpha = target;

            if (Mathf.Approximately(target, 0f))
                fadeOverlay.gameObject.SetActive(false);
        }

        public void CloseArchiveUI()
        {
            // Hiện tại đóng Archive quay về Main HUD
            CloseAllPopups();
        }

        public void CloseAllPopups()
        {
            if (popup.plantRhythmStartPanel != null) popup.plantRhythmStartPanel.Hide();
            if (popup.animalInteractionPanel != null) popup.animalInteractionPanel.Hide();
            if (notify.plantRewardPanel != null) notify.plantRewardPanel.Hide();
            if (notify.animalRewardPanel != null) notify.animalRewardPanel.gameObject.SetActive(false);
            
            // Nếu có ArchivePanel đang mở thì ẩn luôn (tùy thuộc vào cấu trúc ArchivePanel.Show/Hide của bạn)
            if (archivePanel != null) archivePanel.gameObject.SetActive(false);

            ShowMainHUD();
        }

        public void ShowAnimalInteraction(AnimalController animal, InteractionTrigger sourceTrigger = null)
        {
            if (popup == null || popup.animalInteractionPanel == null) return;
            ShowMainHUD();
            popup.animalInteractionPanel.ShowForAnimal(animal, sourceTrigger);
        }

        public void OpenSettings() => popup.settingsMenu?.SetActive(true);
        public void CloseSettings() => popup.settingsMenu?.SetActive(false);

        private void HideMainHUD() => mainGameUIPanel?.gameObject.SetActive(false);
        private void ShowMainHUD() => mainGameUIPanel?.gameObject.SetActive(true);

        // =========================
        // COMPATIBILITY OVERLOADS
        // =========================
        public bool RequestStartPlantRhythm(object plots, List<PlantDefinition> selectedPlants, PlantArea area) => RequestStartPlantRhythm(area, selectedPlants);
        public bool RequestStartPlantRhythm(PlantArea area) => RequestStartPlantRhythm(area, new List<PlantDefinition>());
        public bool RequestStartAnimalRhythm(AnimalController animal, int energyCost) => RequestStartAnimalRhythm(animal, null, energyCost);
        public bool RequestStartAnimalRhythm(AnimalController animal) => RequestStartAnimalRhythm(animal, null, 1);
        public bool RequestStartAnimalRhythm(AnimalController animal, FoodItem selectedFood) => RequestStartAnimalRhythm(animal, selectedFood, 1);
        public bool RequestStartPlantRhythm(PlantArea area, List<PlantDefinition> selectedPlants) => RequestStartPlantRhythm(area, selectedPlants, 0);
    }
}