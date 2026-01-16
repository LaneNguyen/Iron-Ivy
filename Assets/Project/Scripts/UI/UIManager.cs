using System.Collections;
using System.Collections.Generic;
using IronIvy.Data;
using IronIvy.Gameplay;
using IronIvy.Gameplay.Animals;
using IronIvy.Gameplay.Interaction;
using IronIvy.Gameplay.Rhythm;
using IronIvy.UI;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

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

            public GameObject minimapRoot;
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

        [Header("Ending Timeline (ScreenFader -> Hide UI -> Play Timeline)")]
        [SerializeField] private float holdBlackBeforePlayTimeline = 0.10f;

        [Header("After Ending Timeline")]
        [SerializeField] private bool loadFirstSceneAfterTimeline = true;
        [SerializeField] private int firstSceneBuildIndex = 0;
        [SerializeField] private bool fadeInBeforeLoadFirstScene = false;

        [Header("Timeline AutoPlay Guard")]
        [Tooltip("Kéo các PlayableDirector bạn KHÔNG muốn nó tự chạy vào đây (vd: EndingTimeline Director). UIManager sẽ disable component lúc Start.")]
        [SerializeField] private List<PlayableDirector> directorsToDisableOnStart = new List<PlayableDirector>();

        [Tooltip("Nếu true: UIManager sẽ disable toàn bộ directorsToDisableOnStart ngay khi Start.")]
        [SerializeField] private bool disableDirectorsOnStart = true;

        [Tooltip("Nếu true: khi PlayEndingTimeline sẽ gọi Evaluate() trước Play để update bindings ngay.")]
        [SerializeField] private bool evaluateBeforePlayTimeline = true;

        private ClickPlantRhythmMinigame _plantRhythmMinigame;
        private ClickAnimalRhythmMinigame _animalRhythmMinigame;
        private Coroutine _fadeRoutine;

        private PlayableDirector _currentEndingDirector;

        private void Start()
        {
            EnsureMinigameRefs();

            if (fadeOverlay != null)
            {
                fadeOverlay.alpha = 0f;
                fadeOverlay.gameObject.SetActive(false);
            }

            SetMinimapVisible(true);

            ApplyAutoPlayGuard();
        }

        private void ApplyAutoPlayGuard()
        {
            if (!disableDirectorsOnStart) return;
            if (directorsToDisableOnStart == null || directorsToDisableOnStart.Count == 0) return;

            for (int i = 0; i < directorsToDisableOnStart.Count; i++)
            {
                var d = directorsToDisableOnStart[i];
                if (d == null) continue;

                // Disable component để không ai Play được (kể cả script khác lỡ gọi)
                d.enabled = false;

                // Reset về 0 cho sạch sẽ, tránh case InitialTime bị set khác
                d.time = 0;

                // Evaluate chỉ khi component enabled, nên ở đây mình không gọi
            }
        }

        private void OnEnable()
        {
            if (ListenManager.HasInstance)
            {
                ListenManager.Instance.OnRhythmPlantResult += HandlePlantRhythmResult;
                ListenManager.Instance.OnRhythmAnimalResult += HandleAnimalRhythmResult;
                ListenManager.Instance.OnArchiveOpenRequested += HandleArchiveOpenRequested;

                ListenManager.Instance.OnGameplayHUDVisibleRequested += HandleGameplayHUDVisibleRequested;
                ListenManager.Instance.OnMinimapVisibleRequested += HandleMinimapVisibleRequested;
            }
        }

        private void OnDisable()
        {
            if (ListenManager.HasInstance)
            {
                ListenManager.Instance.OnRhythmPlantResult -= HandlePlantRhythmResult;
                ListenManager.Instance.OnRhythmAnimalResult -= HandleAnimalRhythmResult;
                ListenManager.Instance.OnArchiveOpenRequested -= HandleArchiveOpenRequested;

                ListenManager.Instance.OnGameplayHUDVisibleRequested -= HandleGameplayHUDVisibleRequested;
                ListenManager.Instance.OnMinimapVisibleRequested -= HandleMinimapVisibleRequested;
            }

            UnhookEndingDirector();
        }

        private void HandlePlantRhythmResult(ListenManager.RhythmPlantResultPayload payload)
        {
            if (notify.plantRewardPanel != null)
            {
                Debug.Log("<color=cyan>[UIManager]</color> Nhận tín hiệu kết quả Plant Rhythm. Đang mở bảng thưởng...");
                notify.plantRewardPanel.ShowPlantRhythmResult(payload);
            }

             ShowMinimap();
        }

        private void HandleAnimalRhythmResult(ListenManager.RhythmAnimalResultPayload payload)
        {
            if (notify.animalRewardPanel != null)
            {
                Debug.Log("<color=cyan>[UIManager]</color> Đang mở bảng thưởng động vật...");
                notify.animalRewardPanel.gameObject.SetActive(true);
                notify.animalRewardPanel.ShowAnimalRhythmResult(payload);
            }
            ShowMinimap();
        }

        private void HandleArchiveOpenRequested()
        {
            OpenArchiveUI();
        }

        private void HandleGameplayHUDVisibleRequested(bool visible)
        {
            if (visible) ShowMainHUD();
            else HideMainHUD();
        }

        private void HandleMinimapVisibleRequested(bool visible)
        {
            SetMinimapVisible(visible);
        }

        private void EnsureMinigameRefs()
        {
            if (_plantRhythmMinigame == null)
                _plantRhythmMinigame = FindObjectOfType<ClickPlantRhythmMinigame>(true);

            if (_animalRhythmMinigame == null)
                _animalRhythmMinigame = FindObjectOfType<ClickAnimalRhythmMinigame>(true);
        }

        public void ShowMinimap() => SetMinimapVisible(true);
        public void HideMinimap() => SetMinimapVisible(false);

        private void SetMinimapVisible(bool visible)
        {
            if (notify != null && notify.minimapRoot != null)
                notify.minimapRoot.SetActive(visible);
        }

        private void ClearRhythmHUDAvatarCache()
        {
            if (notify == null) return;
            if (notify.rhythmHUD == null) return;

            var hud = notify.rhythmHUD;

            hud.SendMessage("ClearAvatarIcon", SendMessageOptions.DontRequireReceiver);
            hud.SendMessage("ClearAvatar", SendMessageOptions.DontRequireReceiver);
            hud.SendMessage("ClearIcon", SendMessageOptions.DontRequireReceiver);
            hud.SendMessage("ResetAvatar", SendMessageOptions.DontRequireReceiver);

            hud.SendMessage("SetAvatarIcon", null, SendMessageOptions.DontRequireReceiver);
            hud.SendMessage("SetIcon", null, SendMessageOptions.DontRequireReceiver);
            hud.SendMessage("SetAnimalIcon", null, SendMessageOptions.DontRequireReceiver);
        }

        public bool RequestStartPlantRhythm(PlantArea area, List<PlantDefinition> selectedPlants, int energyCost)
        {
            EnsureMinigameRefs();
            if (_plantRhythmMinigame == null || area == null) return false;

            if (EnergyManager.HasInstance && !EnergyManager.Instance.TrySpend(energyCost)) return false;

            ClearRhythmHUDAvatarCache();

            if (notify != null && notify.rhythmHUD != null)
            {
                notify.rhythmHUD.ClearReactionPresenterAnimal();
            }

            _plantRhythmMinigame.StartSequence(area.plots, selectedPlants, area);
            CloseAllPopups();

            HideMinimap();

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
                        
                    if (isFavorite)
{
    animal.GrantFavoriteFoodBuffToken();
}


                    animal.TryFeed(selectedFood);
                    if (ListenManager.HasInstance) ListenManager.Instance.RaiseInventoryChanged();
                }
            }

            ClearRhythmHUDAvatarCache();

            _animalRhythmMinigame.RequestPlay(animal, isFavorite);
            CloseAllPopups();

            HideMinimap();

            if (ListenManager.HasInstance) ListenManager.Instance.RaiseMinigameStarted();
            return true;
        }

        public void OpenArchiveUI()
        {
            if (archivePanel == null) return;

            HideMinimap();

            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
            _fadeRoutine = StartCoroutine(OpenArchiveWithFade());
        }

        private IEnumerator OpenArchiveWithFade()
        {
            yield return FadeOverlay(1f, fadeOutTime, blockRaycasts: true);

            CloseAllPopups();

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayOpenPanelSE();

            if (archivePanel != null) archivePanel.Show();

            yield return new WaitForSecondsRealtime(holdBlack);

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
                t += Time.unscaledDeltaTime;
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
            CloseAllPopups();
            ShowMinimap();
        }

        public void CloseAllPopups()
        {
            if (popup.plantRhythmStartPanel != null) popup.plantRhythmStartPanel.Hide();
            if (popup.animalInteractionPanel != null) popup.animalInteractionPanel.Hide();
            if (notify.plantRewardPanel != null) notify.plantRewardPanel.Hide();
            if (notify.animalRewardPanel != null) notify.animalRewardPanel.gameObject.SetActive(false);

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

        public void PlayEndingTimeline(PlayableDirector director)
        {
            if (director == null)
            {
                Debug.LogWarning("<color=yellow>[UIManager]</color> PlayEndingTimeline() director is null.");
                return;
            }

            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);

            UnhookEndingDirector();

            _fadeRoutine = StartCoroutine(PlayEndingTimelineRoutine(director));
        }

        private IEnumerator PlayEndingTimelineRoutine(PlayableDirector director)
        {
            yield return FadeOverlay(1f, fadeOutTime, blockRaycasts: true);

            HideAllGameUIForCutscene();

            if (holdBlackBeforePlayTimeline > 0f)
                yield return new WaitForSecondsRealtime(holdBlackBeforePlayTimeline);

            _currentEndingDirector = director;
            _currentEndingDirector.stopped += HandleEndingTimelineStopped;

            // Quan trọng: nếu director bị disable ở Start để chống autoplay, thì giờ phải bật lại trước khi Play
            if (!_currentEndingDirector.enabled)
                _currentEndingDirector.enabled = true;

            _currentEndingDirector.time = 0;

            if (evaluateBeforePlayTimeline)
                _currentEndingDirector.Evaluate();

            // Fade từ đen về sáng để thấy cutscene
            yield return FadeOverlay(0f, fadeInTime, blockRaycasts: false);
            // Tắt BGM hiện tại để nhường chỗ cho BGM của timeline
            if (AudioManager.HasInstance)
            {
                AudioManager.Instance.PauseBGMRuntime();
            }
            // Rồi mới play để cutscene chạy
            _currentEndingDirector.Play();

            _fadeRoutine = null;
        }

        private void HideAllGameUIForCutscene()
        {
            if (popup != null)
            {
                if (popup.plantRhythmStartPanel != null) popup.plantRhythmStartPanel.Hide();
                if (popup.animalInteractionPanel != null) popup.animalInteractionPanel.Hide();

                if (popup.pauseMenu != null) popup.pauseMenu.SetActive(false);
                if (popup.settingsMenu != null) popup.settingsMenu.SetActive(false);
            }

            if (notify != null)
            {
                if (notify.plantRewardPanel != null) notify.plantRewardPanel.Hide();
                if (notify.animalRewardPanel != null) notify.animalRewardPanel.gameObject.SetActive(false);

                if (notify.rhythmHUD != null) notify.rhythmHUD.gameObject.SetActive(false);
                if (notify.minimapRoot != null) notify.minimapRoot.SetActive(false);
            }

            if (archivePanel != null) archivePanel.gameObject.SetActive(false);
            if (mainGameUIPanel != null) mainGameUIPanel.gameObject.SetActive(false);
        }

        private void HandleEndingTimelineStopped(PlayableDirector d)
        {
            UnhookEndingDirector();
            // Nếu không load scene thì resume để game không bị im luôn
            if (!loadFirstSceneAfterTimeline)
            {
                if (AudioManager.HasInstance)
                    AudioManager.Instance.ResumeBGMRuntime();
            }
            if (loadFirstSceneAfterTimeline)
            {
                if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
                _fadeRoutine = StartCoroutine(LoadFirstSceneRoutine());
            }
        }

        private IEnumerator LoadFirstSceneRoutine()
        {
            if (fadeInBeforeLoadFirstScene)
            {
                yield return FadeOverlay(0f, fadeInTime, blockRaycasts: false);
            }

            yield return null;

            int idx = Mathf.Max(0, firstSceneBuildIndex);
            SceneManager.LoadScene(idx);

            _fadeRoutine = null;
        }

        private void UnhookEndingDirector()
        {
            if (_currentEndingDirector != null)
            {
                _currentEndingDirector.stopped -= HandleEndingTimelineStopped;
                _currentEndingDirector = null;
            }
        }

        public bool RequestStartPlantRhythm(object plots, List<PlantDefinition> selectedPlants, PlantArea area) => RequestStartPlantRhythm(area, selectedPlants);
        public bool RequestStartPlantRhythm(PlantArea area) => RequestStartPlantRhythm(area, new List<PlantDefinition>());
        public bool RequestStartAnimalRhythm(AnimalController animal, int energyCost) => RequestStartAnimalRhythm(animal, null, energyCost);
        public bool RequestStartAnimalRhythm(AnimalController animal) => RequestStartAnimalRhythm(animal, null, 1);
        public bool RequestStartAnimalRhythm(AnimalController animal, FoodItem selectedFood) => RequestStartAnimalRhythm(animal, selectedFood, 1);
        public bool RequestStartPlantRhythm(PlantArea area, List<PlantDefinition> selectedPlants) => RequestStartPlantRhythm(area, selectedPlants, 0);
    }
}
