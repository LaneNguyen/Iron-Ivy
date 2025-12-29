using UnityEngine;

namespace IronIvy.Core
{
    // Trigger guide ngay khi vào game (Start).
    // Nâng cấp:
    // - 4 icon hướng di chuyển: mỗi cái tắt riêng khi bấm đúng hướng
    // - icon RMB: tắt khi nhấn/giữ RMB (rotate camera)
    // - khi tất cả icon đã tắt -> auto CompleteAndClose()
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

        private GuidePanelView _activeView;

        // trạng thái đã làm xong từng input
        private bool _doneUp;
        private bool _doneLeft;
        private bool _doneDown;
        private bool _doneRight;
        private bool _doneRmb;

        private void Start()
        {
            TryShow();
        }

        private void Update()
        {
            if (_activeView == null) return;
            if (!_activeView.gameObject.activeSelf) return;

            // 1) Detect movement input (WASD + Arrow)
            if (!_doneUp && (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)))
            {
                _doneUp = true;
                HideIcon(iconUp);
            }

            if (!_doneLeft && (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)))
            {
                _doneLeft = true;
                HideIcon(iconLeft);
            }

            if (!_doneDown && (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)))
            {
                _doneDown = true;
                HideIcon(iconDown);
            }

            if (!_doneRight && (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)))
            {
                _doneRight = true;
                HideIcon(iconRight);
            }

            // 2) Detect RMB rotate camera
            // TPS rotate đang dùng GetMouseButton(1) trong PlayerThirdPersonController. :contentReference[oaicite:1]{index=1}
            // Tutorial chỉ cần biết user đã "dùng RMB".
            if (!_doneRmb && (Input.GetMouseButtonDown(1) || Input.GetMouseButton(1)))
            {
                _doneRmb = true;
                HideIcon(iconRightMouse);
            }

            // 3) Auto complete when all required actions done
            if (autoCompleteWhenAllDone && IsAllDone())
            {
                _activeView.CompleteAndClose();
                _activeView = null;
                gameObject.SetActive(false);
            }
        }

        public void TryShow()
        {
            if (guidePanel == null) return;
            if (!GuidePanelManager.HasInstance) return;

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
            // Nếu Lane muốn “không bắt buộc RMB” thì chỉ cần bỏ _doneRmb ra khỏi đây.
            return _doneUp && _doneLeft && _doneDown && _doneRight && _doneRmb;
        }
    }
}
