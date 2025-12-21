using IronIvy.Data;
using UnityEngine;


namespace IronIvy.Gameplay.Animals
{
    public enum AnimalReactionState
    {
        Neutral,
        Sad,
        Angry,
        Happy
    }

    public class AnimalReactionController : MonoBehaviour
    {
        [Header("Output")]
        public SpriteRenderer targetSpriteRenderer;

        [Header("Runtime")]
        [SerializeField] private AnimalReactionState state = AnimalReactionState.Neutral;

        [Header("Logic Tuning (fallback if definition missing)")]
        [Min(1)] public int happyStreakThresholdFallback = 4;
        [Min(0f)] public float missReactionSecondsFallback = 0.55f;
        [Min(0f)] public float happyHoldSecondsFallback = 0.65f;
        [Min(0f)] public float streakDecaySecondsFallback = 1.25f;
        public bool alternateSadAngryFallback = false;

        // Context (NO sprites stored here)
        [SerializeField] private AnimalDefinition _def;

        private int _streak;
        private float _lastHitTime;
        private float _stateUntil;
        private bool _flip;

        private void Reset()
        {
            if (targetSpriteRenderer == null)
                targetSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        private void Awake()
        {
            // Auto-bind definition from AnimalController if not assigned
            if (_def == null)
            {
                var ac = GetComponentInParent<AnimalController>();
                if (ac != null)
                    _def = ac.Definition;
            }
        }

        private void OnEnable()
        {
            // Ensure starts in neutral
            SetState(AnimalReactionState.Neutral, 0f);
        }

        public void SetDefinition(AnimalDefinition def)
        {
            _def = def;
            SetState(AnimalReactionState.Neutral, 0f);
        }

        public void OnHit()
        {
            _streak++;
            _lastHitTime = Time.time;

            int threshold = GetHappyThreshold();
            if (_streak >= threshold)
            {
                SetState(AnimalReactionState.Happy, GetHappyHoldSeconds());
            }
            else
            {
                if (state != AnimalReactionState.Happy)
                    SetState(AnimalReactionState.Neutral, 0f);
            }
        }

        public void OnMiss()
        {
            _streak = 0;

            var missState = AnimalReactionState.Sad;

            if (GetAlternateSadAngry())
            {
                _flip = !_flip;
                missState = _flip ? AnimalReactionState.Sad : AnimalReactionState.Angry;
            }

            SetState(missState, GetMissSeconds());
        }

        private void Update()
        {
            // decay streak
            float decay = GetStreakDecaySeconds();
            if (_streak > 0 && decay > 0f && (Time.time - _lastHitTime) > decay)
                _streak = 0;

            // timeout -> neutral
            if (state != AnimalReactionState.Neutral && _stateUntil > 0f && Time.time >= _stateUntil)
                SetState(AnimalReactionState.Neutral, 0f);
        }

        private void SetState(AnimalReactionState newState, float duration)
        {
            state = newState;
            _stateUntil = duration > 0f ? Time.time + duration : 0f;

            // IMPORTANT: sprite is read from AnimalDefinition.reactionVisuals every time
            if (targetSpriteRenderer != null)
            {
                var sprite = GetSpriteForState(newState);
                if (sprite != null)
                    targetSpriteRenderer.sprite = sprite;
            }
        }

        private Sprite GetSpriteForState(AnimalReactionState s)
        {
            if (_def == null) return null;

            switch (s)
            {
                case AnimalReactionState.Neutral: return _def.reactionVisuals.neutral;
                case AnimalReactionState.Sad: return _def.reactionVisuals.sad;
                case AnimalReactionState.Angry: return _def.reactionVisuals.angry;
                case AnimalReactionState.Happy: return _def.reactionVisuals.happy;
            }
            return null;
        }

        private int GetHappyThreshold()
        {
            if (_def != null && _def.reactionVisuals.happyStreakThreshold > 0)
                return _def.reactionVisuals.happyStreakThreshold;
            return happyStreakThresholdFallback;
        }

        private float GetMissSeconds()
        {
            if (_def != null && _def.reactionVisuals.missReactionSeconds > 0f)
                return _def.reactionVisuals.missReactionSeconds;
            return missReactionSecondsFallback;
        }

        private float GetHappyHoldSeconds()
        {
            if (_def != null && _def.reactionVisuals.happyHoldSeconds > 0f)
                return _def.reactionVisuals.happyHoldSeconds;
            return happyHoldSecondsFallback;
        }

        private float GetStreakDecaySeconds()
        {
            if (_def != null && _def.reactionVisuals.streakDecaySeconds > 0f)
                return _def.reactionVisuals.streakDecaySeconds;
            return streakDecaySecondsFallback;
        }

        private bool GetAlternateSadAngry()
        {
            if (_def != null)
                return _def.reactionVisuals.alternateSadAngry;
            return alternateSadAngryFallback;
        }
    }
}
