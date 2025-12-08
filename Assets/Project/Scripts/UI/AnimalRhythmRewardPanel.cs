using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IronIvy.Core;
using IronIvy.Gameplay.Animals;
using IronIvy.Data; // Để dùng FoodItem

namespace IronIvy.UI
{
    public class AnimalRhythmRewardPanel : MonoBehaviour
    {
        [Header("Root")]
        public GameObject root;

        [Header("Texts")]
        public TextMeshProUGUI titleText;
        public TextMeshProUGUI animalNameText;
        public TextMeshProUGUI successText;
        public TextMeshProUGUI archiveGainText;
        public TextMeshProUGUI archiveCurrentText;
        
        //  Text hiển thị loot
        [Tooltip("Text hiển thị vật phẩm nhận được")]
        public TextMeshProUGUI lootText; 

        [Header("Icon (optional)")]
        public Image animalIcon;

        private AnimalController _currentAnimal;
        private float _lastGainedArchive;

        private void Awake()
        {
            if (root == null) root = gameObject;
            root.SetActive(false);
        }

        // Thêm tham số Loot
        public void ShowAnimalRhythmResult(AnimalController animal, float successRatio, float archiveGained, FoodItem lootItem, int lootCount)
        {
            if (root == null) root = gameObject;
            _currentAnimal = animal;
            _lastGainedArchive = archiveGained;

            if (!gameObject.activeSelf) gameObject.SetActive(true);
            if (!root.activeSelf) root.SetActive(true);

            // Fill UI
            if (titleText != null) titleText.text = "Rhythm complete";

            // Name
            string displayName = "Animal";
            if (animal != null && animal.Definition != null) displayName = animal.Definition.displayName;
            if (animalNameText != null) animalNameText.text = displayName;

            // Success
            string grade = "Missed";
            if (successRatio >= 0.99f) grade = "Perfect";
            else if (successRatio >= 0.5f) grade = "Good";
            int percent = Mathf.RoundToInt(successRatio * 100f);
            if (successText != null) successText.text = $"Success: {percent}% ({grade})";

            // Archive
            if (archiveGainText != null)
            {
                if (archiveGained > 0f) archiveGainText.text = $"+{archiveGained:F1}% Archive";
                else archiveGainText.text = "No archive gained";
            }

            // Archive Current
            if (archiveCurrentText != null)
            {
                if (ArchiveManager.HasInstance)
                {
                    float cur = ArchiveManager.Instance.CurrentPercent;
                    archiveCurrentText.text = $"Archive now: {Mathf.RoundToInt(cur)}%";
                }
                else archiveCurrentText.text = "";
            }
            
            //  Loot Info
            if (lootText != null)
            {
                if (lootItem != null && lootCount > 0)
                {
                    lootText.text = $"Loot: +{lootCount} {lootItem.displayName}";
                    if (lootItem.icon != null) 
                    {
                        // Nếu muốn hiện icon loot, có thể mở rộng thêm Image lootIcon
                    }
                }
                else
                {
                    lootText.text = "";
                }
            }

            // Icon
            if (animalIcon != null) animalIcon.enabled = animalIcon.sprite != null;
        }

        public void Hide()
        {
            if (root == null) root = gameObject;
            root.SetActive(false);
        }

        public void OnConfirmButton()
        {
            Hide();
            if (_currentAnimal != null) _currentAnimal.DespawnAfterMinigame();
            _currentAnimal = null;
            _lastGainedArchive = 0f;
        }
    }
}