using System;
using UnityEngine;
using IronIvy.Systems.Camera;
using Unity.Cinemachine;

namespace IronIvy.Gameplay
{
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
        private Transform cam; // fallback Camera.main
        private Vector3 currentVelocity; // XZ velocity
        private Vector3 velocityRef;
        private float verticalVel;
        private bool wasMovingLastFrame;

        // events
        public event Action OnPlayerMoveStart;
        public event Action OnPlayerMoveStop;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();

            if (!animator)
            {
                animator = GetComponent<Animator>();
                if (!animator) animator = GetComponentInChildren<Animator>();
            }

            if (!cam && Camera.main) cam = Camera.main.transform;

            // init yaw/pitch from pivot to avoid start snap
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
            // stay enabled; just listen and gate
            if (autoEnableByCamera && CameraManager.HasInstance && thirdPersonCamRef != null)
            {
                CameraManager.Instance.OnCameraChanged += HandleCameraChanged;
            }

            // gate by current camera once at start
            GateByCurrentCamera();
        }

        private void OnDisable()
        {
            if (autoEnableByCamera && CameraManager.HasInstance)
                CameraManager.Instance.OnCameraChanged -= HandleCameraChanged;
        }

        private void Update()
        {
            if (!isTPSActive)
            {
                // inactive: iso controller handles movement thi only keep pivot follow in LateUpdate
                return;
            }

            // STEP 1: read input
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            Vector3 inputDir = new Vector3(h, 0f, v);
            if (inputDir.sqrMagnitude > 1f) inputDir.Normalize();

            // run modifier
            float targetSpeed = (Input.GetKey(KeyCode.LeftShift) ? runSpeed : walkSpeed) * inputDir.magnitude;

            // STEP 2: camera-relative move dir (flatten Y to avoid tilt issues)
            Vector3 moveDir;
            if (cameraPivot)
            {
                Vector3 camForward = cameraPivot.forward; camForward.y = 0f; camForward.Normalize();
                Vector3 camRight = cameraPivot.right; camRight.y = 0f; camRight.Normalize();
                moveDir = camForward * v + camRight * h;
            }
            else
            {
                moveDir = new Vector3(h, 0f, v);
            }
            if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

            // target velocity on XZ
            Vector3 targetVelocity = moveDir * targetSpeed;

            // accel/decel smoothing
            float smoothTime = (targetSpeed > 0.01f) ? Mathf.Max(0.0001f, acceleration)
                                                     : Mathf.Max(0.0001f, deceleration);
            currentVelocity = Vector3.SmoothDamp(currentVelocity, targetVelocity, ref velocityRef, smoothTime);

            // STEP 3: CharacterController.Move (with gravity)
            if (controller.isGrounded)
            {
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

            // STEP 5: rotate character toward move
            Vector3 flatVel = currentVelocity; flatVel.y = 0f;
            bool isMovingNow = flatVel.sqrMagnitude > 0.0001f;

            if (isMovingNow)
            {
                Quaternion targetRot = Quaternion.LookRotation(flatVel, Vector3.up);
                if (smoothRotate)
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
                else
                    transform.rotation = targetRot;
            }

            // Animator
            if (animator)
            {
                float speedParam = new Vector3(controller.velocity.x, 0f, controller.velocity.z).magnitude;
                animator.SetFloat("Speed", speedParam, 0.1f, Time.deltaTime);
                animator.SetBool("IsMoving", isMovingNow);
            }

            // events
            if (isMovingNow && !wasMovingLastFrame) OnPlayerMoveStart?.Invoke();
            else if (!isMovingNow && wasMovingLastFrame) OnPlayerMoveStop?.Invoke();
            wasMovingLastFrame = isMovingNow;
        }

        private void LateUpdate()
        {
            // PIVOT FOLLOW: always run (even when TPS gate is closed)
            if (cameraPivot)
            {
                Vector3 targetPos = new Vector3(
                    transform.position.x,
                    transform.position.y + pivotHeight,
                    transform.position.z
                );

                // framerate independent damping (1 - exp(-k dt))
                float t = 1f - Mathf.Exp(-pivotFollowDamping * Time.deltaTime);
                cameraPivot.position = Vector3.Lerp(cameraPivot.position, targetPos, t);
            }

            // RMB rotate only when TPS is active
            if (!isTPSActive) return;

            // basic cursor lock
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

            // STEP 4: orbit yaw/pitch (after move)
            if (cameraPivot && Input.GetMouseButton(1))
            {
                yaw += Input.GetAxis("Mouse X") * camSensitivityX;
                pitch -= Input.GetAxis("Mouse Y") * camSensitivityY;
                pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
                cameraPivot.rotation = Quaternion.Euler(pitch, yaw, 0f);
            }
        }

        // ===== CameraManager auto gate (no enable/disable) =====
        private void HandleCameraChanged(CinemachineCamera oldCam, CinemachineCamera newCam)
        {
            if (!autoEnableByCamera || thirdPersonCamRef == null) return;

            bool active = (newCam != null && newCam == thirdPersonCamRef);
            SetTPSActive(active);              // gate only
            if (active) ResyncCameraAnglesFromPivot();
        }

        private void GateByCurrentCamera()
        {
            if (!autoEnableByCamera || thirdPersonCamRef == null || !CameraManager.HasInstance)
            {
                // keep inspector value if we cannot decide
                SetTPSActive(isTPSActive);
                return;
            }

            var current = CameraManager.Instance.CurrentCamera;
            bool active = (current != null && current == thirdPersonCamRef);
            SetTPSActive(active);              // gate only
            if (active) ResyncCameraAnglesFromPivot();
        }

        // called by external switcher too
        public void SetTPSActive(bool value)
        {
            isTPSActive = value;               // do not toggle this.enabled here
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
            // quick guards
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
