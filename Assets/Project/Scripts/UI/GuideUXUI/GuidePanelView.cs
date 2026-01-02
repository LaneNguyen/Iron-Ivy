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
        [SerializeField] private bool allowMarking = true;

        [Header("Testing")]
        [SerializeField] private bool disableMarkInEditor = true;

        [Header("UI Behavior")]
        public bool bringToFrontOnEnable = true;
        public bool overrideCanvasSorting = true;
        public bool enforceCanvasGroupBlockRaycast = true;

        [Header("IMPORTANT: Guide animation should NOT freeze when game is paused")]
        [Tooltip("Nếu true: tự chuyển Animator/Particle trong guide sang UnscaledTime để vẫn chạy khi Time.timeScale = 0.")]
        public bool useUnscaledTimeForGuide = true;

        [Header("Hooks (optional)")]
        public UnityEvent onOpened;
        public UnityEvent onClosed;

        private bool _didPause = false;

        public void Setup(string id, bool pause, bool forceTop, int orderOverride, bool disableMarkInEditorFlag)
        {
            stepId = id;
            pauseGameWhenShow = pause;
            forceShowOnTop = forceTop;
            sortingOrderOverride = orderOverride;
            disableMarkInEditor = disableMarkInEditorFlag;

            allowMarking = true;
        }

        public void SetupRepeatable(string id, bool pause, bool forceTop, int orderOverride)
        {
            stepId = id;
            pauseGameWhenShow = pause;
            forceShowOnTop = forceTop;
            sortingOrderOverride = orderOverride;

            allowMarking = false;
        }

        private void OnEnable()
        {
            // 1) Đảm bảo guide luôn on top
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

            // 2) NEW: Guide UI vẫn chạy dù pause game
            if (useUnscaledTimeForGuide)
                ApplyUnscaledTimeToGuide();

            // 3) Pause world/gameplay (Time.timeScale = 0)
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

        private void ApplyUnscaledTimeToGuide()
        {
            // Animator: UnscaledTime để animation UI không bị đứng hình khi timeScale=0
            var animators = GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                // Chỉ đổi nếu animator đang bật
                animators[i].updateMode = AnimatorUpdateMode.UnscaledTime;
            }

            // ParticleSystem: dùng unscaled time nếu có VFX
            var particles = GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particles.Length; i++)
            {
                var main = particles[i].main;
                main.useUnscaledTime = true;
            }
        }
    }
}
