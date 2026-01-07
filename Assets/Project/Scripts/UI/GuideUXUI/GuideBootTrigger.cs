using UnityEngine;

namespace IronIvy.Core
{
    // Trigger guide ngay khi vào game (Start).
    // Nâng cấp:
    // - 4 icon hướng di chuyển: mỗi cái tắt riêng khi bấm đúng hướng
    // - icon RMB: tắt khi nhấn/giữ RMB (rotate camera)
    // - khi tất cả icon đã tắt -> auto CompleteAndClose()
    // - NEW: có âm thanh khi hoàn thành từng bước / hoàn thành tất cả
    // - NEW: hỗ trợ UI Button (OnClick) gọi trực tiếp các hàm OnPress...
    public class GuideBootTrigger : MonoBehaviour
    {
        [Header("Guide Step")]
        public string stepId = "guide.boot.move";

        [Header("Target Panel")]
        public GameObject guidePanel;

        [Header("Behavior")]
        public bool pauseGameWhenShow = false;
        public bool forceShowOnTop = true;
        public int sortingOrderOverride = 5000;

        [Header("Testing (Unity Editor)")]
        [Tooltip("Trong Unity Editor: nếu true thì bỏ qua PlayerPrefs (guide luôn hiện để test).")]
        public bool ignorePrefsInEditor = true;

        [Tooltip("Trong Unity Editor: nếu true thì CompleteAndClose sẽ KHÔNG MarkShown (để test thoải mái).")]
        public bool disableMarkInEditor = true;

        [Header("UI Icons (set these GameObjects in the guide panel)")]
        [Tooltip("Icon nút W hoặc Up")]
        public GameObject iconUp;

        [Tooltip("Icon nút A hoặc Left")]
        public GameObject iconLeft;

        [Tooltip("Icon nút S hoặc Down")]
        public GameObject iconDown;

        [Tooltip("Icon nút D hoặc Right")]
        public GameObject iconRight;

        [Tooltip("Icon chuột phải (RMB) - rotate camera")]
        public GameObject iconRightMouse;

        [Header("Auto Complete Rules")]
        [Tooltip("Nếu true: đủ 4 hướng + RMB thì tự complete & close.")]
        public bool autoCompleteWhenAllDone = true;

        [Header("Audio SE (Resources/Audio/SE)")]
        [Tooltip("Tên SE phát khi hoàn thành 1 bước (ẩn 1 icon).")]
        public string seOnStepDone = "ui_tick";

        [Tooltip("Tên SE phát khi hoàn thành toàn bộ tutorial (auto close).")]
        public string seOnAllDone = "ui_complete";

        [Tooltip("Chặn spam SE nếu user click liên tục (giây).")]
        public float seCooldown = 0.08f;

        private GuidePanelView _activeView;

        // trạng thái đã làm xong từng input
        private bool _doneUp;
        private bool _doneLeft;
        private bool _doneDown;
        private bool _doneRight;
        private bool _doneRmb;

        private float _nextAllowedSETime = 0f;

        private void OnEnable()
        {
            if (!ListenManager.HasInstance) return;

            // IMPORTANT: Cinematic flow gate
            // Chỉ được show guide khi intro kết thúc và gameplay unlock input.
            // IntroFlow sẽ RaiseInputLockRequested(false) ở bước Enter Gameplay.
            ListenManager.Instance.OnInputLockRequested += HandleInputLockRequested;
        }

        private void OnDisable()
        {
            if (!ListenManager.HasInstance) return;

            // FIX BUG: OnDisable phải unsubscribe, không được +=
            ListenManager.Instance.OnInputLockRequested -= HandleInputLockRequested;
        }

        private void HandleInputLockRequested(bool locked)
        {
            // locked == true: đang intro / đang khoá input => KHÔNG show
            if (locked) return;

            // locked == false: Enter Gameplay => giờ mới được show guide
            TryShow();
        }

        private void Update()
        {
            if (_activeView == null) return;
            if (!_activeView.gameObject.activeSelf) return;

            // 1) Detect movement input (WASD + Arrow)
            if (!_doneUp && (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)))
            {
                AudioManager.Instance?.PlayInterfaceSE();
                MarkDoneUp();
            }

            if (!_doneLeft && (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)))
            {
                AudioManager.Instance?.PlayInterfaceSE();
                MarkDoneLeft();
            }

            if (!_doneDown && (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)))
            {
                AudioManager.Instance?.PlayInterfaceSE();
                MarkDoneDown();
            }

            if (!_doneRight && (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)))
            {
                AudioManager.Instance?.PlayInterfaceSE();
                MarkDoneRight();
            }

            // 2) Detect RMB rotate camera
            if (!_doneRmb && (Input.GetMouseButtonDown(1) || Input.GetMouseButton(1)))
            {
                AudioManager.Instance?.PlayInterfaceSE();
                MarkDoneRmb();
            }

            // 3) Auto complete when all required actions done
            TryAutoComplete();
        }

        public void TryShow()
        {
            if (guidePanel == null) return;
            if (!GuidePanelManager.HasInstance) return;

            // Nếu panel đang mở rồi thì thôi (tránh event gọi lại 2 lần)
            if (_activeView != null && _activeView.gameObject.activeSelf) return;

            ResetRuntimeState();
            ShowAllIcons();

            // show nhưng CHƯA mark; mark khi CompleteAndClose()
            _activeView = GuidePanelManager.Instance.ShowPanelIfNotComplete(
                stepId,
                guidePanel,
                pauseGameWhenShow,
                forceShowOnTop,
                sortingOrderOverride,
                ignorePrefsInEditor,
                disableMarkInEditor
            );

            // Nếu player thật đã xem rồi -> manager trả null -> tắt trigger khỏi tốn công
            if (_activeView == null)
                gameObject.SetActive(false);
        }

        // =========================
        // UI BUTTON HOOKS
        // =========================
        // Gán các hàm này vào OnClick của các nút/ icon trong Guide Panel.
        public void OnPressUp() { if (_activeView != null && _activeView.gameObject.activeSelf) { MarkDoneUp(); TryAutoComplete(); } }
        public void OnPressLeft() { if (_activeView != null && _activeView.gameObject.activeSelf) { MarkDoneLeft(); TryAutoComplete(); } }
        public void OnPressDown() { if (_activeView != null && _activeView.gameObject.activeSelf) { MarkDoneDown(); TryAutoComplete(); } }
        public void OnPressRight() { if (_activeView != null && _activeView.gameObject.activeSelf) { MarkDoneRight(); TryAutoComplete(); } }
        public void OnPressRmb() { if (_activeView != null && _activeView.gameObject.activeSelf) { MarkDoneRmb(); TryAutoComplete(); } }

        // =========================
        // MARK DONE (shared by keyboard + UI)
        // =========================
        private void MarkDoneUp()
        {
            if (_doneUp) return;
            _doneUp = true;
            HideIcon(iconUp);
            PlayStepSE();
        }

        private void MarkDoneLeft()
        {
            if (_doneLeft) return;
            _doneLeft = true;
            HideIcon(iconLeft);
            PlayStepSE();
        }

        private void MarkDoneDown()
        {
            if (_doneDown) return;
            _doneDown = true;
            HideIcon(iconDown);
            PlayStepSE();
        }

        private void MarkDoneRight()
        {
            if (_doneRight) return;
            _doneRight = true;
            HideIcon(iconRight);
            PlayStepSE();
        }

        private void MarkDoneRmb()
        {
            if (_doneRmb) return;
            _doneRmb = true;
            HideIcon(iconRightMouse);
            PlayStepSE();
        }

        private void TryAutoComplete()
        {
            if (!autoCompleteWhenAllDone) return;
            if (!IsAllDone()) return;

            PlayCompleteSE();

            _activeView.CompleteAndClose();
            _activeView = null;
            gameObject.SetActive(false);
        }

        private void ResetRuntimeState()
        {
            _doneUp = false;
            _doneLeft = false;
            _doneDown = false;
            _doneRight = false;
            _doneRmb = false;
        }

        private void ShowAllIcons()
        {
            ShowIcon(iconUp);
            ShowIcon(iconLeft);
            ShowIcon(iconDown);
            ShowIcon(iconRight);
            ShowIcon(iconRightMouse);
        }

        private void HideIcon(GameObject icon)
        {
            if (icon == null) return;
            icon.SetActive(false);
        }

        private void ShowIcon(GameObject icon)
        {
            if (icon == null) return;
            icon.SetActive(true);
        }

        private bool IsAllDone()
        {
            return _doneUp && _doneLeft && _doneDown && _doneRight && _doneRmb;
        }

        private void PlayStepSE()
        {
            if (Time.unscaledTime < _nextAllowedSETime) return;
            _nextAllowedSETime = Time.unscaledTime + Mathf.Max(0.02f, seCooldown);

            if (string.IsNullOrEmpty(seOnStepDone)) return;
            if (AudioManager.Instance == null) return;

            AudioManager.Instance.PlaySE(seOnStepDone);
        }

        private void PlayCompleteSE()
        {
            if (string.IsNullOrEmpty(seOnAllDone)) return;
            if (AudioManager.Instance == null) return;

            AudioManager.Instance.PlaySE(seOnAllDone);
        }
    }
}
