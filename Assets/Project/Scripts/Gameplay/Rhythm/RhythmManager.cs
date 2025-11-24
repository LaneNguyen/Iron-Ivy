using UnityEngine;
using System.Collections.Generic;

namespace IronIvy.Core
{
    // simple manager giữ list RhythmPattern cho game
    public class RhythmManager : BaseManager<RhythmManager>
    {
        // drag pattern vô list này trong inspector cho nhanh
        public List<IronIvy.Data.RhythmPattern> patterns = new List<IronIvy.Data.RhythmPattern>();

        // lấy pattern theo id string, nếu không thấy thì log warning
        public IronIvy.Data.RhythmPattern GetPattern(string id)
        {
            foreach (var p in patterns)
                if (p && p.patternId == id) return p;

            Debug.LogWarning($"[RhythmManager] Pattern {id} not found.");
            return null;
        }

        // helper generic shuffle cho list, dùng lại chỗ khác cũng được
        public static void Shuffle<T>(IList<T> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                int j = Random.Range(i, list.Count);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
