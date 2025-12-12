using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IronIvy.Gameplay.Animals;
using IronIvy.Data;
using System.Collections;

namespace IronIvy.UI
{
    public class AnimalRhythmRewardPanel : MonoBehaviour
    {
        [Header("Root")]
        public GameObject root;

        [Header("Texts (Title / Name)")]
        [Tooltip("Title để designer tự set ngoài Editor, code không động vào nữa")]
        public TextMeshProUGUI titleText;

        [Tooltip("Tên animal (lấy từ AnimalDefinition.displayName)")]
        public TextMeshProUGUI animalNameText;

        [Header("Trust / Success")]
        [Tooltip("Chỉ hiển thị số % trust/success (vd: 85). Label / % để object khác lo")]
        public TextMeshProUGUI trustText;

        [Header("Hit / Miss (separate)")]
        [Tooltip("Text hiển thị số Perfect (hit)")]
        public TextMeshProUGUI perfectText;

        [Tooltip("Text hiển thị số Miss")]
        public TextMeshProUGUI missText;

        [Header("Archive")]
        [Tooltip("Text hiển thị số Archive vừa nhận, sẽ animate từ 0 lên")]
        public TextMeshProUGUI archiveGainText;

        [Tooltip("Thời gian animate archive từ 0 -> final (giây)")]
        public float archiveAnimDuration = 0.6f;

        [Tooltip("Scale bắt đầu cho archive text (nếu muốn nó nảy lên nhẹ)")]
        public float archiveStartScale = 0.6f;

        [Header("Reward Message + Loot Slots")]
        [Tooltip("Text hiển thị thông điệp khi có/không có loot")]
        public TextMeshProUGUI rewardMessageText;

        [Tooltip("Câu khi có thu hoạch được item")]
        public string hasRewardMessage = "Bạn nhận được";

        [Tooltip("Câu khi không thu hoạch được gì")]
        public string noRewardMessage = "Bạn không thu được gì...";

        [Tooltip("Container chứa các slot loot (Horizontal/Vertical Layout Group)")]
        public Transform rewardContainer;

        [Tooltip("Prefab UI_ItemSlot dùng để hiển thị icon + số lượng loot")]
        public GameObject itemSlotPrefab;

        [Header("Trust Number Animation")]
        [Tooltip("Thời gian số trust chạy từ 0 -> final (giây)")]
        public float trustAnimDuration = 0.6f;

        [Tooltip("Scale bắt đầu của số trust (vd: 0.6 => nhỏ, sau đó lớn lên 1.0)")]
        public float trustStartScale = 0.6f;

        [Header("Icon (optional)")]
        public Image animalIcon;

        // field cũ, giờ không dùng nữa (để tránh lỗi compile nếu còn gán trong inspector)
        [HideInInspector] public TextMeshProUGUI successText;
        [HideInInspector] public TextMeshProUGUI archiveCurrentText;
        [HideInInspector] public TextMeshProUGUI lootText;

        private AnimalController _currentAnimal;
        private float _lastGainedArchive;

        private Coroutine _trustAnimRoutine;
        private Coroutine _archiveAnimRoutine;

        private void Awake()
        {
            if (root == null) root = gameObject;
            root.SetActive(false);

            if (trustText != null)
                trustText.transform.localScale = Vector3.one;

            if (archiveGainText != null)
                archiveGainText.transform.localScale = Vector3.one;
        }

        /// <summary>
        /// Show kết quả minigame animal
        /// successRatio: 0..1 (dùng làm trust %)
        /// archiveGained: lượng archive vừa cộng
        /// lootItem/lootCount: phần thưởng thêm
        /// hitCount/missCount: tổng hit / miss của minigame
        /// </summary>
        public void ShowAnimalRhythmResult(
            AnimalController animal,
            float successRatio,
            float archiveGained,
            FoodItem lootItem,
            int lootCount,
            int hitCount,
            int missCount)
        {
            if (root == null) root = gameObject;
            _currentAnimal = animal;
            _lastGainedArchive = archiveGained;

            if (!gameObject.activeSelf) gameObject.SetActive(true);
            if (!root.activeSelf) root.SetActive(true);

            // Title để designer tự set ngoài editor, không set ở đây nữa

            // Tên animal
            string displayName = "Animal";
            if (animal != null && animal.Definition != null)
                displayName = animal.Definition.displayName;

            if (animalNameText != null)
                animalNameText.text = displayName;

            // Hit / Miss: tách ra 2 text riêng
            if (perfectText != null)
                perfectText.text = hitCount.ToString();

            if (missText != null)
                missText.text = missCount.ToString();

            // Tính % và grade
            int percent = Mathf.RoundToInt(successRatio * 100f);


            // Trust: animate số từ 0 -> percent, sau đó animate archive
            if (trustText != null)
            {
                if (_trustAnimRoutine != null)
                    StopCoroutine(_trustAnimRoutine);

                _trustAnimRoutine = StartCoroutine(AnimateTrustThenArchive(percent, archiveGained));
            }
            else
            {
                // nếu không có trustText thì set thẳng archive
                if (archiveGainText != null)
                    SetArchiveInstant(archiveGained);
            }


            // Reward message + loot slots
            bool hasReward = (lootItem != null && lootCount > 0);

            if (rewardMessageText != null)
                rewardMessageText.text = hasReward ? hasRewardMessage : noRewardMessage;

            if (rewardContainer != null && itemSlotPrefab != null)
            {
                foreach (Transform child in rewardContainer)
                    Destroy(child.gameObject);

                if (hasReward)
                {
                    GameObject slotObj = Instantiate(itemSlotPrefab, rewardContainer);
                    slotObj.SetActive(true);
                    slotObj.transform.localScale = Vector3.one;

                    var slotScript = slotObj.GetComponent<UIItemSlot>();
                    if (slotScript != null)
                    {
                        slotScript.Setup(lootItem.icon, lootCount);
                    }
                    else
                    {
                        var iconImg = slotObj.GetComponentInChildren<Image>();
                        if (iconImg != null && lootItem.icon != null)
                            iconImg.sprite = lootItem.icon;

                        var countText = slotObj.GetComponentInChildren<TextMeshProUGUI>();
                        if (countText != null)
                            countText.text = lootCount.ToString();
                    }
                }
            }

            // Icon animal (nếu có)
            if (animalIcon != null)
            {
                if (animal != null && animal.Definition != null && animal.Definition.icon != null)
                {
                    animalIcon.sprite = animal.Definition.icon;
                    animalIcon.enabled = true;
                }
                else
                {
                    animalIcon.enabled = false;
                }
            }
        }

        // === ANIMATION SEQUENCE ===
        // 1. Animate trust 0 -> trustPercent
        // 2. Sau khi xong, animate archive 0 -> archiveValue
        private IEnumerator AnimateTrustThenArchive(int trustPercent, float archiveValue)
        {
            // animate trust
            if (trustText != null)
            {
                float duration = Mathf.Max(0.01f, trustAnimDuration);
                float t = 0f;

                trustText.transform.localScale = Vector3.one * Mathf.Max(0.01f, trustStartScale);

                while (t < duration)
                {
                    t += Time.deltaTime;
                    float normalized = Mathf.Clamp01(t / duration);

                    float value = Mathf.Lerp(0f, trustPercent, normalized);
                    int displayValue = Mathf.RoundToInt(value);
                    trustText.text = displayValue.ToString();

                    float scale = Mathf.Lerp(trustStartScale, 1f, normalized);
                    trustText.transform.localScale = new Vector3(scale, scale, 1f);

                    yield return null;
                }

                trustText.text = trustPercent.ToString() + "%";
                trustText.transform.localScale = Vector3.one;
            }

            _trustAnimRoutine = null;

            // sau khi trust xong thì tới archive
            if (archiveGainText != null)
            {
                if (_archiveAnimRoutine != null)
                    StopCoroutine(_archiveAnimRoutine);

                _archiveAnimRoutine = StartCoroutine(AnimateArchiveNumber(archiveValue));
            }
        }

        // animate archive từ 0 -> archiveValue
        private IEnumerator AnimateArchiveNumber(float archiveValue)
        {
            if (archiveGainText == null)
                yield break;

            if (archiveValue <= 0f)
            {
                archiveGainText.text = "No archive gained";
                archiveGainText.transform.localScale = Vector3.one;
                _archiveAnimRoutine = null;
                yield break;
            }

            float duration = Mathf.Max(0.01f, archiveAnimDuration);
            float t = 0f;

            archiveGainText.transform.localScale = Vector3.one * Mathf.Max(0.01f, archiveStartScale);

            while (t < duration)
            {
                t += Time.deltaTime;
                float normalized = Mathf.Clamp01(t / duration);

                float value = Mathf.Lerp(0f, archiveValue, normalized);
                archiveGainText.text = $"+{value:F1}%";

                float scale = Mathf.Lerp(archiveStartScale, 1f, normalized);
                archiveGainText.transform.localScale = new Vector3(scale, scale, 1f);

                yield return null;
            }

            archiveGainText.text = $"+{archiveValue:F1}%";
            archiveGainText.transform.localScale = Vector3.one;
            _archiveAnimRoutine = null;
        }

        private void SetArchiveInstant(float archiveValue)
        {
            if (archiveGainText == null) return;

            if (archiveValue > 0f)
                archiveGainText.text = $"+{archiveValue:F1}%";
            else
                archiveGainText.text = "0% :()";

            archiveGainText.transform.localScale = Vector3.one;
        }

        public void Hide()
        {
            if (root == null) root = gameObject;
            root.SetActive(false);
        }

        public void OnConfirmButton()
        {
            Hide();

            if (_currentAnimal != null)
                _currentAnimal.DespawnAfterMinigame();

            _currentAnimal = null;
            _lastGainedArchive = 0f;
        }
    }
}
