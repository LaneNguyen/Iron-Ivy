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
        [Tooltip("Tốc độ tối đa (m/s)")]
        public float moveSpeed = 4f;

        [Tooltip("Thời gian vọt lên tốc độ mục tiêu (giây). Nhỏ = bốc hơn")]
        public float accelerationTime = 0.08f;

        [Tooltip("Thời gian hãm về 0 (giây). Lớn hơn để dừng mượt")]
        public float decelerationTime = 0.12f;

        [Tooltip("Tốc độ quay tối đa (độ/giây)")]
        public float rotationMaxDegree = 540f;

        [Tooltip("Bỏ rung input nhỏ (0–0.2). 0.1 là hợp lý cho stick phím / analog")]
        public float inputDeadZone = 0.08f;

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

        // smoothing state
        private Vector3 _smoothedVelocity = Vector3.zero;
        private Vector3 _velRef = Vector3.zero;

        // cờ dùng Input System mới
        private bool _useNewInput;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _interaction = GetComponent<InteractionSystem>();

            if (!mainCamera) mainCamera = Camera.main;

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
            // 1) Đọc input
            Vector2 rawMove = ReadMove();
            if (rawMove.sqrMagnitude < inputDeadZone * inputDeadZone)
                rawMove = Vector2.zero;

            // 2) Đổi sang hướng theo camera (isometric WASD theo camera)
            Vector3 moveDir = Vector3.zero;
            if (rawMove != Vector2.zero)
            {
                Vector3 camF = Vector3.forward;
                Vector3 camR = Vector3.right;

                if (mainCamera)
                {
                    camF = mainCamera.transform.forward; camF.y = 0f; camF.Normalize();
                    camR = mainCamera.transform.right; camR.y = 0f; camR.Normalize();
                }

                moveDir = (camR * rawMove.x + camF * rawMove.y);
                if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();
            }

            // 3) Tính vận tốc mục tiêu & SmoothDamp để tăng/giảm tốc mượt
            Vector3 targetVelocity = moveDir * moveSpeed;
            float smoothTime = (targetVelocity.sqrMagnitude > 0.0001f) ? accelerationTime : decelerationTime;
            _smoothedVelocity = Vector3.SmoothDamp(_smoothedVelocity, targetVelocity, ref _velRef, Mathf.Max(0.0001f, smoothTime));

            // 4) Di chuyển (SimpleMove nhận vận tốc theo m/s và tự áp gravity)
            _cc.SimpleMove(_smoothedVelocity);

            // 5) Xoay theo hướng di chuyển hiện tại (dùng vận tốc đã mượt)
            Vector3 facing = _smoothedVelocity;
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.0001f && rotationMaxDegree > 0f)
            {
                Quaternion toRot = Quaternion.LookRotation(facing, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, toRot, rotationMaxDegree * Time.deltaTime);
            }

            // 6) Đẩy tốc độ sang Animator (mượt)
            if (animator)
            {
                float targetSpeed = _smoothedVelocity.magnitude; // m/s
                // Dùng damping nội bộ Animator để mượt hơn
                animator.SetFloat("Speed", targetSpeed, 0.1f, Time.deltaTime);
            }

            // 7) Tương tác
            if (ReadInteractPressed())
                _interaction?.TryInteract();

#if UNITY_EDITOR
            // Debug hướng (bật khi cần)
            // Debug.DrawRay(transform.position, facing.normalized * 1.2f, Color.yellow);
#endif
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
            accelerationTime = Mathf.Max(0.0f, accelerationTime);
            decelerationTime = Mathf.Max(0.0f, decelerationTime);
            moveSpeed = Mathf.Max(0.0f, moveSpeed);
            rotationMaxDegree = Mathf.Max(0.0f, rotationMaxDegree);
        }
#endif
    }
}
