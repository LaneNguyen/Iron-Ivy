using UnityEngine;
using System.Collections.Generic;

namespace IronIvy.Core
{
    public class RhythmManager : BaseManager<RhythmManager>
    {
        public List<IronIvy.Data.RhythmPattern> patterns = new List<IronIvy.Data.RhythmPattern>();

        public IronIvy.Data.RhythmPattern GetPattern(string id)
        {
            foreach (var p in patterns)
                if (p && p.patternId == id) return p;
            Debug.LogWarning($"[RhythmManager] Pattern {id} not found.");
            return null;
        }

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