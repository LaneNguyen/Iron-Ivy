using UnityEngine;
using IronIvy.Gameplay.Rhythm;

namespace IronIvy.Data
{
    [CreateAssetMenu(menuName = "IronIvy/Animal Definition")]
    public class AnimalDefinition : ScriptableObject
    {
        public string id;
        public string displayName;

        [Header("Rhythm (multi-pattern)")]
        public RhythmPattern[] patterns;    //nhiều pattern
        public RhythmPlaybackMode playbackMode = RhythmPlaybackMode.Sequential;

        [Header("Animation names (Animal)")]
        public string goodAnim = "Good";
        public string badAnim = "Bad";

        [Header("IV-17 Reactions")]
        // Các tên state trong Animator của IV-17 sẽ được play khi bấm dc “GOOD”
        public string[] iv17Reactions;

        [Header("Audio/FX")]
        public AudioClip loopSfx;
        public GameObject successVFX;
    }
}