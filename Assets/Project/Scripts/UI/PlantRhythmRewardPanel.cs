using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IronIvy.Data;
using System.Collections;
using System.Collections.Generic;
using IronIvy.Gameplay.Rhythm;

namespace IronIvy.UI
{
    // panel kết quả sau khi chơi xong plant rhythm
    // - show trust, hit/miss
    // - spawn list item reward từ Dictionary<FoodItem, int>
    public class PlantRhythmRewardPanel : MonoBehaviour
    {
        [Header("Root")]
        public GameObject root;

        [Header("Texts")]
        [Tooltip("Title để designer tự set trong Editor, code không động vào")]
        public TextMeshProUGUI titleText;

        [Tooltip("Chỉ hiển thị số trust (vd: 85). Label / % để object khác lo")]
        public TextMeshProUGUI trustText;

        [Tooltip("Text hiển thị số Perfect (hit)")]
        public TextMeshProUGUI perfectText;

        [Tooltip("Text hiển thị số Miss")]
        public TextMeshProUGUI missText;

        [Header("Reward Message")]
        [Tooltip("Text hiển thị thông điệp khi có/không có thu hoạch")]
        public TextMeshProUGUI rewardMessageText;

        [Tooltip("Câu khi có thu hoạch được item")]
        public string hasRewardMessage = "Bạn nhận được";

        [Tooltip("Câu khi không thu hoạch được gì")]
        public string noRewardMessage = "Bạn không thu được gì...";

        [Header("Rewards List")]
        [Tooltip("Container chứa các slot item (gắn Horizontal/Vertical Layout Group)")]
        public Transform rewardContainer;

        [Tooltip("Prefab UI_ItemSlot (dùng chung với Inventory)")]
        public GameObject itemSlotPrefab;

        [Header("Trust Number Animation")]
        [Tooltip("Thời gian số trust chạy từ 0 -> final (giây)")]
        public float trustAnimDuration = 0.6f;

        [Tooltip("Scale bắt đầu của số trust (ví dụ 0.6 => hơi nhỏ, sau đó nảy lên 1.0)")]
        public float trustStartScale = 0.6f;

        private Coroutine _trustAnimRoutine;

        private void Awake()
        {
            if (root == null) root = gameObject;
            root.SetActive(false);

            if (trustText != null)
                trustText.transform.localScale = Vector3.one;
        }

        // show kết quả harvest với danh sách reward
        // - rewards: item + số lượng
        // - hit/miss + trust %
        public void Show(Dictionary<FoodItem, int> rewards, int hit, int miss, float trust)
        {
            if (root == null) root = gameObject;
            gameObject.SetActive(true);
            root.SetActive(true);

            // titleText: để nguyên, không set trong code nữa

            // Trust: chỉ số, chạy animation từ 0 -> final
            if (trustText != null)
            {
                int trustRounded = Mathf.RoundToInt(trust);

                if (_trustAnimRoutine != null)
                    StopCoroutine(_trustAnimRoutine);

                _trustAnimRoutine = StartCoroutine(AnimateTrustNumber(trustRounded));
            }

            // Perfect / Miss: tách text riêng, chỉ set số
            if (perfectText)
                perfectText.text = hit.ToString();

            if (missText)
                missText.text = miss.ToString();

            // kiểm tra xem có thu hoạch được item nào không
            bool hasReward = false;
            if (rewards != null)
            {
                foreach (var kvp in rewards)
                {
                    if (kvp.Key == null) continue;
                    if (kvp.Value > 0)
                    {
                        hasReward = true;
                        break;
                    }
                }
            }

            // cập nhật message
            if (rewardMessageText != null)
            {
                rewardMessageText.text = hasReward ? hasRewardMessage : noRewardMessage;
            }

            // spawn item slots nếu có reward
            if (rewardContainer && itemSlotPrefab)
            {
                // clear slot cũ
                foreach (Transform child in rewardContainer)
                    Destroy(child.gameObject);

                if (hasReward)
                {
                    foreach (var kvp in rewards)
                    {
                        FoodItem item = kvp.Key;
                        int count = kvp.Value;

                        if (item == null || count <= 0) continue;

                        GameObject slotObj = Instantiate(itemSlotPrefab, rewardContainer);
                        slotObj.SetActive(true);
                        slotObj.transform.localScale = Vector3.one;

                        // nếu dùng UIItemSlot chung với inventory
                        var slotScript = slotObj.GetComponent<UIItemSlot>();
                        if (slotScript != null)
                        {
                            slotScript.Setup(item.icon, count);
                        }
                        else
                        {
                            // fallback: nếu prefab không có UIItemSlot
                            var iconImg = slotObj.GetComponentInChildren<Image>();
                            if (iconImg != null && item.icon != null)
                                iconImg.sprite = item.icon;

                            var countText = slotObj.GetComponentInChildren<TextMeshProUGUI>();
                            if (countText != null)
                                countText.text = count.ToString();
                        }
                    }
                }
                else
                {
                    // không có reward: chỉ để container trống, message lo phần feedback
                }
            }
        }

        private IEnumerator AnimateTrustNumber(int targetValue)
        {
            if (trustText == null)
                yield break;

            if (trustAnimDuration <= 0.01f)
            {
                trustText.text = targetValue.ToString() + "%";
                trustText.transform.localScale = Vector3.one;
                yield break;
            }

            float t = 0f;
            trustText.transform.localScale = Vector3.one * Mathf.Max(0.01f, trustStartScale);

            while (t < trustAnimDuration)
            {
                t += Time.deltaTime;
                float normalized = Mathf.Clamp01(t / trustAnimDuration);

                float value = Mathf.Lerp(0f, targetValue, normalized);
                int displayValue = Mathf.RoundToInt(value);
                trustText.text = displayValue.ToString();

                float scale = Mathf.Lerp(trustStartScale, 1f, normalized);
                trustText.transform.localScale = new Vector3(scale, scale, 1f);

                yield return null;
            }

            trustText.text = targetValue.ToString() + "%";
            trustText.transform.localScale = Vector3.one;
            _trustAnimRoutine = null;
        }

        public void Hide()
        {
            if (root == null) root = gameObject;
            root.SetActive(false);
        }

        public void OnClickClose()
        {
            Hide();

            // ép Main UI update lại food panel
            var mainUI = FindObjectOfType<MainGameUIPanel>(true);
            if (mainUI != null)
            {
                mainUI.gameObject.SetActive(true);

                if (mainUI.foodInventoryPanel != null)
                {
                    mainUI.foodInventoryPanel.UpdateUI();
                }
            }
        }
    }
}
