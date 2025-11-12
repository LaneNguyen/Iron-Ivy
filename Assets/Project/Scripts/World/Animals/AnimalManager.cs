using UnityEngine;
using System.Collections.Generic;
using IronIvy.Data;

namespace IronIvy.Core
{
    public class AnimalManager : BaseManager<AnimalManager>
    {
        public List<AnimalDefinition> zone1Animals = new List<AnimalDefinition>();
        public List<AnimalDefinition> zone2Animals = new List<AnimalDefinition>();
        public AnimalDefinition Today { get; private set; }

        public void RerollTodayEncounter()
        {
            var list = (ZoneManager.Instance.CurrentZone == Zone.Zone2) ? zone2Animals : zone1Animals;
            if (list.Count > 0) Today = list[Random.Range(0, list.Count)];
        }
    }
}
