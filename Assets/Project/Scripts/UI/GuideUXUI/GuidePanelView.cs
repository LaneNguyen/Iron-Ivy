using UnityEngine;
using UnityEngine.Events;

namespace IronIvy.Core
{
    public class GuidePanelView : MonoBehaviour
    {
        [Header("Config (set by GuidePanelManager / Trigger)")]
        [SerializeField] private string stepId;
        [SerializeField] private bool pauseGameWhenShow;
        [SerializeField] private bool forceShowOnTop = true;
        [SerializeField] private int sortingOrderOverride = 5000;

        [Header("Marking")]
        [Tooltip("Nếu false: CompleteAndClose sẽ KHÔNG MarkShown (dùng cho guide repeatable).")]
        [SerializeField] private bool allowMarking = true;

        [Header("Testing")]
        [Tooltip("Trong UNITY_EDITOR: nếu true thì CompleteAndClose sẽ KHÔNG MarkShown.")]
        [SerializeField] private bool disableMarkInEditor = true;

        [Header("UI Behavior")]
        public bool bringToFrontOnEnable = true;
        public bool overrideCanvasSorting = true;
        public bool enforceCanvasGroupBlockRaycast = true;

        [Header("Hooks (optional)")]
        public UnityEvent onOpened;
        public UnityEvent onClosed;

        private bool _didPause = false;

        // Setup cũ (giữ tương thích)
        public void Setup(string id, bool pause, bool forceTop, int orderOverride, bool disableMarkInEditorFlag)
        {
            stepId = id;
            pauseGameWhenShow = pause;
            forceShowOnTop = forceTop;
            sortingOrderOverride = orderOverride;
            disableMarkInEditor = disableMarkInEditorFlag;

            allowMarking = true; // setup kiểu "once" mặc định cho phép mark
        }

        // NEW: dùng cho Repeatable trigger (không mark)
        public void SetupRepeatable(string id, bool pause, bool forceTop, int orderOverride)
        {
            stepId = id;
            pauseGameWhenShow = pause;
            forceShowOnTop = forceTop;
            sortingOrderOverride = orderOverride;

            allowMarking = false; // quan trọng
            // Editor flag không cần thiết nữa vì đã chặn bằng allowMarking
        }

        private void OnEnable()
        {
            if (forceShowOnTop && bringToFrontOnEnable)
                transform.SetAsLastSibling();

            if (forceShowOnTop && overrideCanvasSorting)
            {
                var canvas = GetComponent<Canvas>();
                if (canvas != null)
                {
                    canvas.overrideSorting = true;
                    canvas.sortingOrder = Mathf.Max(1, sortingOrderOverride);
                }
            }

            if (forceShowOnTop && enforceCanvasGroupBlockRaycast)
            {
                var cg = GetComponent<CanvasGroup>();
                if (cg != null)
                {
                    cg.interactable = true;
                    cg.blocksRaycasts = true;
                }
            }

            if (pauseGameWhenShow && GuidePanelManager.HasInstance)
            {
                GuidePanelManager.Instance.PauseGame();
                _didPause = true;
            }

            onOpened?.Invoke();
        }

        private void OnDisable()
        {
            if (_didPause && GuidePanelManager.HasInstance)
            {
                GuidePanelManager.Instance.ResumeGame();
                _didPause = false;
            }

            onClosed?.Invoke();
        }

        public void CloseOnly()
        {
            gameObject.SetActive(false);
        }

        public void CompleteAndClose()
        {
            bool allowMark = allowMarking;

#if UNITY_EDITOR
            if (disableMarkInEditor) allowMark = false;
#endif

            if (allowMark && GuidePanelManager.HasInstance && !string.IsNullOrEmpty(stepId))
            {
                GuidePanelManager.Instance.MarkShown(stepId);
            }

            gameObject.SetActive(false);
        }
    }
}
