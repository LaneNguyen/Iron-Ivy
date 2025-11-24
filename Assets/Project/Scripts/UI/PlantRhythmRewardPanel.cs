using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IronIvy.Data;

namespace IronIvy.UI
{
    /// <summary>
    /// Panel thu hoạch sau khi chơi xong plant rhythm.
    /// </summary>
    public class PlantRhythmRewardPanel : MonoBehaviour
    {
        [Header("Root")]
        [Tooltip("Panel gốc cần bật/tắt. Nếu để trống sẽ dùng chính gameObject này.")]
        public GameObject root;

        [Header("Texts")]
        public TextMeshProUGUI titleText;
        public TextMeshProUGUI plantNameText;
        public TextMeshProUGUI trustText;
        public TextMeshProUGUI hitMissText;
        public TextMeshProUGUI rewardText;

        [Header("Visual (optional)")]
        public Image rewardIcon;

        private void Awake()
        {
            // Nếu object start active thì Awake sẽ chạy và ẩn panel lúc đầu.
            // Nếu object start inactive thì Awake sẽ chạy lần đầu khi GameObject được SetActive(true).
            if (root == null)
                root = gameObject;

            root.SetActive(false);
        }

        /// <summary>
        /// Show kết quả harvest.
        /// </summary>
        public void Show(PlantDefinition plant, int hit, int miss, float trust,
                         int yieldCount, string yieldItemName = null, Sprite yieldSprite = null)
        {
            // fallback nếu root chưa gán (trong trường hợp Awake chưa chạy do object đang inactive)
            if (root == null)
                root = gameObject;

            // nếu GameObject chứa script đang tắt sẵn trong editor -> bật lên
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            // bật panel root
            if (!root.activeSelf)
                root.SetActive(true);

            // Debug nho nhỏ để chắc chắn Show thực sự chạy
            Debug.Log("[PlantRhythmRewardPanel] Show called -> activating reward panel");

            // ==== Fill UI ====

            if (titleText != null)
                titleText.text = "Harvest complete";

            if (plantNameText != null)
                plantNameText.text = plant != null ? plant.name : "Plant";

            if (trustText != null)
                trustText.text = $"Trust: {Mathf.RoundToInt(trust)}";

            if (hitMissText != null)
                hitMissText.text = $"Hit {hit} / Miss {miss}";

            if (rewardText != null)
            {
                if (yieldCount > 0)
                {
                    string itemName = yieldItemName;
                    if (string.IsNullOrEmpty(itemName) && plant != null && plant.yieldItem != null)
                        itemName = plant.yieldItem.name;

                    rewardText.text = $"{yieldCount} x {itemName}";
                }
                else
                {
                    rewardText.text = "No reward this time";
                }
            }

            if (rewardIcon != null)
            {
                if (yieldSprite != null)
                {
                    rewardIcon.sprite = yieldSprite;
                    rewardIcon.enabled = true;
                }
                else
                {
                    rewardIcon.enabled = false;
                }
            }
        }

        public void Hide()
        {
            if (root == null)
                root = gameObject;

            root.SetActive(false);
        }

        /// <summary>
        /// Gán hàm này cho nút Close/OK trong Inspector.
        /// </summary>
        public void OnConfirmButton()
        {
            Hide();
        }
    }
}
