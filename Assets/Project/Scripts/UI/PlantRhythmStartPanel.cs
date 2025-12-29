using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using IronIvy.Data;
using IronIvy.Gameplay;
using IronIvy.Core;

namespace IronIvy.UI
{
    public class PlantRhythmStartPanel : MonoBehaviour
    {
        [Header("Root")]
        public GameObject root;

        [Header("UI - Plot Slots")]
        public Transform plotSlotContainer;
        public Button plotSlotPrefab;

        [Header("UI - Seed List")]
        public Transform seedListContainer;
        public Button seedButtonPrefab;

        [Header("UI - Actions")]
        public Button startButton;
        public Button cancelButton;
        public TextMeshProUGUI energyCostText;

        [Header("Config")]
        public int energyPerPlant = 1;

        // GIỮ NGUYÊN để MainGameUIPanel compile
        public int baseEnergyCost => energyPerPlant;

        [Header("Visual - Slot Colors")]
        public Color slotNormalColor = Color.white;
        public Color slotSelectedColor = new Color(1f, 0.92f, 0.2f, 1f);
        public Color slotFilledColor = new Color(0.75f, 1f, 0.75f, 1f);

        // =========================
        // Guide: show when panel opens first time
        // =========================
        [Header("Guide - Show when panel opens first time")]
        [SerializeField] private GameObject guidePanelOnFirstOpen;

        [SerializeField] private string guideStepId_FirstOpen = "guide.plant.startpanel.open";

        [SerializeField] private bool pauseGameWhenGuideShown = false;
        [SerializeField] private bool forceGuideOnTop = true;
        [SerializeField] private int guideSortingOrderOverride = 5000;

        [Header("Guide - Testing in Unity")]
        [Tooltip("Trong Unity Editor: nếu true thì bỏ qua PlayerPrefs (guide luôn hiện để test).")]
        [SerializeField] private bool ignorePrefsInEditor = true;

        [Tooltip("Trong Unity Editor: nếu true thì CompleteAndClose sẽ KHÔNG ghi nhận MarkShown.")]
        [SerializeField] private bool disableMarkInEditor = true;

        private GuidePanelView _activeGuideView;

        private PlantArea _currentArea;
        private List<PlantDefinition> _selectedPlants = new List<PlantDefinition>();
        private int _currentSelectedSlotIndex = -1;

        // GIỮ NGUYÊN để code cũ không gãy (MainGameUIPanel đang gọi)
        public void Show()
        {
            if (root) root.SetActive(true);

            // Nếu ai đó vẫn gọi Show() cũ thì mình cũng cố show guide (an toàn)
            TryShowGuide_OnFirstOpen();

            Debug.LogWarning("Old Show() called. Please update caller to use ShowForArea(PlantArea).");
        }

        private void Start()
        {
            if (startButton)
                startButton.onClick.AddListener(OnStartClicked);

            if (cancelButton)
                cancelButton.onClick.AddListener(OnCancelClicked);
        }

        public void ShowForArea(PlantArea area)
        {
            _currentArea = area;
            _selectedPlants.Clear();
            _activeGuideView = null;

            if (area != null && area.plots != null)
            {
                for (int i = 0; i < area.plots.Count; i++)
                    _selectedPlants.Add(null);
            }

            if (root) root.SetActive(true);

            // ✅ ĐÚNG Ý MỚI: guide hiện ngay khi panel mở (lần đầu)
            TryShowGuide_OnFirstOpen();

            _currentSelectedSlotIndex = (_selectedPlants.Count > 0) ? 0 : -1;
            RefreshUI();
            HighlightCurrentPlot();
        }

        public void Hide()
        {
            ClearPlotHighlight();
            ClearAllPreviews();

            // hide panel -> đóng guide nếu đang mở (KHÔNG mark)
            if (_activeGuideView != null && _activeGuideView.gameObject.activeSelf)
            {
                _activeGuideView.CloseOnly();
                _activeGuideView = null;
            }

            if (root) root.SetActive(false);
            _currentArea = null;
        }

        private void OnCancelClicked()
        {
            ClearPlotHighlight();
            ClearAllPreviews();

            // cancel -> đóng guide nếu đang mở (KHÔNG mark)
            if (_activeGuideView != null && _activeGuideView.gameObject.activeSelf)
            {
                _activeGuideView.CloseOnly();
                _activeGuideView = null;
            }

            if (UIManager.HasInstance)
                UIManager.Instance.CloseAllPopups();
            else
                Hide();
        }

        private void RefreshUI()
        {
            RenderPlotSlots();
            RenderSeedList();
            UpdateStatus();
        }

        private void RenderPlotSlots()
        {
            if (!plotSlotContainer || !plotSlotPrefab || _currentArea == null || _currentArea.plots == null) return;

            foreach (Transform child in plotSlotContainer)
                Destroy(child.gameObject);

            for (int i = 0; i < _currentArea.plots.Count; i++)
            {
                int index = i;
                Button btn = Instantiate(plotSlotPrefab, plotSlotContainer);

                btn.transition = Selectable.Transition.None;

                var txt = btn.GetComponentInChildren<TextMeshProUGUI>();
                if (txt)
                {
                    string plantName = _selectedPlants[index] != null ? _selectedPlants[index].displayName : "Trống";
                    txt.text = $"Ô đất {index + 1}\n<size=80%>{plantName}</size>";
                }

                ApplySlotVisual(btn, index);

                btn.onClick.AddListener(() =>
                {
                    _currentSelectedSlotIndex = index;
                    RefreshUI();
                    HighlightCurrentPlot();
                });
            }
        }

        private void ApplySlotVisual(Button btn, int index)
        {
            if (btn == null) return;
            var g = btn.targetGraphic;
            if (g == null) return;

            bool isSelected = (index == _currentSelectedSlotIndex);
            bool isFilled = (_selectedPlants != null && index >= 0 && index < _selectedPlants.Count && _selectedPlants[index] != null);

            if (isSelected) g.color = slotSelectedColor;
            else if (isFilled) g.color = slotFilledColor;
            else g.color = slotNormalColor;
        }

        private void RenderSeedList()
        {
            if (!seedListContainer || !seedButtonPrefab) return;

            foreach (Transform child in seedListContainer)
                Destroy(child.gameObject);

            List<PlantDefinition> availableSeeds = null;

            if (ArchiveManager.HasInstance)
                availableSeeds = ArchiveManager.Instance.GetAvailablePlants();

            if (availableSeeds == null || availableSeeds.Count == 0)
            {
                Debug.LogWarning("[PlantRhythmStartPanel] No available seeds from ArchiveManager.");
                return;
            }

            foreach (var plant in availableSeeds)
            {
                if (plant == null) continue;
                if (string.IsNullOrEmpty(plant.displayName)) continue;
                if (plant.displayName.Equals("More", System.StringComparison.OrdinalIgnoreCase)) continue;

                Button btn = Instantiate(seedButtonPrefab, seedListContainer);

                var txt = btn.GetComponentInChildren<TextMeshProUGUI>();
                if (txt != null) txt.text = plant.displayName;

                var p = plant;
                btn.onClick.AddListener(() => SelectPlantForCurrentSlot(p));
            }
        }

        private void SelectPlantForCurrentSlot(PlantDefinition plant)
        {
            if (_currentSelectedSlotIndex < 0 || _currentSelectedSlotIndex >= _selectedPlants.Count) return;

            _selectedPlants[_currentSelectedSlotIndex] = plant;

            if (_currentArea != null && _currentArea.plots != null
                && _currentSelectedSlotIndex >= 0 && _currentSelectedSlotIndex < _currentArea.plots.Count)
            {
                var plot = _currentArea.plots[_currentSelectedSlotIndex];
                if (plot != null) plot.SetPreviewPlant(plant);
            }

            if (_currentSelectedSlotIndex < _selectedPlants.Count - 1)
                _currentSelectedSlotIndex++;

            RefreshUI();
            HighlightCurrentPlot();
        }

        private void UpdateStatus()
        {
            int plantCount = 0;
            foreach (var p in _selectedPlants)
                if (p != null) plantCount++;

            int totalEnergy = plantCount * energyPerPlant;

            if (energyCostText)
                energyCostText.text = $"Energy Cost: {totalEnergy}";

            if (startButton)
                startButton.interactable = plantCount > 0;
        }

        private void OnStartClicked()
        {
            int plantCount = 0;
            foreach (var p in _selectedPlants)
                if (p != null) plantCount++;

            if (plantCount == 0) return;

            // Cơ chế auto: qua bước tiếp theo (Start minigame) -> complete + close guide
            CompleteAndCloseGuideIfOpen();

            int cost = plantCount * energyPerPlant;

            if (!UIManager.HasInstance)
            {
                Debug.LogWarning("[PlantRhythmStartPanel] UIManager missing.");
                return;
            }

            ClearPlotHighlight();
            ClearAllPreviews();

            UIManager.Instance.RequestStartPlantRhythm(_currentArea, _selectedPlants, cost);
        }

        // =========================
        // Guide helpers
        // =========================
        private void TryShowGuide_OnFirstOpen()
        {
            if (guidePanelOnFirstOpen == null) return;
            if (!GuidePanelManager.HasInstance) return;

            // đang mở rồi thì thôi
            if (_activeGuideView != null && _activeGuideView.gameObject.activeSelf)
                return;

            // show nhưng CHƯA mark. mark khi CompleteAndClose()
            _activeGuideView = GuidePanelManager.Instance.ShowPanelIfNotComplete(
                guideStepId_FirstOpen,
                guidePanelOnFirstOpen,
                pauseGameWhenGuideShown,
                forceGuideOnTop,
                guideSortingOrderOverride,
                ignorePrefsInEditor,
                disableMarkInEditor
            );
        }

        private void CompleteAndCloseGuideIfOpen()
        {
            if (_activeGuideView == null) return;
            if (!_activeGuideView.gameObject.activeSelf) return;

            _activeGuideView.CompleteAndClose();
            _activeGuideView = null;
        }

        private void HighlightCurrentPlot()
        {
            if (_currentArea == null) return;
            if (_currentSelectedSlotIndex < 0) return;
            if (_currentArea.plots == null) return;
            if (_currentSelectedSlotIndex >= _currentArea.plots.Count) return;

            _currentArea.HighlightPlot(_currentArea.plots[_currentSelectedSlotIndex]);
        }

        private void ClearPlotHighlight()
        {
            if (_currentArea != null)
                _currentArea.ClearHighlight();
        }

        private void ClearAllPreviews()
        {
            if (_currentArea == null || _currentArea.plots == null) return;

            for (int i = 0; i < _currentArea.plots.Count; i++)
            {
                if (_currentArea.plots[i] != null)
                    _currentArea.plots[i].ClearPreview();
            }
        }
    }
}
