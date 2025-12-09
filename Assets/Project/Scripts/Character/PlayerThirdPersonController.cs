using UnityEngine;
using Unity.Cinemachine;
using IronIvy.Systems.Camera;

namespace IronIvy.Gameplay
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerThirdPersonController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [Tooltip("Tốc độ đi bộ (m/s)")]
        public float walkSpeed = 3f;

        [Tooltip("Tốc độ chạy (m/s) - Giữ Shift")]
        public float runSpeed = 6f;

        [Tooltip("Tốc độ xoay người theo hướng di chuyển")]
        public float rotationSpeed = 12f;

        [Header("Physics Settings (Restored)")]
        [Tooltip("Thời gian để đạt tốc độ tối đa (Càng nhỏ càng nhanh)")]
        public float acceleration = 0.1f; 

        [Tooltip("Thời gian để dừng lại hẳn (Càng nhỏ dừng càng nhanh)")]
        public float deceleration = 0.15f; 

        [Tooltip("Trọng lực")]
        public float gravity = 15.0f;

        [Tooltip("Độ cao nhảy (nếu có)")]
        public float jumpHeight = 1.0f;

        [Header("Camera Settings")]
        public Transform cameraPivot;
        public CinemachineCamera thirdPersonCamRef;
        public bool autoEnableByCamera = true;

        [Header("Input Sensitivity")]
        public float camSensitivityX = 2f;
        public float camSensitivityY = 1.5f;
        public float minPitch = -30f;
        public float maxPitch = 60f;

        // --- Runtime State ---
        private float yaw;
        private float pitch;
        private CharacterController _cc;
        private Animator _anim;

        // Physics State
        private float _verticalVelocity;       // Y velocity
        private Vector3 _currentVelocity;      // XZ velocity
        private Vector3 _smoothDampVelocity;   // helper cho SmoothDamp

        [SerializeField, Tooltip("Debug: Check xem controller có đang active không")]
        private bool isTPSActive = true;

        // tham chiếu qua IsoPlayer để còn bật/tắt cho đỡ cộng dồn
        private IsoPlayerController _isoController;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _anim = GetComponent<Animator>();
            _isoController = GetComponent<IsoPlayerController>(); // có thì lấy, không có thì thôi
        }

        private void Start()
        {
            if (cameraPivot)
            {
                Vector3 e = cameraPivot.eulerAngles;
                yaw = e.y;
                pitch = e.x;
            }

            if (CameraManager.HasInstance)
            {
                CameraManager.Instance.OnCameraChanged += OnCameraChanged;
            }

            // sync trạng thái Iso theo TPS ban đầu
            if (_isoController != null)
            {
                // nếu TPS đang active => tắt iso, ngược lại bật iso
                _isoController.enabled = !isTPSActive;
            }
        }

        private void OnDestroy()
        {
            if (CameraManager.HasInstance)
            {
                CameraManager.Instance.OnCameraChanged -= OnCameraChanged;
            }
        }

        private void Update()
        {
            if (isTPSActive)
            {
                HandleCameraInput();
                HandleMovement();      // TPS tự move
            }
            else
            {
                // nếu TPS tắt mà Iso cũng tắt (trường hợp đặc biệt) thì vẫn phải có gravity
                // còn nếu Iso đang bật thì để Iso lo SimpleMove + gravity luôn, mình không đụng
                if (_isoController == null || !_isoController.enabled)
                {
                    ApplyGravityOnly();
                }
            }
        }

        private void HandleCameraInput()
        {
            if (!cameraPivot) return;

            if (Input.GetMouseButton(1))
            {
                float mx = Input.GetAxis("Mouse X") * camSensitivityX;
                float my = Input.GetAxis("Mouse Y") * camSensitivityY;

                yaw += mx;
                pitch -= my;
                pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            }

            cameraPivot.rotation = Quaternion.Euler(pitch, yaw, 0f);
            cameraPivot.position = transform.position + Vector3.up * 1.5f;
        }

        // Movement dùng SmoothDamp, giữ nguyên như bạn đang xài
        private void HandleMovement()
        {
            float h = Input.GetAxis("Horizontal");
            float v = Input.GetAxis("Vertical");
            bool isRun = Input.GetKey(KeyCode.LeftShift);

            // 1. Hướng move theo camera
            Vector3 targetDir = Vector3.zero;
            if (Camera.main != null)
            {
                Vector3 camForward = Camera.main.transform.forward;
                Vector3 camRight = Camera.main.transform.right;
                camForward.y = 0; 
                camRight.y = 0;
                camForward.Normalize(); 
                camRight.Normalize();
                targetDir = (camForward * v + camRight * h).normalized;
            }

            // 2. Tốc độ mong muốn
            float targetSpeed = 0f;
            if (targetDir.magnitude > 0.1f)
            {
                targetSpeed = isRun ? runSpeed : walkSpeed;
            }

            // 3. Quán tính accel/decel
            float smoothTime = (targetSpeed > 0.1f) ? acceleration : deceleration;
            Vector3 targetVelocity = targetDir * targetSpeed;

            _currentVelocity = Vector3.SmoothDamp(
                _currentVelocity,
                targetVelocity,
                ref _smoothDampVelocity,
                smoothTime
            );

            // 4. Xoay theo hướng đang di chuyển
            if (_currentVelocity.magnitude > 0.1f)
            {
                Quaternion targetRot = Quaternion.LookRotation(_currentVelocity.normalized);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRot,
                    rotationSpeed * Time.deltaTime
                );
            }

            // 5. Gravity
            if (_cc.isGrounded)
            {
                if (_verticalVelocity < 0.0f)
                    _verticalVelocity = -2f;

                if (Input.GetButtonDown("Jump"))
                {
                    _verticalVelocity = Mathf.Sqrt(jumpHeight * 2f * gravity);
                }
            }
            else
            {
                _verticalVelocity -= gravity * Time.deltaTime;
            }

            // 6. Move final
            Vector3 finalMove = _currentVelocity;
            finalMove.y = _verticalVelocity;

            _cc.Move(finalMove * Time.deltaTime);

            // 7. Animation
            if (_anim)
            {
                float speedPercent = new Vector3(_cc.velocity.x, 0, _cc.velocity.z).magnitude;
                _anim.SetFloat("Speed", speedPercent);
            }
        }

        private void ApplyGravityOnly()
        {
            if (_cc.isGrounded && _verticalVelocity < 0)
                _verticalVelocity = -2f;
            else
                _verticalVelocity -= gravity * Time.deltaTime;

            _cc.Move(new Vector3(0, _verticalVelocity, 0) * Time.deltaTime);
        }

        // =========================================================
        // CAMERA EVENT LISTENER
        // =========================================================

        private void OnCameraChanged(CinemachineCamera oldCam, CinemachineCamera newCam)
        {
            if (!autoEnableByCamera) return;
            if (thirdPersonCamRef == null) return;

            bool isMyCamera = (newCam == thirdPersonCamRef);
            SetTPSActive(isMyCamera);
        }

        public void SetTPSActive(bool active)
        {
            isTPSActive = active;

            // bật TPS => tắt Iso; tắt TPS => bật Iso
            if (_isoController != null)
            {
                _isoController.enabled = !active;
            }

            if (active)
            {
                // reset quán tính một chút cho đỡ bị "kéo" từ iso
                _currentVelocity = Vector3.zero;
                _smoothDampVelocity = Vector3.zero;

                ResyncCameraAnglesFromPivot();
            }
        }

        public void ResyncCameraAnglesFromPivot()
        {
            if (!cameraPivot) return;

            Vector3 e = cameraPivot.eulerAngles;
            yaw = e.y;

            float rawPitch = e.x;
            if (rawPitch > 180f) rawPitch -= 360f;
            pitch = Mathf.Clamp(rawPitch, minPitch, maxPitch);
        }
    }
}
