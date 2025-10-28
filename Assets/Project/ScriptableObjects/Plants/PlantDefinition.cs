using UnityEngine;

namespace IronIvy.Gameplay
{
    [CreateAssetMenu(menuName = "Iron & Ivy/Definitions/Plant", fileName = "Plant_")]
    public class PlantDefinition : ScriptableObject
    {
        public string id;
        public string displayName;
        [Min(0)] public float growthTime = 10f;
        public int yieldAmount = 1;
        public GameObject plantPrefab;
        public Sprite icon;
    }
}