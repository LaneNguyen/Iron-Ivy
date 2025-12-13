using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using IronIvy.Gameplay.Rhythm;
using IronIvy.Gameplay.Animals;
using IronIvy.Core;
using IronIvy.Data;

namespace IronIvy.UI
{
    public class MinigameInteractionPanel : MonoBehaviour
    {
        public static MinigameInteractionPanel Instance { get; private set; }

        [Header("Root")]
        public GameObject panelRoot;

        [Header("Texts")]
        public TextMeshProUGUI titleText;
        public TextMeshProUGUI questionText;
        [Tooltip("Text hiển thị thông báo buff (ví dụ: 'Favorite Food!')")]
        public TextMeshProUGUI buffInfoText; 

        [TextArea]
        public string defaultQuestion = "Chơi với bạn nhỏ này?";

        [Header("Feeding UI")]
        public GameObject feedingSectionRoot; // Parent chứa UI chọn đồ ăn
        public Transform foodContainer;       // Grid layout
        public GameObject foodSlotPrefab;     // Prefab slot
        public Color selectedColor = Color.green;
        public Color normalColor = Color.white;

        [Header("Config")]
        [SerializeField] int animalEnergyCost = 1;
        [SerializeField] int plantDirectCost = 1; 

        // context
        private AnimalController _currentAnimal;
        private ClickAnimalRhythmMinigame _currentAnimalMinigame;
        private ClickPlantRhythmMinigame _currentPlantMinigame;
        private PlantRhythmStartPanel _currentPlantStartPanel;

        // Feeding State
        private FoodItem _selectedFood;
        private Dictionary<FoodItem, GameObject> _spawnedSlots = new Dictionary<FoodItem, GameObject>();

        // Dictionary lưu FoodItem và cái Image nền để đổi màu
private Dictionary<FoodItem, Image> _slotBackgrounds = new Dictionary<FoodItem, Image>();

        private void Awake()
        {
            Instance = this;
            if (panelRoot != null) panelRoot.SetActive(false);
            if (buffInfoText) buffInfoText.text = "";
        }

        // --- SETUP ---
        public void ShowForAnimal(AnimalController animal, ClickAnimalRhythmMinigame minigame)
        {
            _currentAnimal = animal;
            _currentAnimalMinigame = minigame;
            
            _currentPlantMinigame = null;
            _currentPlantStartPanel = null;
            _selectedFood = null; // Reset selection
            if (buffInfoText) buffInfoText.text = "";

            ShowPanel("Play Animal Rhythm?", $"{defaultQuestion}\n(-{animalEnergyCost} năng lượng)");

            // Bật UI Feeding
            if (feedingSectionRoot) feedingSectionRoot.SetActive(true);
            RenderFoodList();
        }

        public void ShowForPlant(ClickPlantRhythmMinigame minigame, PlantRhythmStartPanel startPanel)
        {
            _currentPlantMinigame = minigame;
            _currentPlantStartPanel = startPanel;
            
            _currentAnimal = null;
            _currentAnimalMinigame = null;
            _selectedFood = null;

            int displayCost = (startPanel != null) ? startPanel.baseEnergyCost : plantDirectCost;
            ShowPanel("Play Plant Rhythm?", $"{defaultQuestion}\n(-{displayCost} năng lượng)");

            // Tắt UI Feeding cho Plant
            if (feedingSectionRoot) feedingSectionRoot.SetActive(false);
        }

        private void ShowPanel(string title, string question)
        {
            if (panelRoot != null) panelRoot.SetActive(true);
            if (titleText != null) titleText.text = title;
            if (questionText != null) questionText.text = question;
        }

        public void HideIfCurrentAnimal(AnimalController animal)
        {
            if (animal == _currentAnimal) Hide();
        }

        public void Hide()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
            _currentAnimal = null;
            _currentAnimalMinigame = null;
            _currentPlantMinigame = null;
            _currentPlantStartPanel = null;
            _selectedFood = null;
        }

        // render danh sách đồ ăn từ inventory
        private void RenderFoodList()
        {
            if (!foodContainer || !foodSlotPrefab) return;

            // xóa hết slot cũ trước đã
            foreach (Transform child in foodContainer) Destroy(child.gameObject);
            _spawnedSlots.Clear();
            _slotBackgrounds.Clear();

            if (!InventoryManager.HasInstance) return;

            var allItems = InventoryManager.Instance.All();

            foreach (var kvp in allItems)
            {
                FoodItem food = kvp.Key;
                int count = kvp.Value;

                if (count <= 0) continue;

                // tạo slot từ prefab
                GameObject slotObj = Instantiate(foodSlotPrefab, foodContainer);
                
                // setup icon và số lượng
                var slotScript = slotObj.GetComponent<UIItemSlot>(); 
                if (slotScript) slotScript.Setup(food.icon, count);
                
                // prefab có thể chưa có Button, nên tự add vào luôn
                Button btn = slotObj.GetComponent<Button>();
                if (btn == null) btn = slotObj.AddComponent<Button>();

                // tắt transition mặc định vì tự đổi màu
                btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(() => OnFoodSelected(food));

                // lấy background image để đổi màu khi chọn
                // giả sử Image nằm ở root, nếu không thì dùng GetComponentInChildren
                Image bgImage = slotObj.GetComponent<Image>();
                
                if (bgImage)
                {
                    _slotBackgrounds.Add(food, bgImage);
                    bgImage.color = normalColor;
                }

                _spawnedSlots.Add(food, slotObj);
            }
        }



        private void OnFoodSelected(FoodItem food)
        {
            if (_selectedFood == food)
            {
                _selectedFood = null; // Toggle off
                UpdateSlotHighlights();
                if (buffInfoText) buffInfoText.text = "";
                return;
            }

            _selectedFood = food;
            UpdateSlotHighlights();

            // Check favorite
            if (_currentAnimal != null && _currentAnimal.Definition != null)
            {
                if (_currentAnimal.Definition.favoriteFood == food)
                {
                    if (buffInfoText) buffInfoText.text = "<color=green> Trúng gu trúng gu!(Buffs Active)</color>";
                }
                else
                {
                    if (buffInfoText) buffInfoText.text = "Chọn món này.";
                }
            }
        }

        // đổi màu highlight cho slot đang chọn
        private void UpdateSlotHighlights()
        {
            foreach (var kvp in _slotBackgrounds)
            {
                FoodItem thisFood = kvp.Key;
                Image bgImage = kvp.Value;

        if (bgImage == null) continue;

                if (thisFood == _selectedFood)
                    bgImage.color = selectedColor;
                else
                    bgImage.color = normalColor;
            }
        }
        // --- ACTIONS ---
        public void OnPlayButton()
        {
            // CASE ANIMAL
            if (_currentAnimal != null && _currentAnimalMinigame != null)
            {
                if (EnergyManager.HasInstance && !EnergyManager.Instance.TrySpend(animalEnergyCost))
                {
                    Debug.LogWarning("Not enough energy.");
                    return; 
                }

                // Xử lý Feeding
                bool isFavorite = false;
                if (_selectedFood != null)
                {
                    if (InventoryManager.HasInstance && InventoryManager.Instance.Consume(_selectedFood, 1))
                    {
                        if (_currentAnimal.Definition.favoriteFood == _selectedFood) isFavorite = true;
                        _currentAnimal.TryFeed(_selectedFood);
                    }
                }

                // Gọi Play kèm cờ Buff (Chưa có logic tính toán bên trong)
                _currentAnimalMinigame.RequestPlay(_currentAnimal, isFavorite);
            }
            // CASE PLANT
            else if (_currentPlantStartPanel != null) _currentPlantStartPanel.Show();
            else if (_currentPlantMinigame != null)
            {
                if (EnergyManager.HasInstance && !EnergyManager.Instance.TrySpend(plantDirectCost)) return;
                _currentPlantMinigame.StartGame();
            }

            Hide();
        }

        public void OnCancelButton() => Hide();
    }
}