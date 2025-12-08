using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IronIvy.Data;
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
        public TextMeshProUGUI titleText;
        public TextMeshProUGUI trustText;
        public TextMeshProUGUI hitMissText;

        [Header("Rewards List (Setup Mới)")]
        [Tooltip("Container chứa các slot item (gắn Horizontal Layout Group)")]
        public Transform rewardContainer; 
        
        [Tooltip("Prefab UI_ItemSlot (dùng chung với Inventory)")]
        public GameObject itemSlotPrefab;

        // biến cũ giữ lại cho an toàn Inspector
        [HideInInspector] public TextMeshProUGUI plantNameText;
        [HideInInspector] public TextMeshProUGUI rewardText;
        [HideInInspector] public Image rewardIcon;

        private void Awake()
        {
            if (root == null) root = gameObject;
            root.SetActive(false);
        }

            // show kết quả harvest với danh sách reward
            // - rewards: item + số lượng
            // - hit/miss + trust %
        public void Show(Dictionary<FoodItem, int> rewards, int hit, int miss, float trust)
        {
            if (root == null) root = gameObject;
            gameObject.SetActive(true);
            root.SetActive(true);

            // info chung
            if (titleText) titleText.text = "Harvest Complete!";
            if (trustText) trustText.text = $"Trust: {Mathf.RoundToInt(trust)}%";
            if (hitMissText) hitMissText.text = $"Perfect: {hit} | Miss: {miss}";

            // spawn item slots vào container
            if (rewardContainer && itemSlotPrefab)
            {
                // clear slot cũ
                foreach (Transform child in rewardContainer)
                    Destroy(child.gameObject);

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

                        // dùng UIItemSlot để set icon + số lượng
                        var slotScript = slotObj.GetComponent<UIItemSlot>();
                        if (slotScript)
                        {
                            slotScript.Setup(item.icon, count);
                        }
                    }
                }
                else
                {
                    // trường hợp không có quà
                    // có thể thêm 1 text "No reward" nếu cần
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
