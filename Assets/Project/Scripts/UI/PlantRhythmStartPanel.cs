using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using IronIvy.Data;
using IronIvy.Gameplay;
using IronIvy.Core;

namespace IronIvy.UI
{
    // panel chuẩn bị trước khi chơi plant rhythm
    // - chọn plot
    // - chọn seed cho từng plot
    // - bắn request qua UIManager để start minigame
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

        public int baseEnergyCost => energyPerPlant;

        private PlantArea _currentArea;
        private List<PlantDefinition> _selectedPlants = new List<PlantDefinition>();
        private int _currentSelectedSlotIndex = -1;

        public void Show()
        {
            if (root) root.SetActive(true);
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

            if (area != null && area.plots != null)
            {
                for (int i = 0; i < area.plots.Count; i++)
                    _selectedPlants.Add(null);
            }

            if (root) root.SetActive(true);

            _currentSelectedSlotIndex = (_selectedPlants.Count > 0) ? 0 : -1;
            RefreshUI();
        }

        public void Hide()
        {
            if (root) root.SetActive(false);
            _currentArea = null;
        }

        private void OnCancelClicked()
        {
            // đóng popup, trả UI về main
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

                var txt = btn.GetComponentInChildren<TextMeshProUGUI>();
                if (txt)
                {
                    string plantName = _selectedPlants[index] != null ?
                        _selectedPlants[index].displayName : "Trống";

                    txt.text = $"Ô đất {index + 1}\n<size=80%>{plantName}</size>";
                }

                var img = btn.GetComponent<Image>();
                if (img)
                    img.color = (index == _currentSelectedSlotIndex) ? Color.yellow : Color.white;

                btn.onClick.AddListener(() =>
                {
                    _currentSelectedSlotIndex = index;
                    RefreshUI();
                });
            }
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
                if (txt != null)
                    txt.text = plant.displayName;

                var p = plant;
                btn.onClick.AddListener(() => SelectPlantForCurrentSlot(p));
            }
        }

        private void SelectPlantForCurrentSlot(PlantDefinition plant)
        {
            if (_currentSelectedSlotIndex >= 0 && _currentSelectedSlotIndex < _selectedPlants.Count)
            {
                _selectedPlants[_currentSelectedSlotIndex] = plant;

                if (_currentSelectedSlotIndex < _selectedPlants.Count - 1)
                    _currentSelectedSlotIndex++;

                RefreshUI();
            }
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

            int cost = plantCount * energyPerPlant;

            if (!UIManager.HasInstance)
            {
                Debug.LogWarning("[PlantRhythmStartPanel] UIManager missing.");
                return;
            }

            UIManager.Instance.RequestStartPlantRhythm(_currentArea, _selectedPlants, cost);
        }
    }
}
