using UnityEngine;

namespace IronIvy.Gameplay
{
    [CreateAssetMenu(menuName = "Iron & Ivy/Definitions/NPC", fileName = "NPC_")]
    public class NPCDefinition : ScriptableObject
    {
        public string npcName;
        [Range(0, 100)] public int trustStart = 0;

        [System.Serializable]
        public struct TradeItem
        {
            public string itemId;
            public int priceScrap;
            public int qty;
        }
        public TradeItem[] tradeTable;
        public Sprite portrait;
    }
}