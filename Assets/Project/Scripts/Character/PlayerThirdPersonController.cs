using System;
using UnityEngine;
using IronIvy.Systems.Camera;
using Unity.Cinemachine;

namespace IronIvy.Gameplay
{
    // controller third person cho player
    // - dùng CharacterController
    // - pivot camera riêng, vcam follow pivot
    [RequireComponent(typeof(CharacterController))]
    public class PlayerThirdPersonController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [Tooltip("walk speed m/s")]
        public float walkSpeed = 3f;

        [Tooltip("run speed m/s (hold Left Shift)")]
        public float runSpeed = 6f;

        [Tooltip("how fast we turn toward move dir (bigger = snappier)")]
        public float rotationSpeed = 12f;

        [Tooltip("accel smooth time (smaller = snappier)")]
        public float acceleration = 0.08f;

        [Tooltip("decel smooth time (smaller = snappier)")]
        public float deceleration = 0.12f;

        [Tooltip("smooth rotate or instant")]
        public bool smoothRotate = true;

        [Header("Camera Settings")]
        [Tooltip("orbit pivot used by vcam; dont parent under player")]
        public Transform cameraPivot;

        [Tooltip("mouse X sens when RMB hold")]
        public float camSensitivityX = 2f;

        [Tooltip("mouse Y sens when RMB hold")]
        public float camSensitivityY = 1.5f;

        [Tooltip("min look angle (negative)")]
        public float minPitch = -40f;

        [Tooltip("max look angle")]
        public float maxPitch = 60f;

        [Header("Pivot Follow Settings")]
        [Tooltip("pivot height above player feet")]
        public float pivotHeight = 1.6f;

        [Tooltip("follow damping for pivot position")]
        public float pivotFollowDamping = 12f;

        [Header("Animation")]
        [Tooltip("Animator with params: bool IsMoving, float Speed")]
        public Animator animator;

        [Header("Gravity / Jump (optional)")]
        [Tooltip("gravity m/s^2")]
        public float gravity = 9.81f;

        [Tooltip("jump height meters")]
        public float jumpHeight = 1.2f;

        [Tooltip("press Space to jump")]
        public bool enableJump = false;

        [Header("Camera Integration (optional)")]
        [Tooltip("TPS vcam ref used to auto gate by CameraManager")]
        public CinemachineCamera thirdPersonCamRef;

        [Tooltip("auto gate by CameraManager (keep component enabled!)")]
        public bool autoEnableByCamera = true;

        [Header("Mode Gate")]
        [Tooltip("when false, TPS input/move disabled; only pivot follow keeps running")]
        public bool isTPSActive = false;

        // state
        private float yaw, pitch;
        private CharacterController controller;
        private Transform cam;             // fallback Camera.main nếu cần
        private Vector3 currentVelocity;   // XZ velocity
        private Vector3 velocityRef;
        private float verticalVel;
        private bool wasMovingLastFrame;

        // flag nhỏ để tránh subscribe event nhiều lần
        private bool hasCameraEventsHooked = false;

        // events cho chỗ khác hook vào
        public event Action OnPlayerMoveStart;
        public event Action OnPlayerMoveStop;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();

            // auto tìm animator quanh player
            if (!animator)
            {
                animator = GetComponent<Animator>();
                if (!animator) animator = GetComponentInChildren<Animator>();
            }

            if (!cam && Camera.main)
                cam = Camera.main.transform;

            // init yaw/pitch từ pivot để tránh bị giật frame đầu
            if (cameraPivot)
            {
                Vector3 e = cameraPivot.rotation.eulerAngles;
                yaw = e.y;

                float rawPitch = e.x;
                if (rawPitch > 180f) rawPitch -= 360f;

                pitch = Mathf.Clamp(rawPitch, minPitch, maxPitch);
                cameraPivot.rotation = Quaternion.Euler(pitch, yaw, 0f);
            }
        }

        private void OnEnable()
        {
            // reset state hook event
            hasCameraEventsHooked = false;

            // thử hook nếu CameraManager đã có sẵn
            EnsureCameraEventsHooked();

            // sync gate theo camera hiện tại lúc start (nếu có)
            GateByCurrentCamera();
        }

        private void OnDisable()
        {
            // đảm bảo gỡ event cho sạch
            if (autoEnableByCamera && hasCameraEventsHooked && CameraManager.HasInstance)
            {
                CameraManager.Instance.OnCameraChanged -= HandleCameraChanged;
            }

            hasCameraEventsHooked = false;
        }

        private void Update()
        {
            if (!isTPSActive)
            {
                // khi gate off thì iso controller xử lý move
                // ở đây chỉ lo phần pivot follow trong LateUpdate
                return;
            }

            // step 1: đọc input move
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            Vector3 inputDir = new Vector3(h, 0f, v);
            if (inputDir.sqrMagnitude > 1f) inputDir.Normalize();

            // chạy hay đi tùy theo shift
            float targetSpeed = (Input.GetKey(KeyCode.LeftShift) ? runSpeed : walkSpeed) * inputDir.magnitude;

            // step 2: convert move theo hướng camera
            Vector3 moveDir;
            if (cameraPivot)
            {
                Vector3 camForward = cameraPivot.forward;
                camForward.y = 0f;
                camForward.Normalize();

                Vector3 camRight = cameraPivot.right;
                camRight.y = 0f;
                camRight.Normalize();

                moveDir = camForward * v + camRight * h;
            }
            else
            {
                moveDir = new Vector3(h, 0f, v);
            }
            if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

            Vector3 targetVelocity = moveDir * targetSpeed;

            // step 3: smooth accel / decel
            float smoothTime = (targetSpeed > 0.01f)
                ? Mathf.Max(0.0001f, acceleration)
                : Mathf.Max(0.0001f, deceleration);

            currentVelocity = Vector3.SmoothDamp(
                currentVelocity,
                targetVelocity,
                ref velocityRef,
                smoothTime
            );

            // step 4: move controller + gravity
            if (controller.isGrounded)
            {
                // giữ 1 ít negative để CharacterController báo grounded
                verticalVel = -0.5f;

                if (enableJump && Input.GetKeyDown(KeyCode.Space))
                    verticalVel = Mathf.Sqrt(2f * gravity * Mathf.Max(0.01f, jumpHeight));
            }
            else
            {
                verticalVel -= gravity * Time.deltaTime;
            }

            Vector3 frameMotion = new Vector3(currentVelocity.x, verticalVel, currentVelocity.z) * Time.deltaTime;
            controller.Move(frameMotion);

            // step 5: rotate character theo hướng chạy
            Vector3 flatVel = currentVelocity;
            flatVel.y = 0f;

            bool isMovingNow = flatVel.sqrMagnitude > 0.0001f;

            if (isMovingNow)
            {
                Quaternion targetRot = Quaternion.LookRotation(flatVel, Vector3.up);
                if (smoothRotate)
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
                else
                    transform.rotation = targetRot;
            }

            // animator sync speed + flag
            if (animator)
            {
                float speedParam = new Vector3(controller.velocity.x, 0f, controller.velocity.z).magnitude;
                animator.SetFloat("Speed", speedParam, 0.1f, Time.deltaTime);
                animator.SetBool("IsMoving", isMovingNow);
            }

            // fire event move start/stop
            if (isMovingNow && !wasMovingLastFrame)
                OnPlayerMoveStart?.Invoke();
            else if (!isMovingNow && wasMovingLastFrame)
                OnPlayerMoveStop?.Invoke();

            wasMovingLastFrame = isMovingNow;
        }

        private void LateUpdate()
        {
            // đảm bảo mỗi frame đều thử hook nếu CameraManager spawn trễ
            EnsureCameraEventsHooked();

            // pivot follow player mọi lúc, dù tps active hay không
            if (cameraPivot)
            {
                Vector3 targetPos = new Vector3(
                    transform.position.x,
                    transform.position.y + pivotHeight,
                    transform.position.z
                );

                // dạng damping 1 - exp(-k * dt)
                float t = 1f - Mathf.Exp(-pivotFollowDamping * Time.deltaTime);
                cameraPivot.position = Vector3.Lerp(cameraPivot.position, targetPos, t);
            }

            // phần xoay camera chỉ chạy khi TPS đang active
            if (!isTPSActive) return;

            // xử lý lock/unlock cursor khi giữ chuột phải
            if (Input.GetMouseButtonDown(1))
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else if (Input.GetMouseButtonUp(1))
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            // step 6: orbit yaw/pitch khi giữ RMB
            if (cameraPivot && Input.GetMouseButton(1))
            {
                yaw += Input.GetAxis("Mouse X") * camSensitivityX;
                pitch -= Input.GetAxis("Mouse Y") * camSensitivityY;
                pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
                cameraPivot.rotation = Quaternion.Euler(pitch, yaw, 0f);
            }
        }

        // auto hook event đổi camera khi cần
        private void EnsureCameraEventsHooked()
        {
            if (!autoEnableByCamera) return;
            if (hasCameraEventsHooked) return;
            if (!CameraManager.HasInstance) return;
            if (thirdPersonCamRef == null) return;

            // đăng kí 1 lần thôi
            CameraManager.Instance.OnCameraChanged += HandleCameraChanged;
            hasCameraEventsHooked = true;

            // vừa hook xong thì sync luôn theo camera hiện tại
            GateByCurrentCamera();
        }

        // camera manager auto gate khu này
        // - khi camera đổi thì bật/tắt tps bằng flag isTPSActive
        private void HandleCameraChanged(CinemachineCamera oldCam, CinemachineCamera newCam)
        {
            if (!autoEnableByCamera || thirdPersonCamRef == null) return;

            bool active = (newCam != null && newCam == thirdPersonCamRef);
            SetTPSActive(active);

#if UNITY_EDITOR
            // log nhẹ cho dễ debug xem lúc nào tps được bật
            // Debug.Log($"[TPS] CameraChanged -> active = {active}");
#endif

            if (active) ResyncCameraAnglesFromPivot();
        }

        private void GateByCurrentCamera()
        {
            if (!autoEnableByCamera || thirdPersonCamRef == null || !CameraManager.HasInstance)
            {
                // nếu không quyết định được thì giữ lại giá trị inspector
                SetTPSActive(isTPSActive);
                return;
            }

            var current = CameraManager.Instance.CurrentCamera;
            bool active = (current != null && current == thirdPersonCamRef);
            SetTPSActive(active);
            if (active) ResyncCameraAnglesFromPivot();
        }

        // public để switcher bên ngoài gọi
        public void SetTPSActive(bool value)
        {
            isTPSActive = value;    // chỉ gate logic, không đụng this.enabled
            if (isTPSActive) ResyncCameraAnglesFromPivot();
        }

        public void ResyncCameraAnglesFromPivot()
        {
            if (!cameraPivot) return;

            Vector3 e = cameraPivot.rotation.eulerAngles;
            yaw = e.y;

            float rawPitch = e.x;
            if (rawPitch > 180f) rawPitch -= 360f;

            pitch = Mathf.Clamp(rawPitch, minPitch, maxPitch);
            cameraPivot.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // vài guard nhỏ cho giá trị
            minPitch = Mathf.Clamp(minPitch, -89f, 0f);
            maxPitch = Mathf.Clamp(maxPitch, 0f, 89f);
            walkSpeed = Mathf.Max(0f, walkSpeed);
            runSpeed = Mathf.Max(0f, runSpeed);
            rotationSpeed = Mathf.Max(0f, rotationSpeed);
            acceleration = Mathf.Max(0.0001f, acceleration);
            deceleration = Mathf.Max(0.0001f, deceleration);
            gravity = Mathf.Max(0f, gravity);
            jumpHeight = Mathf.Max(0f, jumpHeight);
        }
#endif
    }
}
