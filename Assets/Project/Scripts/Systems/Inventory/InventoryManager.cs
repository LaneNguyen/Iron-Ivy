using System.Collections.Generic;
using UnityEngine;
using IronIvy.Data;

namespace IronIvy.Core
{
    public class InventoryManager : BaseManager<InventoryManager>
    {
        private Dictionary<FoodItem, int> items = new Dictionary<FoodItem, int>();

        public void AddFood(FoodItem item, int count = 1)
        {
            if (!item || count <= 0) return;
            items[item] = GetCount(item) + count;
        }

        public bool Consume(FoodItem item, int count = 1)
        {
            if (!item || count <= 0) return false;
            var cur = GetCount(item);
            if (cur < count) return false;
            items[item] = cur - count;
            if (items[item] <= 0) items.Remove(item);
            return true;
        }

        public int GetCount(FoodItem item) => items.TryGetValue(item, out var c) ? c : 0;
        public IEnumerable<KeyValuePair<FoodItem, int>> All() => items;
    }
}
