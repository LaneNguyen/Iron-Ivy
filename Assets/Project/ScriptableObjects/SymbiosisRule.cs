using UnityEngine;

namespace IronIvy.Gameplay
{
    public enum SymbiosisTrigger { PlantHarvested, NPCTrustReached, TimeOfDay }
    public enum SymbiosisEffect { AddScrap, SpeedGrowth, TrustUp }

    [CreateAssetMenu(menuName = "Iron & Ivy/Rules/Symbiosis Rule", fileName = "Rule_")]
    public class SymbiosisRule : ScriptableObject
    {
        public SymbiosisTrigger trigger;
        public string triggerParam; // ví dụ: plant id, trust >= X, time bucket
        public SymbiosisEffect effect;
        public int effectValue;     // ví dụ: +Scrap, +% tốc độ…
    }
}