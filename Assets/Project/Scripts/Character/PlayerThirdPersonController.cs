using IronIvy.Core;
using IronIvy.Systems.Camera;
using Unity.Cinemachine;
using UnityEngine;

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

        [Header("Animator Anti Jitter")]
        [Tooltip("Ngưỡng speed (m/s) coi như Idle để khóa Side về 0.")]
        public float idleVelocityThreshold = 0.05f;

        [Tooltip("Deadzone riêng cho Side để tránh axis noise.")]
        public float sideDeadZone = 0.08f;

        [Tooltip("Ngưỡng input nhỏ coi như đứng yên (giống Iso).")]
        public float inputDeadZone = 0.06f;

        [Header("Mode Switch Stabilizer")]
        [Tooltip("Khi vừa bật TPS, ép Idle vài frame để tránh giật/lắc khi camera/controller chuyển mode.")]
        public float activationGraceTime = 0.12f;

        // --- Runtime State ---
        private float yaw;
        private float pitch;
        private CharacterController _cc;
        private Animator _anim;

        // Physics State
        private float _verticalVelocity;
        private Vector3 _currentVelocity;       // velocity phẳng do mình điều khiển (X/Z)
        private Vector3 _smoothDampVelocity;    // ref cho SmoothDamp

        [SerializeField, Tooltip("Debug: Check xem controller có đang active không")]
        private bool isTPSActive = false; // IMPORTANT: default false để không “đè” intro establish

        private IsoPlayerController _isoController;

        private float _activationTimer = 0f;

        // Opening intro lock (event-driven)
        private bool _inputLocked;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _anim = GetComponent<Animator>();
            _isoController = GetComponent<IsoPlayerController>();
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

            if (ListenManager.HasInstance)
            {
                ListenManager.Instance.OnInputLockRequested += HandleInputLockRequested;
                ListenManager.Instance.OnGameplayBegin += HandleGameplayBegin;
            }

            if (_isoController != null)
            {
                _isoController.enabled = !isTPSActive;
            }

            if (isTPSActive)
            {
                _activationTimer = activationGraceTime;
                ForceIdleAnimatorState();
            }

            // Sync ngay lần đầu (nếu gameplay start thẳng không qua intro)
            SyncTPSFromCurrentCamera(allowWhileLocked: false);
        }

        private void OnDestroy()
        {
            if (CameraManager.HasInstance)
            {
                CameraManager.Instance.OnCameraChanged -= OnCameraChanged;
            }

            if (ListenManager.HasInstance)
            {
                ListenManager.Instance.OnInputLockRequested -= HandleInputLockRequested;
                ListenManager.Instance.OnGameplayBegin -= HandleGameplayBegin;
            }
        }

        private void Update()
        {
            // Nếu đang bị lock input (opening timeline), đứng yên + vẫn rơi do gravity.
            if (_inputLocked)
            {
                ForceIdleAnimatorState();
                ApplyGravityOnly();
                return;
            }

            if (isTPSActive)
            {
                if (_activationTimer > 0f)
                {
                    _activationTimer -= Time.deltaTime;
                    ForceIdleAnimatorState();
                    ApplyGravityOnly();
                    return;
                }

                HandleCameraInput();
                HandleMovement();
            }
            else
            {
                if (_isoController == null || !_isoController.enabled)
                {
                    ApplyGravityOnly();
                }
            }
        }

        private void HandleInputLockRequested(bool locked)
        {
            _inputLocked = locked;

            if (locked)
            {
                _currentVelocity = Vector3.zero;
                _smoothDampVelocity = Vector3.zero;
                ForceIdleAnimatorState();
                return;
            }

            // IMPORTANT: vừa unlock xong, sync TPS theo camera hiện tại
            // Vì camera có thể đã switch sang GameCam trong lúc còn lock.
            SyncTPSFromCurrentCamera(allowWhileLocked: false);
        }

        private void HandleGameplayBegin()
        {
            // Gameplay bắt đầu: sync lần nữa cho chắc.
            SyncTPSFromCurrentCamera(allowWhileLocked: false);
        }

        private void SyncTPSFromCurrentCamera(bool allowWhileLocked)
        {
            if (!autoEnableByCamera) return;
            if (thirdPersonCamRef == null) return;

            if (!allowWhileLocked && _inputLocked) return;

            if (!CameraManager.HasInstance) return;

            var cur = CameraManager.Instance.CurrentCamera;
            bool shouldBeTPS = (cur == thirdPersonCamRef);
            SetTPSActive(shouldBeTPS);
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

        private void HandleMovement()
        {
            // 1) Read input + deadzone (giống Iso)
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");

            Vector2 raw = new Vector2(h, v);
            if (raw.sqrMagnitude < inputDeadZone * inputDeadZone) raw = Vector2.zero;

            bool hasMoveInput = raw.sqrMagnitude > 0.0001f;
            bool isRun = Input.GetKey(KeyCode.LeftShift);

            // 2) World dir by camera
            Vector3 targetDir = Vector3.zero;
            if (hasMoveInput)
            {
                if (Camera.main != null)
                {
                    Vector3 camForward = Camera.main.transform.forward;
                    Vector3 camRight = Camera.main.transform.right;
                    camForward.y = 0f;
                    camRight.y = 0f;
                    camForward.Normalize();
                    camRight.Normalize();
                    targetDir = (camForward * raw.y + camRight * raw.x).normalized;
                }
                else
                {
                    targetDir = new Vector3(raw.x, 0f, raw.y).normalized;
                }
            }

            // 3) Target speed theo input
            float targetSpeed = (hasMoveInput ? (isRun ? runSpeed : walkSpeed) : 0f);
            Vector3 targetVelocity = targetDir * targetSpeed;

            float smoothTime = (targetSpeed > 0.1f) ? acceleration : deceleration;

            _currentVelocity = Vector3.SmoothDamp(
                _currentVelocity,
                targetVelocity,
                ref _smoothDampVelocity,
                Mathf.Max(0.0001f, smoothTime)
            );

            // 4) HARD STOP giống Iso: không input -> ép velocity phẳng về 0 khi đã nhỏ
            if (!hasMoveInput && _currentVelocity.magnitude < idleVelocityThreshold)
            {
                _currentVelocity = Vector3.zero;
                _smoothDampVelocity = Vector3.zero;
            }

            // 5) Rotate theo hướng move (chỉ khi thật sự đang move)
            if (_currentVelocity.magnitude > 0.1f)
            {
                Quaternion targetRot = Quaternion.LookRotation(_currentVelocity.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
            }

            // 6) Gravity + jump
            if (_cc.isGrounded)
            {
                if (_verticalVelocity < 0.0f) _verticalVelocity = -2f;

                if (Input.GetButtonDown("Jump"))
                {
                    _verticalVelocity = Mathf.Sqrt(jumpHeight * 2f * gravity);
                }
            }
            else
            {
                _verticalVelocity -= gravity * Time.deltaTime;
            }

            Vector3 finalMove = _currentVelocity;
            finalMove.y = _verticalVelocity;
            _cc.Move(finalMove * Time.deltaTime);

            // 7) Animator: dùng logic "tắt cứng" giống Iso
            UpdateAnimatorTPS(hasMoveInput, raw, targetSpeed, isRun);
        }

        private void UpdateAnimatorTPS(bool hasMoveInput, Vector2 rawInput, float targetSpeed, bool isRun)
        {
            if (!_anim) return;

            float speedFlat = new Vector3(_currentVelocity.x, 0f, _currentVelocity.z).magnitude;

            bool isIdleByTarget = targetSpeed <= 0.01f || !hasMoveInput;
            bool isIdleByVelocity = speedFlat < idleVelocityThreshold;

            if (isIdleByTarget || isIdleByVelocity)
            {
                ForceIdleAnimatorState();
                return;
            }

            _anim.SetFloat("Speed", speedFlat);

            float side = rawInput.x;
            if (Mathf.Abs(side) < sideDeadZone) side = 0f;
            _anim.SetFloat("Side", side);

            _anim.SetFloat("run", isRun ? 1f : 0f);
        }

        private void ForceIdleAnimatorState()
        {
            if (!_anim) return;
            _anim.SetFloat("Speed", 0f);
            _anim.SetFloat("Side", 0f);
            _anim.SetFloat("run", 0f);
        }

        private void ApplyGravityOnly()
        {
            if (_cc.isGrounded && _verticalVelocity < 0)
                _verticalVelocity = -2f;
            else
                _verticalVelocity -= gravity * Time.deltaTime;

            _cc.Move(new Vector3(0f, _verticalVelocity, 0f) * Time.deltaTime);
        }

        private void OnCameraChanged(CinemachineCamera oldCam, CinemachineCamera newCam)
        {
            if (!autoEnableByCamera) return;
            if (thirdPersonCamRef == null) return;

            // Trong intro lock, camera có thể switch nhưng player không được phép bật TPS.
            // Việc sync sẽ xảy ra khi unlock / GameplayBegin.
            if (_inputLocked) return;

            bool isMyCamera = (newCam == thirdPersonCamRef);
            SetTPSActive(isMyCamera);
        }

        public void SetTPSActive(bool active)
        {
            isTPSActive = active;

            if (_isoController != null)
            {
                _isoController.enabled = !active;
            }

            if (active)
            {
                _currentVelocity = Vector3.zero;
                _smoothDampVelocity = Vector3.zero;
                _verticalVelocity = 0f;

                ResyncCameraAnglesFromPivot();

                ForceIdleAnimatorState();
                _activationTimer = activationGraceTime;
            }
            else
            {
                ForceIdleAnimatorState();
                _activationTimer = 0f;
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
