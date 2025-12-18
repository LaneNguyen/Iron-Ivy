using UnityEngine;
using UnityEngine.Events;
using IronIvy.Gameplay.Animals;
using IronIvy.Core;

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
        [Tooltip("Hook OpenAnimalInteractionPanelSticky() vào đây")]
        public UnityEvent onInteract;

        [Header("Sticky Interaction")]
        public AnimalController animalToLock;

        [Tooltip("Ignore trigger exit trong lúc interacting")]
        public bool ignoreExitWhileInteracting = true;

        [Tooltip("Disable trigger collider trong lúc interacting")]
        public bool disableTriggerColliderWhileInteracting = true;

        [Header("Collider Binding (IMPORTANT)")]
        [Tooltip("Kéo đúng collider trigger dùng cho interaction vào đây. Nếu bỏ trống, script sẽ tự tìm collider isTrigger=true.")]
        public Collider interactionTriggerCollider;

        [Header("Anti spam")]
        public float interactCooldown = 0.25f;

        [Header("Debug")]
        public bool debugLog = false;

        // ===== Runtime state =====
        private bool _isPlayerInZone;
        private bool _isInteracting;
        private float _nextAllowedTime;

        private Collider _triggerCol;
        private Collider _playerCol;

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
            // pooled spawn: đảm bảo collider trigger không bị "kẹt disabled" từ vòng trước
            BindTriggerCollider();

            _isInteracting = false;
            _isPlayerInZone = false;
            _playerCol = null;

            if (_triggerCol != null)
                _triggerCol.enabled = true;

            if (interactPrompt)
                interactPrompt.SetActive(false);

            onToggleHighlight?.Invoke(false);
        }

        private void BindTriggerCollider()
        {
            // 1) ưu tiên collider Lane kéo tay
            if (interactionTriggerCollider != null)
            {
                _triggerCol = interactionTriggerCollider;
                if (debugLog && !_triggerCol.isTrigger)
                    Debug.LogWarning($"[InteractionTrigger] {name} interactionTriggerCollider is NOT trigger. Please set isTrigger=true on that collider.");
                return;
            }

            // 2) auto-pick: ưu tiên collider trigger ngay trên object này
            var colsHere = GetComponents<Collider>();
            for (int i = 0; i < colsHere.Length; i++)
            {
                if (colsHere[i] != null && colsHere[i].isTrigger)
                {
                    _triggerCol = colsHere[i];
                    return;
                }
            }

            // 3) auto-pick: tìm trong children (include inactive)
            var colsChild = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colsChild.Length; i++)
            {
                if (colsChild[i] != null && colsChild[i].isTrigger)
                {
                    _triggerCol = colsChild[i];
                    return;
                }
            }

            // 4) fallback cuối: lấy đại collider trên object (nhưng không chỉnh isTrigger nữa)
            _triggerCol = GetComponent<Collider>();

            if (debugLog)
                Debug.LogWarning($"[InteractionTrigger] {name} cannot find any trigger collider. Please assign 'interactionTriggerCollider' manually.");
        }

        private void Update()
        {
            bool keyDown = Input.GetKeyDown(interactKey);

            if (debugLog)
            {
                Debug.Log(
                    $"[InteractionTrigger][Update] {name} " +
                    $"inZone={_isPlayerInZone} interacting={_isInteracting} keyDown={keyDown}"
                );
            }

            if (!_isPlayerInZone) return;
            if (_isInteracting) return;
            if (Time.time < _nextAllowedTime) return;

            if (keyDown)
            {
                _nextAllowedTime = Time.time + interactCooldown;
                _isInteracting = true;

                if (debugLog)
                    Debug.Log($"[InteractionTrigger] Interact START ({name})");

                if (interactPrompt)
                    interactPrompt.SetActive(false);

                onToggleHighlight?.Invoke(false);

                if (animalToLock != null)
                    animalToLock.SetInteractionLocked(true);

                // chỉ disable đúng trigger collider, không bao giờ đụng collider physical
                if (disableTriggerColliderWhileInteracting && _triggerCol != null)
                    _triggerCol.enabled = false;

                try
                {
                    onInteract?.Invoke();
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[InteractionTrigger] onInteract error: {ex}");
                    CancelStickyInteraction();
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag(playerTag)) return;

            _playerCol = other;
            _isPlayerInZone = true;

            if (!_isInteracting)
            {
                if (interactPrompt)
                    interactPrompt.SetActive(true);

                onToggleHighlight?.Invoke(true);
            }

            if (debugLog)
                Debug.Log($"[InteractionTrigger][Enter] {name} player entered");
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag(playerTag)) return;

            if (_isInteracting && ignoreExitWhileInteracting)
            {
                if (debugLog)
                    Debug.Log($"[InteractionTrigger][Exit] ignored (sticky)");
                return;
            }

            _isPlayerInZone = false;
            _playerCol = null;

            if (interactPrompt)
                interactPrompt.SetActive(false);

            onToggleHighlight?.Invoke(false);

            if (debugLog)
                Debug.Log($"[InteractionTrigger][Exit] {name} player exited");
        }

        // ===== Entry point để UI gọi =====
        public void OpenAnimalInteractionPanelSticky()
        {
            if (!UIManager.HasInstance || animalToLock == null)
            {
                CancelStickyInteraction();
                return;
            }

            UIManager.Instance.ShowAnimalInteraction(animalToLock, this);
        }

        // ===== Khi panel đóng / cancel =====
        public void CompleteStickyInteraction()
        {
            if (!_isInteracting) return;

            if (debugLog)
                Debug.Log($"[InteractionTrigger] CompleteSticky ({name})");

            _isInteracting = false;

            if (_triggerCol != null)
                _triggerCol.enabled = true;

            if (animalToLock != null)
            {
                animalToLock.SetInteractionLocked(false);
                animalToLock.CancelLookAtPlayerNow();
            }

            RecheckPlayerOverlap();
        }

        private void CancelStickyInteraction()
        {
            if (debugLog)
                Debug.Log($"[InteractionTrigger] CancelSticky ({name})");

            _isInteracting = false;

            if (_triggerCol != null)
                _triggerCol.enabled = true;

            if (animalToLock != null)
            {
                animalToLock.SetInteractionLocked(false);
                animalToLock.CancelLookAtPlayerNow();
            }

            RecheckPlayerOverlap();
        }

        // ===== Gia cố quan trọng =====
        // Khi collider bị disable, Unity không gửi Exit.
        // Hàm này check lại overlap để tránh kẹt prompt/highlight.
        private void RecheckPlayerOverlap()
        {
            if (_triggerCol == null)
                return;

            bool stillInside = false;

            if (_playerCol != null)
            {
                // bounds intersects ok cho case cơ bản
                stillInside = _triggerCol.bounds.Intersects(_playerCol.bounds);
            }

            _isPlayerInZone = stillInside;

            if (!stillInside)
            {
                if (interactPrompt)
                    interactPrompt.SetActive(false);

                onToggleHighlight?.Invoke(false);
            }
            else
            {
                if (!_isInteracting && interactPrompt)
                    interactPrompt.SetActive(true);

                onToggleHighlight?.Invoke(true);
            }

            if (debugLog)
                Debug.Log($"[InteractionTrigger] RecheckOverlap -> {stillInside}");
        }

        // Backward compat
        public void ForceHidePrompt()
        {
            if (interactPrompt)
                interactPrompt.SetActive(false);
        }
    }
}
