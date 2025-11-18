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

        [Tooltip("Action Interact (Button). Kéo PlayerControls (Player/Interact) vào đây (optional)")]
        public InputActionReference interactAction;

        [Header("Legacy Input Manager")]
        [Tooltip("Axis Horizontal (legacy input)")]
        public string legacyHorizontal = "Horizontal";

        [Tooltip("Axis Vertical (legacy input)")]
        public string legacyVertical = "Vertical";

        [Tooltip("Phím tương tác (legacy input)")]
        public KeyCode legacyInteractKey = KeyCode.E;

        [Header("Movement")]
        [Tooltip("Tốc độ di chuyển chính (m/s)")]
        public float moveSpeed = 5f;

        [Tooltip("Thời gian vọt lên tốc độ mục tiêu (giây). Nhỏ = bốc hơn")]
        public float accelerationTime = 0.08f;

        [Tooltip("Thời gian hãm về 0 (giây). Lớn hơn để dừng mượt")]
        public float decelerationTime = 0.12f;

        [Tooltip("Tốc độ quay tối đa (độ/giây)")]
        public float rotationMaxDegree = 360f;

        [Tooltip("Bỏ rung input nhỏ (0–0.2). 0.1 là hợp lý cho stick/phím/analog")]
        public float inputDeadZone = 0.08f;

        [Header("Tham chiếu")]
        [Tooltip("Camera dùng để định hướng move. Để trống sẽ lấy Camera.main")]
        public Camera mainCamera;

        [Tooltip("Animator cần tham số Speed (float). Nếu để trống sẽ tự tìm")]
        public Animator animator;

        private CharacterController _cc;
        private InteractionSystem _interaction;

        private Vector2 _moveInput;
        private Vector3 _smoothedVelocity = Vector3.zero;
        private Vector3 _velRef = Vector3.zero;

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
            // chỉ cần có MoveAction là đủ để dùng New Input
            _useNewInput = (moveAction != null);
            if (!_useNewInput)
                Debug.LogWarning("[IsoPlayerController] No MoveAction assigned, falling back to legacy axes or zero.");
#else
            _useNewInput = false;
#endif
        }

        private void OnEnable()
        {
#if ENABLE_INPUT_SYSTEM
            if (_useNewInput && moveAction != null)
                moveAction.action.Enable();
            if (_useNewInput && interactAction != null)
                interactAction.action.Enable();
#endif
        }

        private void OnDisable()
        {
#if ENABLE_INPUT_SYSTEM
            if (_useNewInput && moveAction != null)
                moveAction.action.Disable();
            if (_useNewInput && interactAction != null)
                interactAction.action.Disable();
#endif
        }

        private void Update()
        {
            // 1) Đọc input
            Vector2 rawMove = ReadMove();
            if (rawMove.sqrMagnitude < inputDeadZone * inputDeadZone)
                rawMove = Vector2.zero;

            _moveInput = rawMove;

            // 2) Chuyển sang hướng world theo camera
            Vector3 moveDir = Vector3.zero;
            if (_moveInput.sqrMagnitude > 0.0001f)
            {
                if (!mainCamera)
                {
                    moveDir = new Vector3(_moveInput.x, 0f, _moveInput.y);
                }
                else
                {
                    Vector3 camF = mainCamera.transform.forward; camF.y = 0f; camF.Normalize();
                    Vector3 camR = mainCamera.transform.right; camR.y = 0f; camR.Normalize();
                    moveDir = camR * _moveInput.x + camF * _moveInput.y;
                    if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();
                }
            }

            // 3) Smooth tốc độ
            Vector3 targetVelocity = moveDir * moveSpeed;
            float smoothTime = (targetVelocity.sqrMagnitude > 0.0001f) ? accelerationTime : decelerationTime;
            _smoothedVelocity = Vector3.SmoothDamp(
                _smoothedVelocity, targetVelocity, ref _velRef, Mathf.Max(0.0001f, smoothTime));

            // 4) Di chuyển (SimpleMove có apply gravity)
            _cc.SimpleMove(_smoothedVelocity);

            // 5) Xoay theo hướng di chuyển
            Vector3 facing = _smoothedVelocity;
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.0001f && rotationMaxDegree > 0f)
            {
                Quaternion toRot = Quaternion.LookRotation(facing, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, toRot, rotationMaxDegree * Time.deltaTime);
            }

            // 6) Animator speed
            if (animator)
            {
                float targetSpeed = _smoothedVelocity.magnitude;
                animator.SetFloat("Speed", targetSpeed, 0.1f, Time.deltaTime);
            }

            // 7) Interact
            if (_interaction && ReadInteractPressed())
            {
                _interaction.TryInteract();
            }
        }

        private Vector2 ReadMove()
        {
#if ENABLE_INPUT_SYSTEM
            if (_useNewInput && moveAction != null)
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
            if (_useNewInput && interactAction != null)
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
