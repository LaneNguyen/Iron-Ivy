using UnityEngine;
using UnityEngine.Events;

namespace IronIvy.Gameplay.Interaction
{
    [RequireComponent(typeof(Collider))] 
    public class InteractionTrigger : MonoBehaviour
    {
        [Header("Settings")]
        public KeyCode interactKey = KeyCode.F;
        public string playerTag = "Player";

        [Header("Visual Feedback")]
        public GameObject interactPrompt;

        [Tooltip("Kéo script AnimalHighlightController vào đây (nếu có)")]
        public UnityEvent<bool> onToggleHighlight;

        [Header("Logic")]
        public UnityEvent onInteract;


        

        private bool _isPlayerInZone;

        private void Start()
        {
            if (interactPrompt) interactPrompt.SetActive(false);
            
            // Auto check collider
            var col = GetComponent<Collider>();
            if (col != null && !col.isTrigger) col.isTrigger = true;
        }

        private void Update()
        {
            if (!_isPlayerInZone) return;

            if (Input.GetKeyDown(interactKey))
            {
                // 1. Tắt nút F ngay lập tức
                if (interactPrompt) interactPrompt.SetActive(false);
                
                // 2. Gọi logic mở Panel
                onInteract?.Invoke();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag(playerTag))
            {
                _isPlayerInZone = true;
                
                // [LOGIC CHỈN CHU] Luôn bật lại F khi bước vào
                // Dù trước đó có chơi xong hay cancel, bước ra vào lại là reset
                if (interactPrompt) interactPrompt.SetActive(true);
                
                onToggleHighlight?.Invoke(true); 
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag(playerTag))
            {
                _isPlayerInZone = false;
                
                // Dọn dẹp visual khi rời đi
                if (interactPrompt) interactPrompt.SetActive(false);
                
                onToggleHighlight?.Invoke(false); 
            }
        }

        // Hàm này gọi từ code ngoài nếu muốn cưỡng chế ẩn (ví dụ Cutscene)
        public void ForceHidePrompt()
        {
            if (interactPrompt) interactPrompt.SetActive(false);
        }
    }
}