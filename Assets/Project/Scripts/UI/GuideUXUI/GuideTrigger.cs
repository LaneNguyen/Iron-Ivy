using UnityEngine;
using UnityEngine.Playables;

namespace IronIvy.Core
{
    // GuideTrigger (patched):
    // - Có thể trigger bằng Start (như cũ)
    // - Hoặc trigger bằng Collider (đến gần là bật)
    // - Hỗ trợ Repeatable (mỗi lần đến gần lại hiện) hoặc Once (1 lần cho player)
    public class GuideTrigger : MonoBehaviour
    {
        public enum TriggerMode
        {
            OncePerPlayerPrefs = 0, // giống hành vi cũ: dùng PlayerPrefs, show 1 lần
            Repeatable = 1          // đến gần collider là show lại (không mark)
        }

        [Header("Guide Step")]
        public string stepId = "tutorial.move";

        [Header("One of these (panel OR timeline)")]
        public GameObject panelToShow;
        public PlayableDirector timelineToPlay;

        [Header("Trigger Settings")]
        public bool triggerOnStart = false;

        [Tooltip("Bật để trigger khi player đến gần collider (IsTrigger).")]
        public bool triggerOnProximity = true;

        [Tooltip("Mode Once: dùng PlayerPrefs, chỉ show 1 lần. Mode Repeatable: đến gần là show lại.")]
        public TriggerMode triggerMode = TriggerMode.OncePerPlayerPrefs;

        [Tooltip("Chỉ nhận trigger nếu collider có tag này. Để trống là nhận tất cả.")]
        public string requiredTag = "Player";

        [Tooltip("Tránh spam trigger liên tục (giây).")]
        public float retriggerCooldown = 0.5f;

        [Tooltip("Nếu true: khi rời khỏi vùng trigger thì tắt panel (Repeatable hay dùng).")]
        public bool autoHideOnExit = true;

        [Tooltip("Chỉ dùng cho OncePerPlayerPrefs: sau khi trigger thành công thì disable object này.")]
        public bool disableAfterTriggered = true;

        [Header("Panel Options")]
        public bool pauseGameWhenShowPanel = false;

        [Tooltip("Bật để panel luôn nằm trên dù đang ở Plant/Animal Rhythm hoặc panel khác")]
        public bool forceShowOnTop = true;

        [Tooltip("Sorting order ép lên top. Hay dùng 5000.")]
        public int sortingOrderOverride = 5000;

        // runtime
        private float _nextAllowedTime = 0f;
        private GuidePanelView _activeView; // để auto hide on exit

        private void Start()
        {
            if (triggerOnStart)
                TryTrigger();
        }

        // 3D Trigger
        private void OnTriggerEnter(Collider other)
        {
            if (!triggerOnProximity) return;
            if (!IsValidOther(other ? other.gameObject : null)) return;

            TryTrigger();
        }

        private void OnTriggerExit(Collider other)
        {
            if (!triggerOnProximity) return;
            if (!autoHideOnExit) return;
            if (!IsValidOther(other ? other.gameObject : null)) return;

            HidePanelIfOpen();
        }

        // 2D Trigger (nếu scene có dùng BoxCollider2D)
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!triggerOnProximity) return;
            if (!IsValidOther(other ? other.gameObject : null)) return;

            TryTrigger();
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!triggerOnProximity) return;
            if (!autoHideOnExit) return;
            if (!IsValidOther(other ? other.gameObject : null)) return;

            HidePanelIfOpen();
        }

        private bool IsValidOther(GameObject go)
        {
            if (go == null) return false;

            if (!string.IsNullOrEmpty(requiredTag))
                return go.CompareTag(requiredTag);

            return true;
        }

        public void TryTrigger()
        {
            // Cooldown để khỏi spam
            if (Time.time < _nextAllowedTime) return;
            _nextAllowedTime = Time.time + Mathf.Max(0.05f, retriggerCooldown);

            if (!GuidePanelManager.HasInstance) return;

            bool didSomething = false;

            // ================
            // PANEL
            // ================
            if (panelToShow != null)
            {
                if (triggerMode == TriggerMode.OncePerPlayerPrefs)
                {
                    // Hành vi cũ: show 1 lần (MarkShown ngay)
                    didSomething = GuidePanelManager.Instance.ShowPanelOnce(
                        stepId,
                        panelToShow,
                        pauseGameWhenShowPanel,
                        forceShowOnTop,
                        sortingOrderOverride
                    );
                }
                else
                {
                    // Repeatable: không MarkShown, chỉ bật panel lại mỗi lần
                    didSomething = ShowPanelRepeatable();
                }
            }

            // ================
            // TIMELINE (optional)
            // ================
            if (!didSomething && timelineToPlay != null)
            {
                // Timeline kiểu Once vẫn hợp lý hơn (thường cinematic không muốn lặp).
                // Nếu Lane muốn timeline repeatable cũng được, mình sẽ thêm option sau.
                didSomething = GuidePanelManager.Instance.PlayTimelineOnce(stepId, timelineToPlay);
            }

            // Disable object chỉ cho mode Once
            if (didSomething && disableAfterTriggered && triggerMode == TriggerMode.OncePerPlayerPrefs)
            {
                gameObject.SetActive(false);
            }
        }

        private bool ShowPanelRepeatable()
        {
            if (panelToShow == null) return false;

            // bật panel
            panelToShow.SetActive(true);

            // nếu có GuidePanelView thì cấu hình để:
            // - lên top
            // - pause nếu cần
            // - quan trọng: allowMarking = false (để không bị "đã hướng dẫn")
            var view = panelToShow.GetComponent<GuidePanelView>();
            if (view != null)
            {
                view.SetupRepeatable(
                    stepId,
                    pauseGameWhenShowPanel,
                    forceShowOnTop,
                    sortingOrderOverride
                );

                _activeView = view;
            }
            else
            {
                // không có view thì vẫn cố gắng bring to top
                if (forceShowOnTop)
                {
                    panelToShow.transform.SetAsLastSibling();
                    var canvas = panelToShow.GetComponent<Canvas>();
                    if (canvas != null)
                    {
                        canvas.overrideSorting = true;
                        canvas.sortingOrder = Mathf.Max(1, sortingOrderOverride);
                    }
                }

                // Pause basic
                if (pauseGameWhenShowPanel && GuidePanelManager.HasInstance)
                    GuidePanelManager.Instance.PauseGame();
            }

            return true;
        }

        private void HidePanelIfOpen()
        {
            if (panelToShow == null) return;
            if (!panelToShow.activeSelf) return;

            // Repeatable: đóng thôi, không mark
            if (_activeView != null)
            {
                _activeView.CloseOnly();
                _activeView = null;
            }
            else
            {
                panelToShow.SetActive(false);

                // Nếu trước đó pause mà không có view để resume thì ta resume ở đây
                if (pauseGameWhenShowPanel && GuidePanelManager.HasInstance)
                    GuidePanelManager.Instance.ResumeGame();
            }
        }
    }
}
