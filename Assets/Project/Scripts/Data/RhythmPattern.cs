using UnityEngine;

namespace IronIvy.Data
{
    [CreateAssetMenu(menuName = "IronIvy/Rhythm Pattern")]
    public class RhythmPattern : ScriptableObject
    {
        public string patternId;
        public string displayName;
        [Min(1)] public int bpm = 80;
        public float hitWindowSeconds = 0.2f;

        [System.Serializable]
        public struct Step
        {
            public StepType type;
            [Min(1)] public int beats;
        }

        public enum StepType { Tap, Hold, Rest }

        public Step[] sequence = new Step[]
        {
            new Step{ type = StepType.Tap, beats = 1 },
            new Step{ type = StepType.Tap, beats = 1 },
            new Step{ type = StepType.Hold, beats = 2 }
        };
    }
}
