using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using IronIvy.Data;
using IronIvy.Gameplay.Rhythm;
using IronIvy.Core;

namespace IronIvy.UI
{
    /// <summary>
    /// Panel chọn cây để start ClickPlantRhythmMinigame.
    /// Hỗ trợ trường hợp GameObject chứa script bị tắt sẵn trong editor.
    /// </summary>
    public class PlantRhythmStartPanel : MonoBehaviour
    {
        [Header("Root")]
        [Tooltip("Panel gốc cần bật/tắt. Nếu để trống sẽ dùng chính gameObject này.")]
        public GameObject root;

        [Header("Energy UI")]
        public TextMeshProUGUI energyText;
        [Tooltip("Mặc định tốn bao nhiêu energy cho 1 lần chơi.")]
        public int baseEnergyCost = 1;

        [Header("Plant List UI")]
        public Transform plantButtonContainer;
        public Button plantButtonPrefab;

        [Header("Data")]
        public List<PlantDefinition> availablePlants = new List<PlantDefinition>();

        [Header("Minigame")]
        public ClickPlantRhythmMinigame minigame;

        //=====================================================
        //  Unity Events
        //=====================================================

        // Lưu ý: nếu GameObject start inactive thì Awake/OnEnable
        // sẽ chỉ chạy sau lần đầu SetActive(true).
        private void Awake()
        {
            // KHÔNG setActive(false) ở đây,
            // vì nếu object đang tắt trong editor thì Awake không chạy.
            // Để logic ẩn/hiện dùng Show/Hide lo.
        }

        private void OnEnable()
        {
            if (EventBus.HasInstance)
            {
                EventBus.Instance.OnEnergyChanged += OnEnergyChanged;
            }
        }

        private void OnDisable()
        {
            if (EventBus.HasInstance)
            {
                EventBus.Instance.OnEnergyChanged -= OnEnergyChanged;
            }
        }

        private void OnEnergyChanged(int current)
        {
            RefreshEnergyText();
        }

        //=====================================================
        //  Public API
        //=====================================================

        /// <summary>
        /// Gọi từ button "Play plant rhythm".
        /// Hỗ trợ cả trường hợp GameObject đang tắt từ trước.
        /// </summary>
        public void Show()
        {
            // nếu root chưa gán thì dùng luôn gameObject hiện tại
            if (root == null)
                root = gameObject;

            // nếu GameObject đang inactive (bị tắt từ editor) -> bật nó lên
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            // bật panel root lên
            if (!root.activeSelf)
                root.SetActive(true);

            RefreshEnergyText();
            BuildPlantButtons();
        }

        public void Hide()
        {
            if (root == null)
                root = gameObject;

            root.SetActive(false);
        }

        //=====================================================
        //  Energy helpers
        //=====================================================

        private int GetCurrentEnergy()
        {
            if (EnergyManager.HasInstance)
                return EnergyManager.Instance.Current;
            return 0;
        }

        private void RefreshEnergyText()
        {
            if (energyText == null) return;

            int cur = GetCurrentEnergy();
            energyText.text = $"Energy: {cur} (-{baseEnergyCost} per run)";
        }

        //=====================================================
        //  Plant buttons
        //=====================================================

        private void BuildPlantButtons()
        {
            if (plantButtonContainer == null || plantButtonPrefab == null)
                return;

            // clear cũ
            for (int i = plantButtonContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(plantButtonContainer.GetChild(i).gameObject);
            }

            foreach (var plant in availablePlants)
            {
                if (plant == null) continue;

                Button btn = Instantiate(plantButtonPrefab, plantButtonContainer);
                var label = btn.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null)
                    label.text = plant.name;

                var captured = plant;
                btn.onClick.AddListener(() => OnPlantSelected(captured));
            }
        }

        private void OnPlantSelected(PlantDefinition plant)
        {
            if (minigame == null)
            {
                Debug.LogWarning("[PlantRhythmStartPanel] Missing ClickPlantRhythmMinigame reference.");
                return;
            }

            if (!EnergyManager.HasInstance)
            {
                Debug.LogWarning("[PlantRhythmStartPanel] No EnergyManager instance in scene.");
                return;
            }

            // thử trừ energy
            if (!EnergyManager.Instance.TrySpend(baseEnergyCost))
            {
                Debug.Log("[PlantRhythmStartPanel] Not enough energy.");
                RefreshEnergyText();
                return;
            }

            RefreshEnergyText();

            // gán plant rồi start
            minigame.plant = plant;
            minigame.StartGame();

            Hide();
        }
    }
}
