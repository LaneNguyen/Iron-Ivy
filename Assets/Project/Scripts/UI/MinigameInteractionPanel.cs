using UnityEngine;
using TMPro;
using IronIvy.Gameplay.Rhythm;
using IronIvy.Gameplay.Animals;
using IronIvy.Core;
using IronIvy.UI;

namespace IronIvy.UI
{
    // Panel hỏi "Chơi minigame không?" khi lại gần đối tượng
    public class MinigameInteractionPanel : MonoBehaviour
    {
        public static MinigameInteractionPanel Instance { get; private set; }

        [Header("Root")]
        public GameObject panelRoot;

        [Header("Texts")]
        public TextMeshProUGUI titleText;
        public TextMeshProUGUI questionText;

        [TextArea]
        public string defaultQuestion = "Chơi với bạn nhỏ này?";

        [Header("Config")]
        [Tooltip("Cost khi chơi với thú (trừ ngay khi bấm Play)")]
        [SerializeField] int animalEnergyCost = 1;
        
        [Tooltip("Cost fallback cho Plant khi test trực tiếp không qua StartPanel")]
        [SerializeField] int plantDirectCost = 1; 

        // context animal
        private AnimalController _currentAnimal;
        private ClickAnimalRhythmMinigame _currentAnimalMinigame;

        // context plant
        private ClickPlantRhythmMinigame _currentPlantMinigame;
        private PlantRhythmStartPanel _currentPlantStartPanel;

        private void Awake()
        {
            Instance = this;
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        // ---------------------
        // SETUP CONTEXT
        // ---------------------
        public void ShowForAnimal(AnimalController animal, ClickAnimalRhythmMinigame minigame)
        {
            _currentAnimal = animal;
            _currentAnimalMinigame = minigame;
            
            // Reset plant context
            _currentPlantMinigame = null;
            _currentPlantStartPanel = null;

            ShowPanel("Play Animal Rhythm?", $"{defaultQuestion}\n(-{animalEnergyCost} Energy)");
        }

        public void ShowForPlant(ClickPlantRhythmMinigame minigame, PlantRhythmStartPanel startPanel)
        {
            _currentPlantMinigame = minigame;
            _currentPlantStartPanel = startPanel;
            
            // Reset animal context
            _currentAnimal = null;
            _currentAnimalMinigame = null;

            // Nếu có start panel, lấy cost hiển thị từ đó cho đúng visual
            int displayCost = (startPanel != null) ? startPanel.baseEnergyCost : plantDirectCost;
            
            ShowPanel("Play Plant Rhythm?", $"{defaultQuestion}\n(-{displayCost} Energy)");
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
        }

        // ---------------------
        // BUTTON ACTIONS
        // ---------------------
        public void OnPlayButton()
        {
            // CASE 1: ANIMAL - Trừ Energy -> Chơi luôn
            if (_currentAnimal != null && _currentAnimalMinigame != null)
            {
                if (EnergyManager.HasInstance)
                {
                    if (!EnergyManager.Instance.TrySpend(animalEnergyCost))
                    {
                        Debug.LogWarning("[MinigameInteractionPanel] Not enough energy for animal.");
                        // TODO: Thêm effect rung lắc hoặc popup báo thiếu energy
                        return; 
                    }
                }
                _currentAnimalMinigame.RequestPlay(_currentAnimal);
            }
            // CASE 2: PLANT (Có Panel chọn) - Chỉ mở Panel -> Panel lo trừ Energy sau
            else if (_currentPlantStartPanel != null)
            {
                _currentPlantStartPanel.Show();
            }
            // CASE 3: PLANT (Direct Test) - Trừ Energy -> Chơi luôn
            else if (_currentPlantMinigame != null)
            {
                if (EnergyManager.HasInstance)
                {
                    if (!EnergyManager.Instance.TrySpend(plantDirectCost))
                    {
                        Debug.LogWarning("[MinigameInteractionPanel] Not enough energy for plant (direct).");
                        return;
                    }
                }
                _currentPlantMinigame.StartGame();
            }

            Hide();
        }

        public void OnCancelButton()
        {
            Hide();
        }
    }
}