using System;
using System.Collections.Generic;
using UnityEngine;
using IronIvy.Data;

namespace IronIvy.Core
{
    public class InventoryManager : BaseManager<InventoryManager>
    {
        private readonly Dictionary<FoodItem, int> items = new Dictionary<FoodItem, int>();

        public event Action OnInventoryChanged;

        protected override void Awake()
        {
            base.Awake();
        }

        // gọi khi gameplay core bắt đầu (sau load)
        public void InitCore()
        {
            // Sync UI đúng 1 lần tại thời điểm hợp lý
            OnInventoryChanged?.Invoke();
        }

        public void AddFood(FoodItem item, int count = 1)
        {
            if (!item || count <= 0) return;

            items[item] = GetCount(item) + count;

            // Debug nhẹ (giữ được, nhưng đừng spam quá)
            // int listenerCount = OnInventoryChanged != null ? OnInventoryChanged.GetInvocationList().Length : 0;
            // Debug.Log($"[Inventory] ADDED: {item.displayName}. Listeners: {listenerCount}");

            OnInventoryChanged?.Invoke();
        }

        public bool Consume(FoodItem item, int count = 1)
        {
            if (!item || count <= 0) return false;

            int cur = GetCount(item);
            if (cur < count) return false;

            int newValue = cur - count;
            if (newValue <= 0) items.Remove(item);
            else items[item] = newValue;

            OnInventoryChanged?.Invoke();
            return true;
        }

        public int GetCount(FoodItem item) => items.TryGetValue(item, out var c) ? c : 0;

        public IEnumerable<KeyValuePair<FoodItem, int>> All() => items;

        public void ClearAll(bool notify = true)
        {
            items.Clear();
            if (notify) OnInventoryChanged?.Invoke();
        }

        // SaveLoadManager sẽ gọi cái này sau khi parse data từ PlayerPrefs
        public void SetLoadedData(Dictionary<FoodItem, int> loaded, bool notify = false)
        {
            items.Clear();

            if (loaded != null)
            {
                foreach (var kv in loaded)
                {
                    if (!kv.Key) continue;
                    if (kv.Value <= 0) continue;
                    items[kv.Key] = kv.Value;
                }
            }

            if (notify) OnInventoryChanged?.Invoke();
        }

        [ContextMenu("Print Inventory Debug")]
        public void PrintDebug()
        {
            Debug.Log($"=== INVENTORY CONTENT ({items.Count} items) ===");
            foreach (var kvp in items)
                Debug.Log($"- {kvp.Key.displayName}: {kvp.Value}");
        }
    }
}
