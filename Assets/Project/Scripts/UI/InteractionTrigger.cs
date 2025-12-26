using IronIvy.Core;
using IronIvy.Gameplay.Animals;
using UnityEngine;
using UnityEngine.Events;

namespace IronIvy.Gameplay.Interaction
{
    [DisallowMultipleComponent]
    public class InteractionTrigger : MonoBehaviour
    {
        [Header("Settings")]
        public KeyCode interactKey = KeyCode.F;
        public string playerTag = "Player";

        [Header("Visual Feedback")]
        public GameObject interactPrompt;
        public UnityEvent<bool> onToggleHighlight;

        [Header("Logic")]
        public UnityEvent onInteract;

        [Header("Sticky Interaction")]
        public AnimalController animalToLock;

        public bool ignoreExitWhileInteracting = true;
        public bool disableTriggerColliderWhileInteracting = true;

        [Header("Collider Binding")]
        public Collider interactionTriggerCollider;

        [Header("Anti spam")]
        public float interactCooldown = 0.25f;

        [Header("Auto Reset")]
        public float autoResetSeconds = 3f;
        public float reEnableDelaySeconds = 0.25f;

        [Header("Debug")]
        public bool debugLog = false;

        // ===== Runtime =====
        private bool _isPlayerInZone;
        private bool _isInteracting;
        private float _nextAllowedTime;

        private Collider _triggerCol;
        private Collider _playerCol;

        private Coroutine _autoResetRoutine;
        private Coroutine _reEnableRoutine;

        private void Awake()
        {
            BindTriggerCollider();

            if (animalToLock == null)
                animalToLock = GetComponentInParent<AnimalController>();

            if (interactPrompt)
                interactPrompt.SetActive(false);
        }

        private void OnEnable()
        {
            BindTriggerCollider();

            _isInteracting = false;
            _isPlayerInZone = false;
            _playerCol = null;

            StopAllCoroutines();

            if (_triggerCol != null)
                _triggerCol.enabled = true;

            if (interactPrompt)
                interactPrompt.SetActive(false);

            onToggleHighlight?.Invoke(false);
        }

        private void OnDisable()
        {
            StopAllCoroutines();

            if (ListenManager.HasInstance)
                ListenManager.Instance.OnRhythmResultClosed -= OnRewardPanelClosed;
        }

        private void BindTriggerCollider()
        {
            if (interactionTriggerCollider != null)
            {
                _triggerCol = interactionTriggerCollider;
                return;
            }

            var cols = GetComponentsInChildren<Collider>(true);
            foreach (var c in cols)
            {
                if (c.isTrigger)
                {
                    _triggerCol = c;
                    return;
                }
            }

            _triggerCol = GetComponent<Collider>();
        }

        private void Update()
        {
            if (!_isPlayerInZone) return;
            if (_isInteracting) return;
            if (Time.time < _nextAllowedTime) return;

            if (Input.GetKeyDown(interactKey))
            {
                _nextAllowedTime = Time.time + interactCooldown;
                _isInteracting = true;

                if (interactPrompt)
                    interactPrompt.SetActive(false);

                onToggleHighlight?.Invoke(false);

                if (animalToLock != null)
                    animalToLock.SetInteractionLocked(true);

                if (disableTriggerColliderWhileInteracting && _triggerCol != null)
                    _triggerCol.enabled = false;

                if (_autoResetRoutine != null)
                    StopCoroutine(_autoResetRoutine);

                if (autoResetSeconds > 0f)
                    _autoResetRoutine = StartCoroutine(AutoResetAfterSeconds(autoResetSeconds));

                // ===== LISTEN reward panel close =====
                if (ListenManager.HasInstance)
                {
                    ListenManager.Instance.OnRhythmResultClosed -= OnRewardPanelClosed;
                    ListenManager.Instance.OnRhythmResultClosed += OnRewardPanelClosed;
                }

                onInteract?.Invoke();
            }
        }

        private System.Collections.IEnumerator AutoResetAfterSeconds(float seconds)
        {
            yield return new WaitForSeconds(seconds);

            if (_isInteracting)
                CancelStickyInteraction();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag(playerTag)) return;

            _playerCol = other;
            _isPlayerInZone = true;

            if (!_isInteracting && interactPrompt)
                interactPrompt.SetActive(true);

            onToggleHighlight?.Invoke(true);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag(playerTag)) return;

            if (_isInteracting && ignoreExitWhileInteracting)
                return;

            _isPlayerInZone = false;
            _playerCol = null;

            if (interactPrompt)
                interactPrompt.SetActive(false);

            onToggleHighlight?.Invoke(false);
        }

        // ===== CALLED BY LISTEN MANAGER =====
        private void OnRewardPanelClosed()
        {
            if (debugLog)
                Debug.Log($"[InteractionTrigger] Reward panel closed -> CompleteSticky ({name})");

            if (ListenManager.HasInstance)
                ListenManager.Instance.OnRhythmResultClosed -= OnRewardPanelClosed;

            CompleteStickyInteraction();
        }

        public void CompleteStickyInteraction()
        {
            if (!_isInteracting) return;

            _isInteracting = false;

            if (_autoResetRoutine != null)
                StopCoroutine(_autoResetRoutine);

            if (_reEnableRoutine != null)
                StopCoroutine(_reEnableRoutine);

            _reEnableRoutine = StartCoroutine(ReEnableAfterDelay(reEnableDelaySeconds));
        }

        private void CancelStickyInteraction()
        {
            CompleteStickyInteraction();
        }

        private System.Collections.IEnumerator ReEnableAfterDelay(float delay)
        {
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            if (_triggerCol != null)
                _triggerCol.enabled = true;

            if (animalToLock != null)
            {
                animalToLock.SetInteractionLocked(false);
                animalToLock.CancelLookAtPlayerNow();
            }

            RecheckPlayerOverlap();
        }

        private void RecheckPlayerOverlap()
        {
            if (_triggerCol == null || _playerCol == null)
            {
                _isPlayerInZone = false;
                if (interactPrompt) interactPrompt.SetActive(false);
                onToggleHighlight?.Invoke(false);
                return;
            }

            bool inside = _triggerCol.bounds.Intersects(_playerCol.bounds);
            _isPlayerInZone = inside;

            if (inside)
            {
                if (!_isInteracting && interactPrompt)
                    interactPrompt.SetActive(true);

                onToggleHighlight?.Invoke(true);
            }
            else
            {
                if (interactPrompt)
                    interactPrompt.SetActive(false);

                onToggleHighlight?.Invoke(false);
            }
        }

        public void ForceHidePrompt()
        {
            if (interactPrompt)
                interactPrompt.SetActive(false);
        }
    }
}
