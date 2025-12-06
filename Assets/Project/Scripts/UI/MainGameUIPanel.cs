using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IronIvy.Core;
using IronIvy.Gameplay.Rhythm;
using IronIvy.UI;

namespace IronIvy.UI
{
    public class MainGameUIPanel : MonoBehaviour
    {
        [Header("Energy UI")]
        public TextMeshProUGUI energyText;        
        public Slider energySlider;               
        public TextMeshProUGUI plantCostText;     
        public TextMeshProUGUI animalCostText;    

        [Header("Energy Config")]
        public int displayMaxEnergy = 6;
        public int animalBaseEnergyCost = 1;

        [Header("Start Panels")]
        public PlantRhythmStartPanel plantRhythmStartPanel;
        // Bỏ hoặc không dùng reference này nữa
        public AnimalRhythmStartPanel animalRhythmStartPanel;   

        // === [TÍCH HỢP FOOD PANEL] ===
        [Header("Inventory UI")]
        [Tooltip("Kéo script FoodInventoryPanel vào đây để MainUI tự động refresh nó khi bật")]
        public FoodInventoryPanel foodInventoryPanel; 
        // =============================

        [Header("Minigame State")]
        public bool isMinigameRunning;

        public enum MinigameType { None, Plant, Animal }
        public MinigameType currentMinigameType = MinigameType.None;

        public Button plantStartButton;
        public Button animalStartButton;
        public TextMeshProUGUI minigameStatusText;

        [Header("Archive / Day / Trust (Optional)")]
        public Slider archiveSlider;
        public TextMeshProUGUI archiveText;
        public Button endDayButton;
        public TextMeshProUGUI dayText;
        public TextMeshProUGUI trustPopupText;
        public float trustPopupDuration = 2f;

        private Coroutine trustPopupCoroutine;

        //==================================================
        //  Unity
        //==================================================

        private void OnEnable()
        {
            if (ListenManager.HasInstance)
            {
                ListenManager.Instance.OnEnergyChanged += OnEnergyChanged;
                ListenManager.Instance.OnArchiveChanged += OnArchiveChanged;
                ListenManager.Instance.OnMinigameStarted += OnMinigameStarted;
                ListenManager.Instance.OnMinigameStopped += OnMinigameStopped;
                ListenManager.Instance.OnDayEnded += OnDayEnded;
                ListenManager.Instance.OnTrustSuccess += OnTrustSuccess;
            }

            // ⭐ FORCE REFRESH ENERGY
            RefreshEnergyUIFromManager();

            if (ArchiveManager.HasInstance)
                OnArchiveChanged(ArchiveManager.Instance.GetPercent());

            // ⭐ FORCE REFRESH FOOD INVENTORY
            // Đảm bảo khi Main UI bật lên, list đồ ăn được cập nhật mới nhất
            if (foodInventoryPanel != null)
            {
                foodInventoryPanel.UpdateUI();
            }
        }

        private void OnDisable()
        {
            if (ListenManager.HasInstance)
            {
                ListenManager.Instance.OnEnergyChanged -= OnEnergyChanged;
                ListenManager.Instance.OnMinigameStarted -= OnMinigameStarted;
                ListenManager.Instance.OnMinigameStopped -= OnMinigameStopped;
                ListenManager.Instance.OnArchiveChanged -= OnArchiveChanged;
                ListenManager.Instance.OnDayEnded -= OnDayEnded;
                ListenManager.Instance.OnTrustSuccess -= OnTrustSuccess;
            }
        }

        //==================================================
        //  ENERGY
        //==================================================

        private void RefreshEnergyUIFromManager()
        {
            if (!EnergyManager.HasInstance) return;
            int current = EnergyManager.Instance.Current;
            RefreshEnergyUI(current);
        }

        private void RefreshEnergyUI(int current)
        {
            int max = Mathf.Max(displayMaxEnergy, 1);

            if (energyText != null) energyText.text = $"{current} / {max}";
            if (energySlider != null) energySlider.value = Mathf.Clamp01((float)current / max);

            if (plantCostText != null && plantRhythmStartPanel != null)
                plantCostText.text = $"-{plantRhythmStartPanel.baseEnergyCost} energy";

            if (animalCostText != null)
                animalCostText.text = $"-{animalBaseEnergyCost} energy";
        }

        private void OnEnergyChanged(int current)
        {
            RefreshEnergyUI(current);
        }

        //==================================================
        //  MINIGAME STATE
        //==================================================

        private void OnMinigameStarted()
        {
            isMinigameRunning = true;
            RefreshMinigameStateUI();
        }

        private void OnMinigameStopped()
        {
            isMinigameRunning = false;
            currentMinigameType = MinigameType.None;
            RefreshMinigameStateUI();
        }

        private void RefreshMinigameStateUI()
        {
            if (plantStartButton != null) plantStartButton.interactable = !isMinigameRunning;
            if (animalStartButton != null) animalStartButton.interactable = !isMinigameRunning;

            if (minigameStatusText != null)
                minigameStatusText.text = isMinigameRunning ? "Playing rhythm..." : "";
        }

        //==================================================
        //  ENTRY POINT BUTTONS
        //==================================================

        public void OnClickPlayPlantRhythm()
        {
            if (isMinigameRunning) return;
            if (plantRhythmStartPanel != null) plantRhythmStartPanel.Show();
        }

        public void OnClickPlayAnimalRhythm()
        {
            // Người chơi cần đi lại gần con thú để chơi.
            Debug.Log("Please approach an animal to play Animal Rhythm.");
        }

        //==================================================
        //  ARCHIVE / DAY / TRUST
        //==================================================

        private void OnArchiveChanged(float value)
        {
            if (archiveSlider != null) archiveSlider.value = Mathf.Clamp01(value);
            if (archiveText != null)
            {
                int percentInt = Mathf.RoundToInt(value * 100f);
                archiveText.text = percentInt + "%";
            }
        }

        private void OnDayEnded()
        {
            if (endDayButton != null) endDayButton.interactable = false;
        }

        private void OnTrustSuccess()
        {
            ShowTrustPopup("Trust up!");
        }

        private void ShowTrustPopup(string message)
        {
            if (trustPopupText == null) return;
            if (trustPopupCoroutine != null) StopCoroutine(trustPopupCoroutine);
            trustPopupCoroutine = StartCoroutine(TrustPopupRoutine(message));
        }

        private System.Collections.IEnumerator TrustPopupRoutine(string message)
        {
            trustPopupText.gameObject.SetActive(true);
            trustPopupText.text = message;
            float t = 0f;
            while (t < trustPopupDuration)
            {
                t += Time.deltaTime;
                yield return null;
            }
            trustPopupText.gameObject.SetActive(false);
            trustPopupCoroutine = null;
        }

        public void SetDayText(string text)
        {
            if (dayText != null) dayText.text = text;
        }
    }
}