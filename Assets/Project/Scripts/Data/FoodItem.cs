using UnityEngine;

namespace IronIvy.Data
{
    [CreateAssetMenu(menuName = "IronIvy/Food Item")]
    public class FoodItem : ScriptableObject
    {
        public string id;
        public string displayName;
        public Sprite icon;
        [Range(0,30)] public int baseTrustBoost;
        public int basePrice = 1;
    }
}
