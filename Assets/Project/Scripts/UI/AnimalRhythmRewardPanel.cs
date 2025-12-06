using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IronIvy.Core;
using IronIvy.Gameplay.Animals;

namespace IronIvy.UI
{
    // Panel recap sau khi choi xong animal rhythm
    // lam giong plant reward panel nhung data khac xiu
    public class AnimalRhythmRewardPanel : MonoBehaviour
    {
        [Header("Root")]
        [Tooltip("Panel goc bat / tat. Neu de trong se dung chinh gameObject nay.")]
        public GameObject root;

        [Header("Texts")]
        public TextMeshProUGUI titleText;
        public TextMeshProUGUI animalNameText;
        public TextMeshProUGUI successText;
        public TextMeshProUGUI archiveGainText;
        public TextMeshProUGUI archiveCurrentText;

        [Header("Icon (optional)")]
        public Image animalIcon;

        private AnimalController _currentAnimal;
        private float _lastGainedArchive;

        private void Awake()
        {
            if (root == null)
                root = gameObject;

            // an panel luc moi vao scene
            root.SetActive(false);
        }

        // show ket qua minigame animal
        public void ShowAnimalRhythmResult(AnimalController animal, float successRatio, float archiveGained)
        {
            if (root == null)
                root = gameObject;

            _currentAnimal = animal;
            _lastGainedArchive = archiveGained;

            // GameObject chua script dang tat san trong editor -> bat len
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            // bat panel root
            if (!root.activeSelf)
                root.SetActive(true);

            // debug nho de chac panel thuc su chay
            Debug.Log("[AnimalRhythmRewardPanel] ShowAnimalRhythmResult called -> activating animal reward panel");

            // ==== Fill UI ====

            if (titleText != null)
                titleText.text = "Rhythm complete";

            // ten animal
            string displayName = "Animal";
            if (animal != null && animal.Definition != null && !string.IsNullOrEmpty(animal.Definition.displayName))
                displayName = animal.Definition.displayName;

            if (animalNameText != null)
                animalNameText.text = displayName;

            // success text + grade
            string grade = "Missed";
            if (successRatio >= 0.99f)
                grade = "Perfect";
            else if (successRatio >= 0.5f)
                grade = "Good";

            int percent = Mathf.RoundToInt(successRatio * 100f);
            if (successText != null)
                successText.text = $"Success: {percent}% ({grade})";

            // archive gain
            if (archiveGainText != null)
            {
                if (archiveGained > 0f)
                {
                    float rounded = Mathf.Round(archiveGained * 10f) / 10f;
                    archiveGainText.text = $"+{rounded}% Archive";
                }
                else
                {
                    archiveGainText.text = "No archive gained";
                }
            }

            // archive current tu ArchiveManager
            if (archiveCurrentText != null)
            {
                if (ArchiveManager.HasInstance)
                {
                    float cur = ArchiveManager.Instance.CurrentPercent;
                    int curInt = Mathf.RoundToInt(cur);
                    archiveCurrentText.text = $"Archive now: {curInt}%";
                }
                else
                {
                    archiveCurrentText.text = "";
                }
            }

            // icon neu muon show (co the gan sprite san trong inspector)
            if (animalIcon != null)
            {
                // giu nguyen sprite dang co, neu khong dung thi disable trong inspector
                animalIcon.enabled = animalIcon.sprite != null;
            }
        }

        public void Hide()
        {
            if (root == null)
                root = gameObject;

            root.SetActive(false);
        }

        // gan ham nay cho nut OK / Close tren panel
        public void OnConfirmButton()
        {
            Hide();

            // sau khi user confirm moi bat dau despawn one-shot
            if (_currentAnimal != null)
            {
                _currentAnimal.DespawnAfterMinigame();
            }

            _currentAnimal = null;
            _lastGainedArchive = 0f;
        }
    }
}
