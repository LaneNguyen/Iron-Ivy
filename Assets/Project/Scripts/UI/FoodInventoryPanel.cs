using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IronIvy.Core;
using IronIvy.Data;
using IronIvy.UI;

namespace IronIvy.UI
{
    public class FoodInventoryPanel : MonoBehaviour
    {
        [Header("Inventory UI Refs")]
        public Transform itemContainer;     // chỗ chứa mấy ô item
        public GameObject itemSlotPrefab;   // prefab ô item

        Coroutine waitRoutine;
        bool isSubscribed;

        void OnEnable()
        {
            // Đợi InventoryManager sẵn rồi mới đăng ký event
            waitRoutine = StartCoroutine(WaitForInventoryManager());
        }

        void OnDisable()
        {
            // Dừng coroutine nếu panel tắt
            if (waitRoutine != null)
            {
                StopCoroutine(waitRoutine);
                waitRoutine = null;
            }

            // Hủy đăng ký event nếu có
            if (isSubscribed && InventoryManager.HasInstance)
            {
                InventoryManager.Instance.OnInventoryChanged -= UpdateUI;
                isSubscribed = false;
            }
        }

        IEnumerator WaitForInventoryManager()
        {
            // Nếu lỡ gọi lại mà đã subscribe rồi thì thôi
            if (isSubscribed)
                yield break;

            // Chờ đến khi InventoryManager.Instance != null
            while (!InventoryManager.HasInstance)
                yield return null;

            // Đảm bảo không bị double-sub
            InventoryManager.Instance.OnInventoryChanged -= UpdateUI;
            InventoryManager.Instance.OnInventoryChanged += UpdateUI;
            isSubscribed = true;

            // Lần đầu bật panel thì update luôn cho chắc
            UpdateUI();
        }

        public void UpdateUI()
        {
            // Check ref cơ bản
            if (!itemContainer || !itemSlotPrefab)
            {
                Debug.LogWarning("[FoodInventoryPanel] Missing itemContainer or itemSlotPrefab", this);
                return;
            }

            // Xóa toàn bộ slot cũ trước khi build lại
            for (int i = itemContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(itemContainer.GetChild(i).gameObject);
            }

            // Trường hợp hiếm: bị gọi quá sớm khi Inventory chưa sẵn
            if (!InventoryManager.HasInstance)
            {
                Debug.LogWarning("[FoodInventoryPanel] InventoryManager.HasInstance == false trong UpdateUI", this);
                return;
            }

            var allItems = InventoryManager.Instance.All();
            int spawnCount = 0;

            // Trong hàm UpdateUI() của FoodInventoryPanel

    foreach (var kvp in allItems)
    {
        if (kvp.Value <= 0) continue;

        GameObject slotObj = Instantiate(itemSlotPrefab, itemContainer);
        slotObj.SetActive(true);
        slotObj.transform.localScale = Vector3.one;

        // Lấy script UI_ItemSlot ra để dùng
        var slotScript = slotObj.GetComponent<UIItemSlot>();
        if (slotScript)
        {
            // Truyền dữ liệu thẳng vào, không cần Find lung tung
            slotScript.Setup(kvp.Key.icon, kvp.Value);
        }
        else
        {
            Debug.LogError("Prefab UI_ItemSlot thiếu script UI_ItemSlot!");
        }
    }

            // Debug nhẹ cho dễ theo dõi
            // Debug.Log($"[FoodInventoryPanel] Spawned slots: {spawnCount}", this);
        }
    }
}
