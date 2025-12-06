using System.Collections.Generic;
using UnityEngine;
using IronIvy.Data;
using System;

namespace IronIvy.Core
{
    public class InventoryManager : BaseManager<InventoryManager>
    {
        private Dictionary<FoodItem, int> items = new Dictionary<FoodItem, int>();
        public event Action OnInventoryChanged;


      public void AddFood(FoodItem item, int count = 1)
        {
            if (!item || count <= 0) return;
            
            items[item] = GetCount(item) + count;
            
            // --- [DEBUG MỚI] ---
            int listenerCount = OnInventoryChanged != null ? OnInventoryChanged.GetInvocationList().Length : 0;
            Debug.Log($"[Inventory] ADDED: {item.displayName}. Đang bắn tin cho {listenerCount} người nghe.");
            // -------------------

            OnInventoryChanged?.Invoke();
        }
        public bool Consume(FoodItem item, int count = 1)
        {
            if (!item || count <= 0) return false;
            var cur = GetCount(item);
            if (cur < count) return false;
            items[item] = cur - count;
            if (items[item] <= 0) items.Remove(item);
            OnInventoryChanged?.Invoke();
            return true;
        }

        public int GetCount(FoodItem item) => items.TryGetValue(item, out var c) ? c : 0;
        public IEnumerable<KeyValuePair<FoodItem, int>> All() => items;

        // === [DEBUG TOOLS] ===
        // Chuột phải vào component InventoryManager chọn "Print Inventory" để kiểm tra
        [ContextMenu("Print Inventory Debug")]
        public void PrintDebug()
        {
            Debug.Log($"=== INVENTORY CONTENT ({items.Count} items) ===");
            foreach(var kvp in items)
            {
                Debug.Log($"- {kvp.Key.displayName}: {kvp.Value}");
            }
        }
    }
}