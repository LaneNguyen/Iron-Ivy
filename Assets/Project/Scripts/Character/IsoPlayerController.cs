using UnityEngine;
using UnityEngine.InputSystem;

namespace IronIvy.Gameplay
{
    [RequireComponent(typeof(CharacterController))]
    public class IsoPlayerController : MonoBehaviour
    {
        [Header("Input System mới")]
        [Tooltip("Action Move (Vector2). Kéo PlayerControls (Player/Move) vào đây")]
        public InputActionReference moveAction;

        [Tooltip("Action Interact (Button). Kéo PlayerControls (Player/Interact) vào đây")]
        public InputActionReference interactAction;

        [Header("Legacy Input Manager")]
        [Tooltip("Tên axis ngang trong Input Manager")]
        public string legacyHorizontal = "Horizontal";

        [Tooltip("Tên axis dọc trong Input Manager")]
        public string legacyVertical = "Vertical";

        [Tooltip("Phím tương tác khi dùng Legacy Input")]
        public KeyCode legacyInteractKey = KeyCode.E;

        [Header("Movement")]
        [Tooltip("Tốc độ di chuyển m/s")]
        public float moveSpeed = 4f;

        [Tooltip("Tốc độ quay tối đa độ mỗi giây")]
        public float rotationMaxDegree = 720f;

        [Header("Tham chiếu")]
        [Tooltip("Camera chính. Để trống thì tự lấy Camera.main")]
        public Camera mainCamera;

        [Tooltip("Animator cần tham số Speed (float). Nếu để trống sẽ tự tìm")]
        public Animator animator;

        // cache
        private CharacterController _cc;
        private InteractionSystem _interaction;

        // input hiện tại
        private Vector2 _moveInput;

        // cờ dùng Input System mới
        private bool _useNewInput;

        private void Awake()
        {
            // lấy component cần
            _cc = GetComponent<CharacterController>();
            _interaction = GetComponent<InteractionSystem>();

            // auto camera
            if (!mainCamera) mainCamera = Camera.main;

            // auto tìm animator nếu chưa gán
            if (!animator)
            {
                animator = GetComponent<Animator>();
                if (!animator) animator = GetComponentInChildren<Animator>();
            }

#if ENABLE_INPUT_SYSTEM
            _useNewInput = (moveAction != null && interactAction != null);
#else
            _useNewInput = false;
#endif
        }

        private void OnEnable()
        {
#if ENABLE_INPUT_SYSTEM
            if (_useNewInput)
            {
                moveAction.action.Enable();
                interactAction.action.Enable();
            }
#endif
        }

        private void OnDisable()
        {
#if ENABLE_INPUT_SYSTEM
            if (_useNewInput)
            {
                moveAction.action.Disable();
                interactAction.action.Disable();
            }
#endif
        }

        private void Update()
        {
            Vector2 move = ReadMove();
            bool interactPressed = ReadInteractPressed();

            if (interactPressed)
                _interaction?.TryInteract();

            // tính hướng di chuyển theo camera
            Vector3 moveDir = Vector3.zero;
            if (mainCamera)
            {
                Vector3 camF = mainCamera.transform.forward; camF.y = 0; camF.Normalize();
                Vector3 camR = mainCamera.transform.right; camR.y = 0; camR.Normalize();
                moveDir = camR * move.x + camF * move.y;
            }

            // di chuyển
            _cc.SimpleMove(moveDir * moveSpeed);

            // quay hướng di chuyển
            if (moveDir.sqrMagnitude > 0.0001f)
            {
                Quaternion toRot = Quaternion.LookRotation(moveDir, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, toRot, rotationMaxDegree * Time.deltaTime);
            }

            // đẩy tốc độ sang animator
            if (animator)
            {
                float current = animator.GetFloat("Speed");
                float target = _cc.velocity.magnitude;
                float smoothed = Mathf.Lerp(current, target, Time.deltaTime * 10f); // note: mượt
                animator.SetFloat("Speed", smoothed);
            }
            else
            {
                // note: nếu vẫn null báo ra console
                Debug.LogWarning("IsoPlayerController: chưa tìm thấy Animator, hãy gán trong Inspector hoặc đặt Animator ở child.");
            }
        }

        // đọc move cho Both
        private Vector2 ReadMove()
        {
#if ENABLE_INPUT_SYSTEM
            if (_useNewInput)
                return moveAction.action.ReadValue<Vector2>();
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return new Vector2(Input.GetAxisRaw(legacyHorizontal), Input.GetAxisRaw(legacyVertical));
#else
            return Vector2.zero;
#endif
        }

        private bool ReadInteractPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (_useNewInput)
                return interactAction.action.WasPerformedThisFrame();
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(legacyInteractKey);
#else
            return false;
#endif
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!mainCamera) mainCamera = Camera.main;
        }
#endif
    }
}
