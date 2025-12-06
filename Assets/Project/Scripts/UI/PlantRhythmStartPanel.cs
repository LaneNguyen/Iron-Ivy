using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using IronIvy.Data;
using IronIvy.Gameplay.Rhythm;
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
        public TextMeshProUGUI energyCostText;

        [Header("Config")]
        public int energyPerPlant = 1;

        // === [FIX COMPATIBILITY] Code tương thích ngược ===
        // Mapping biến cũ sang biến mới để MainGameUIPanel không lỗi
        public int baseEnergyCost => energyPerPlant; 

        // Hàm Show cũ
        public void Show() 
        {
            if (root) root.SetActive(true);
            Debug.LogWarning("Old Show() called. Please update caller to use ShowForArea().");
        }
        // ==================================================
        
        // --- Runtime State ---
        private PlantArea _currentArea;
        private List<PlantDefinition> _selectedPlants = new List<PlantDefinition>(); 
        private int _currentSelectedSlotIndex = -1; 

        private void Start()
        {
            if (startButton) startButton.onClick.AddListener(OnStartClicked);
        }

        public void ShowForArea(PlantArea area)
        {
            _currentArea = area;
            _selectedPlants.Clear();
            if (area != null)
            {
                for (int i = 0; i < area.plots.Count; i++) _selectedPlants.Add(null);
            }

            if (root) root.SetActive(true);
            _currentSelectedSlotIndex = 0;
            RefreshUI();
        }

        public void Hide()
        {
            if (root) root.SetActive(false);
            _currentArea = null;
        }

        private void RefreshUI()
        {
            RenderPlotSlots();
            RenderSeedList();
            UpdateStatus();
        }

        private void RenderPlotSlots()
        {
            if (!plotSlotContainer || !plotSlotPrefab || _currentArea == null) return;
            foreach (Transform child in plotSlotContainer) Destroy(child.gameObject);

            for (int i = 0; i < _currentArea.plots.Count; i++)
            {
                int index = i;
                Button btn = Instantiate(plotSlotPrefab, plotSlotContainer);
                var txt = btn.GetComponentInChildren<TextMeshProUGUI>();
                if (txt)
                {
                    string plantName = _selectedPlants[index] != null ? _selectedPlants[index].displayName : "Empty";
                    txt.text = $"Plot {index + 1}\n<size=80%>{plantName}</size>";
                }
                var img = btn.GetComponent<Image>();
                if (img) img.color = (index == _currentSelectedSlotIndex) ? Color.yellow : Color.white;
                btn.onClick.AddListener(() => { _currentSelectedSlotIndex = index; RefreshUI(); });
            }
        }

        private void RenderSeedList()
        {
            if (!seedListContainer || !seedButtonPrefab) return;
            foreach (Transform child in seedListContainer) Destroy(child.gameObject);

            var minigame = FindObjectOfType<ClickPlantRhythmMinigame>();
            var availableSeeds = minigame ? minigame.debugAvailablePlants : new List<PlantDefinition>();

            // Button None
            Button clearBtn = Instantiate(seedButtonPrefab, seedListContainer);
            if(clearBtn.GetComponentInChildren<TextMeshProUGUI>()) clearBtn.GetComponentInChildren<TextMeshProUGUI>().text = "None";
            clearBtn.onClick.AddListener(() => SelectPlantForCurrentSlot(null));

            foreach (var plant in availableSeeds)
            {
                Button btn = Instantiate(seedButtonPrefab, seedListContainer);
                if(btn.GetComponentInChildren<TextMeshProUGUI>()) btn.GetComponentInChildren<TextMeshProUGUI>().text = plant.displayName;
                var p = plant;
                btn.onClick.AddListener(() => SelectPlantForCurrentSlot(p));
            }
        }

        private void SelectPlantForCurrentSlot(PlantDefinition plant)
        {
            if (_currentSelectedSlotIndex >= 0 && _currentSelectedSlotIndex < _selectedPlants.Count)
            {
                _selectedPlants[_currentSelectedSlotIndex] = plant;
                if (_currentSelectedSlotIndex < _selectedPlants.Count - 1) _currentSelectedSlotIndex++;
                RefreshUI();
            }
        }

        private void UpdateStatus()
        {
            int plantCount = 0;
            foreach (var p in _selectedPlants) if (p != null) plantCount++;
            int totalEnergy = plantCount * energyPerPlant;
            if (energyCostText) energyCostText.text = $"Energy Cost: {totalEnergy}";
            if (startButton) startButton.interactable = plantCount > 0;
        }

        private void OnStartClicked()
        {
            int plantCount = 0;
            foreach (var p in _selectedPlants) if (p != null) plantCount++;
            if (plantCount == 0) return;

            int cost = plantCount * energyPerPlant;
            if (EnergyManager.HasInstance && !EnergyManager.Instance.TrySpend(cost)) return;

            var minigame = FindObjectOfType<ClickPlantRhythmMinigame>();
            if (minigame && _currentArea != null)
            {
                minigame.StartSequence(_currentArea.plots, _selectedPlants);
            }
            Hide();
        }
    }
}