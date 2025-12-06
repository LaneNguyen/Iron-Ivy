using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IronIvy.Data;
using System.Collections.Generic;
using IronIvy.Gameplay.Rhythm;

namespace IronIvy.UI
{
    /// <summary>
    /// Panel thu hoạch sau khi chơi xong plant rhythm.
    /// Đã nâng cấp để hiển thị danh sách item (Dictionary) thay vì 1 item lẻ.
    /// </summary>
    public class PlantRhythmRewardPanel : MonoBehaviour
    {
        [Header("Root")]
        public GameObject root;

        [Header("Texts")]
        public TextMeshProUGUI titleText;
        public TextMeshProUGUI trustText;
        public TextMeshProUGUI hitMissText;

        [Header("Rewards List (Setup Mới)")]
        [Tooltip("Container chứa các slot item (Gắn Horizontal Layout Group)")]
        public Transform rewardContainer; 
        
        [Tooltip("Prefab UI_ItemSlot (Dùng chung với Inventory)")]
        public GameObject itemSlotPrefab;

        // Biến cũ (Không dùng nữa nhưng giữ lại để đỡ lỗi Inspector nếu lỡ quên xóa)
        [HideInInspector] public TextMeshProUGUI plantNameText;
        [HideInInspector] public TextMeshProUGUI rewardText;
        [HideInInspector] public Image rewardIcon;

        private void Awake()
        {
            if (root == null) root = gameObject;
            root.SetActive(false);
        }

        /// <summary>
        /// Show kết quả harvest với danh sách quà.
        /// </summary>
        public void Show(Dictionary<FoodItem, int> rewards, int hit, int miss, float trust)
        {
            if (root == null) root = gameObject;
            gameObject.SetActive(true);
            root.SetActive(true);

            // 1. Update thông tin chung
            if (titleText) titleText.text = "Harvest Complete!";
            if (trustText) trustText.text = $"Trust: {Mathf.RoundToInt(trust)}%";
            if (hitMissText) hitMissText.text = $"Perfect: {hit} | Miss: {miss}";

            // 2. Spawn Item Slots vào Container
            if (rewardContainer && itemSlotPrefab)
            {
                // Xóa slot cũ
                foreach (Transform child in rewardContainer) Destroy(child.gameObject);

                if (rewards != null && rewards.Count > 0)
                {
                    foreach (var kvp in rewards)
                    {
                        FoodItem item = kvp.Key;
                        int count = kvp.Value;

                        if (count <= 0) continue;

                        GameObject slotObj = Instantiate(itemSlotPrefab, rewardContainer);
                        slotObj.SetActive(true);
                        slotObj.transform.localScale = Vector3.one;

                        // Dùng script UI_ItemSlot để setup visual (Icon + Text)
                        var slotScript = slotObj.GetComponent<UIItemSlot>();
                        if (slotScript)
                        {
                            slotScript.Setup(item.icon, count);
                        }
                    }
                }
                else
                {
                    // Nếu không có quà (Trust thấp hoặc lỗi)
                    // Có thể instantiate một text báo "No Reward" ở đây nếu muốn
                }
            }
        }

        public void Hide()
        {
            if (root == null) root = gameObject;
            root.SetActive(false);
        }

        public void OnClickClose()
        {
            Hide();
            
            // Ép Main UI cập nhật lại kho đồ lần cuối để đồng bộ visual
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