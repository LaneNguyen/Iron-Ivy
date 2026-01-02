using System.Collections.Generic;
using IronIvy.Core;
using IronIvy.Data;
using IronIvy.Gameplay;
using IronIvy.Gameplay.Animals;
using IronIvy.Gameplay.Interaction;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IronIvy.UI
{
    public class MinigameInteractionPanel : MonoBehaviour
    {
        [Header("Root")]
        public GameObject panelRoot;

        [Header("Buttons")]
        public Button playButton; // NEW: kéo nút Start/Play vào đây (optional nhưng recommended)
        public Button cancelButton;

        [Header("Texts")]
        public TextMeshProUGUI titleText;
        public TextMeshProUGUI questionText;
        public TextMeshProUGUI buffInfoText;

        [TextArea]
        public string defaultQuestion = "Chơi với bạn nhỏ này?";

        [Header("Feeding UI")]
        public GameObject feedingSectionRoot;
        public Transform foodContainer;
        public GameObject foodSlotPrefab;
        public Color selectedColor = Color.green;
        public Color normalColor = Color.white;

        [Header("Config")]
        [SerializeField] private int animalEnergyCost = 1;

        [Header("Feedback (Favorite Food)")]
        [TextArea] public string correctFeedbackText = "Trúng gu trúng gu! (Buff Active)";
        [TextArea] public string wrongFeedbackText = "Không phải món khoái khẩu, nhưng vẫn chơi được nè.";

        public Color correctFeedbackColor = new Color(0.2f, 0.85f, 0.35f, 1f);
        public Color wrongFeedbackColor = new Color(1f, 0.45f, 0.2f, 1f);

        [Header("Food Slot Color (Optional)")]
        public bool useFavoriteColorOnSelectedSlot = true;
        public Color correctSelectedColor = new Color(0.2f, 0.9f, 0.35f, 1f);
        public Color wrongSelectedColor = new Color(1f, 0.55f, 0.2f, 1f);


        [Header("Debug")]
        public bool debugLog = true;

        private AnimalController _currentAnimal;
        private FoodItem _selectedFood;
        private Dictionary<FoodItem, Image> _slotBackgrounds = new Dictionary<FoodItem, Image>();

        // sticky owner
        private InteractionTrigger _sourceTrigger;

        // NEW: chống trường hợp panel bị tắt ngoài luồng
        private bool _isOpen;

        private void Awake()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
            if (buffInfoText) buffInfoText.text = "";



            if (playButton != null)
            {
                playButton.onClick.RemoveListener(OnPlayButton);
                playButton.onClick.AddListener(OnPlayButton);
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveListener(OnCancelButton);
                cancelButton.onClick.AddListener(OnCancelButton);
            }
        }

        private void Update()
        {
            // Nếu panelRoot bị SetActive(false) trực tiếp từ inspector/button/animator
            // thì coi như "close" và phải complete sticky để không kẹt trigger.
            if (_isOpen && panelRoot != null && !panelRoot.activeInHierarchy)
            {
                if (debugLog)
                    Debug.Log("[MinigameInteractionPanel] panelRoot was closed externally -> force cleanup");

                ForceCloseFromExternal();
            }
        }

        public void ShowForAnimal(AnimalController animal, InteractionTrigger sourceTrigger)
        {
            _sourceTrigger = sourceTrigger;
            ShowForAnimal(animal);
        }

        // Existing API
        public void ShowForAnimal(AnimalController animal)
        {
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            _currentAnimal = animal;
            _selectedFood = null;
            if (buffInfoText) buffInfoText.text = "";

            ShowPanel(
                "Play Animal Rhythm?",
                $"{defaultQuestion}\n(-{animalEnergyCost} năng lượng)"
            );

            if (feedingSectionRoot) feedingSectionRoot.SetActive(true);
            RenderFoodList();

            // NEW: đảm bảo nút Start không bị disabled vĩnh viễn
            if (playButton != null)
                playButton.interactable = true;

            _isOpen = true;

            if (debugLog)
                Debug.Log("[MinigameInteractionPanel] ShowForAnimal OK");
        }

        private void ShowPanel(string title, string question)
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(true);
                AudioManager.Instance?.PlayOpenPanelSE();
            }
            if (titleText != null) titleText.text = title;
            if (questionText != null) questionText.text = question;
        }

        public void Hide()
        {
            if (!_isOpen) return;

            if (panelRoot != null) panelRoot.SetActive(false);

            _isOpen = false;

            // IMPORTANT: panel đóng thì complete sticky
            if (_sourceTrigger != null)
            {
                _sourceTrigger.CompleteStickyInteraction();
                _sourceTrigger = null;
            }
            else
            {
                // fallback safety
                if (_currentAnimal != null)
                {
                    _currentAnimal.SetInteractionLocked(false);
                    _currentAnimal.CancelLookAtPlayerNow();
                }
            }

            _currentAnimal = null;
            _selectedFood = null;

            if (debugLog)
                Debug.Log("[MinigameInteractionPanel] Hide -> complete sticky");
        }

        // NEW: dùng khi panelRoot bị đóng ngoài luồng
        private void ForceCloseFromExternal()
        {
            // đừng bật/tắt root nữa, vì nó đã off rồi
            _isOpen = false;

            if (_sourceTrigger != null)
            {
                _sourceTrigger.CompleteStickyInteraction();
                _sourceTrigger = null;
            }
            else
            {
                if (_currentAnimal != null)
                {
                    _currentAnimal.SetInteractionLocked(false);
                    _currentAnimal.CancelLookAtPlayerNow();
                }
            }

            _currentAnimal = null;
            _selectedFood = null;
        }

        private void RenderFoodList()
        {
            if (!foodContainer || !foodSlotPrefab) return;

            foreach (Transform child in foodContainer) Destroy(child.gameObject);
            _slotBackgrounds.Clear();

            if (!InventoryManager.HasInstance) return;

            var allItems = InventoryManager.Instance.All();

            foreach (var kvp in allItems)
            {
                FoodItem food = kvp.Key;
                int count = kvp.Value;
                if (count <= 0) continue;

                GameObject slotObj = Instantiate(foodSlotPrefab, foodContainer);

                var slotScript = slotObj.GetComponent<UIItemSlot>();
                if (slotScript) slotScript.Setup(food.icon, count);

                Button btn = slotObj.GetComponent<Button>();
                if (btn == null) btn = slotObj.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;

                FoodItem captured = food;
                btn.onClick.AddListener(() => OnFoodSelected(captured));

                Image bg = slotObj.GetComponent<Image>();
                if (bg)
                {
                    _slotBackgrounds[captured] = bg;
                    bg.color = normalColor;
                }
            }
        }

        private void OnFoodSelected(FoodItem food)
        {
            if (_selectedFood == food)
            {
                _selectedFood = null;
                UpdateSlotHighlights();

                if (buffInfoText)
                {
                    buffInfoText.text = "";
                    buffInfoText.color = Color.white;
                }
                return;
            }

            _selectedFood = food;
            UpdateSlotHighlights();

            if (buffInfoText)
            {
                // default
                buffInfoText.text = "";
                buffInfoText.color = Color.white;

                if (_currentAnimal != null && _currentAnimal.Definition != null)
                {
                    bool isCorrect = (_currentAnimal.Definition.favoriteFood == food);

                    buffInfoText.text = isCorrect ? correctFeedbackText : wrongFeedbackText;
                    buffInfoText.color = isCorrect ? correctFeedbackColor : wrongFeedbackColor;
                }
            }
        }


        private void UpdateSlotHighlights()
        {
            bool hasAnimalDef = (_currentAnimal != null && _currentAnimal.Definition != null);

            foreach (var kvp in _slotBackgrounds)
            {
                if (kvp.Value == null) continue;

                bool isSelected = (kvp.Key == _selectedFood);

                if (!isSelected)
                {
                    kvp.Value.color = normalColor;
                    continue;
                }

                // Selected slot
                if (useFavoriteColorOnSelectedSlot && hasAnimalDef && _selectedFood != null)
                {
                    bool isCorrect = (_currentAnimal.Definition.favoriteFood == _selectedFood);
                    kvp.Value.color = isCorrect ? correctSelectedColor : wrongSelectedColor;
                }
                else
                {
                    kvp.Value.color = selectedColor;
                }
            }
        }

        public void OnPlayButton()
        {
            if (_currentAnimal == null)
            {
                Debug.LogWarning("[MinigameInteractionPanel] No animal context.");
                return;
            }

            if (!UIManager.HasInstance)
            {
                Debug.LogWarning("[MinigameInteractionPanel] UIManager missing.");
                return;
            }

            if (debugLog)
                Debug.Log("[MinigameInteractionPanel] OnPlayButton -> RequestStartAnimalRhythm");
            AudioManager.Instance?.PlayInterfaceSE();
            UIManager.Instance.RequestStartAnimalRhythm(_currentAnimal, _selectedFood, animalEnergyCost);
        }

        public void OnCancelButton()
        {
            Hide();

            // optional: bật main UI lại nếu muốn sau này
            if (UIManager.HasInstance)
                UIManager.Instance.CloseAllPopups();
        }
    }
}
