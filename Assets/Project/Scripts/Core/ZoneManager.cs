using UnityEngine;

namespace IronIvy.Core
{
    public enum Zone { Zone1, Zone2 }

    public class ZoneManager : BaseManager<ZoneManager>
    {
        public Zone CurrentZone { get; private set; } = Zone.Zone1;

        public void InitAtArchive(float archivePercent)
        {
            CurrentZone = (archivePercent >= 75f) ? Zone.Zone2 : Zone.Zone1;
        }

        public int GetCommEnergyCost() => (CurrentZone == Zone.Zone2) ? 2 : 1;
        public int GetArchiveReward()  => (CurrentZone == Zone.Zone2) ? 4 : 2;
    }
}
