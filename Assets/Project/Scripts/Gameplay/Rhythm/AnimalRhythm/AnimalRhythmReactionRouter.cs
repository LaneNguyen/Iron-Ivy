using IronIvy.Core;
using IronIvy.Gameplay.Animals;
using UnityEngine;

namespace IronIvy.Gameplay.Rhythm
{
    public class AnimalRhythmReactionRouter : MonoBehaviour
    {
        public static AnimalRhythmReactionRouter Instance { get; private set; }

        private AnimalController _activeAnimal;
        private AnimalReactionController _activeReaction;

        private bool _subscribed;
        private int _lastHit;
        private int _lastMiss;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable() => TrySubscribe();
        private void Start() => TrySubscribe();

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void TrySubscribe()
        {
            if (_subscribed) return;
            if (!ListenManager.HasInstance) return;

            ListenManager.Instance.OnRhythmHUDUpdate += HandleRhythmHudUpdate;
            ListenManager.Instance.OnRhythmHUDHide += HandleRhythmHudHide;

            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            if (!ListenManager.HasInstance) { _subscribed = false; return; }

            ListenManager.Instance.OnRhythmHUDUpdate -= HandleRhythmHudUpdate;
            ListenManager.Instance.OnRhythmHUDHide -= HandleRhythmHudHide;

            _subscribed = false;
        }

        public void SetActiveAnimal(AnimalController animal)
        {
            _activeAnimal = animal;
            _activeReaction = null;

            if (_activeAnimal != null)
                _activeReaction = _activeAnimal.GetComponentInChildren<AnimalReactionController>(true);

            _lastHit = 0;
            _lastMiss = 0;

            // ADAPTED: new controller API
            if (_activeReaction != null && _activeAnimal != null)
            {
                _activeReaction.SetDefinition(_activeAnimal.Definition);
                // No SetNeutralImmediate() anymore; SetDefinition() already forces Neutral.
            }
        }

        public void ClearActiveAnimal()
        {
            // Optional: if controller exists, force neutral by re-setting definition or disable state.
            // We keep it minimal: just clear references.
            _activeAnimal = null;
            _activeReaction = null;

            _lastHit = 0;
            _lastMiss = 0;
        }

        private void HandleRhythmHudHide()
        {
            ClearActiveAnimal();
        }

        private void HandleRhythmHudUpdate(ListenManager.RhythmHUDUpdatePayload payload)
        {
            if (payload == null) return;

            if (_activeReaction == null)
            {
                _lastHit = payload.hit;
                _lastMiss = payload.miss;
                return;
            }

            int dh = payload.hit - _lastHit;
            int dm = payload.miss - _lastMiss;

            _lastHit = payload.hit;
            _lastMiss = payload.miss;

            if (dm > 0) _activeReaction.OnMiss();
            else if (dh > 0) _activeReaction.OnHit();
            else
            {
                // ADAPTED: controller không còn OnNonScorableTick()
                // Shield! / Rest / update không scorable -> ignore
            }
        }
    }
}
