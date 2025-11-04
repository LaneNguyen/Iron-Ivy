
using System;
using UnityEngine;
using IronIvy.Systems.Camera;     
using Unity.Cinemachine;          

namespace IronIvy.Gameplay
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerThirdPersonController : MonoBehaviour
    {
        // ================== Inspector Fields ==================

        [Header("Movement Settings")]
        [Tooltip("Toc do di bo m/s")]
        public float walkSpeed = 3f;

        [Tooltip("Toc do chay m/s (giu Left Shift)")]
        public float runSpeed = 6f;

        [Tooltip("Toc do quay huong (gia tri lon = quay nhanh hon)")]
        public float rotationSpeed = 12f;

        [Tooltip("Thoi gian mem hoa tang toc (nho = nhay hon)")]
        public float acceleration = 0.08f;

        [Tooltip("Thoi gian giam toc (nho = nhạy hơn)")]
        public float deceleration = 0.12f;

        [Tooltip("Neu bat thi quay huong mem; tat thi quay ngay lap tuc")]
        public bool smoothRotate = true;

        [Header("Camera Settings")]
        [Tooltip("Pivot de camera follow va xoay quanh player (camera la child cua pivot)")]
        public Transform cameraPivot; // follow point (y offset)

        [Tooltip("Do nhay chuot ngang (yaw) khi giu chuot phai")]
        public float camSensitivityX = 2f;

        [Tooltip("Do nhay chuot doc (pitch) khi giu chuot phai")]
        public float camSensitivityY = 1.5f;

        [Tooltip("Goc nhin toi thieu (nhin len xuong)")]
        public float minPitch = -40f;

        [Tooltip("Goc nhin toi da (nhin len xuong)")]
        public float maxPitch = 60f;

        [Header("Animation")]
        [Tooltip("Animator co tham so: bool IsMoving, float Speed")]
        public Animator animator;

        [Header("Gravity / Jump (optional)")]
        [Tooltip("Gia toc trong truong (so duong)")]
        public float gravity = 9.81f;

        [Tooltip("Do cao nhay (m)")]
        public float jumpHeight = 1.2f;

        [Tooltip("Bat nhay bang phim Space")]
        public bool enableJump = false;

        [Header("Camera Integration (optional)")]
        [Tooltip("Gan Cinemachine camera tu sau lung, dung de tu bat/tat controller theo camera dang active")]
        public CinemachineCamera thirdPersonCamRef;

        [Tooltip("Neu bat, controller se tu bat/tat dua tren CameraManager")]
        public bool autoEnableByCamera = true;

        // Trang thai xoay camera
        private float yaw, pitch;

        // Thanh phan can thiet
        private CharacterController controller;
        private Transform cam; // Camera.main (khong bat buoc, dung lam fallback)

        // Mem hoa van toc
        private Vector3 currentVelocity; // van toc XZ dang dung (m/s)
        private Vector3 velocityRef;     // bien tro giup SmoothDamp
        private float verticalVel;       // van toc Y cho trong truong/nhay

        // Su kien chuyen dong
        private bool wasMovingLastFrame;

        // ================== Su kien cho he thong ngoai đe dành cho tuong lai ==================
        public event Action OnPlayerMoveStart;
        public event Action OnPlayerMoveStop;


        private void Awake()
        {
            controller = GetComponent<CharacterController>();

            // Thu tu dong tim animator neu chua gan
            if (!animator)
            {
                animator = GetComponent<Animator>();
                if (!animator) animator = GetComponentInChildren<Animator>();
            }

            // Lay main camera lam fallback
            if (!cam && Camera.main) cam = Camera.main.transform;

            // Khoi tao goc yaw/pitch tu cameraPivot de tranh bi "nhay" luc bat dau
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
            // Neu muon auto bat/tat theo CameraManager thi dang ky lang nghe su kien doi camera
            if (autoEnableByCamera && CameraManager.HasInstance && thirdPersonCamRef != null)
            {
                CameraManager.Instance.OnCameraChanged += HandleCameraChanged;
                // Kiem tra lan dau theo camera hien tai
                SyncEnableWithCurrentCamera();
            }
        }

        private void OnDisable()
        {
            if (autoEnableByCamera && CameraManager.HasInstance)
            {
                CameraManager.Instance.OnCameraChanged -= HandleCameraChanged;
            }
        }

        private void Update()
        {
            // Step 1 Nhap input truc ngang/doc (WASD hoac phim mui ten)
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");

            // inputDir chi de tham khao
            Vector3 inputDir = new Vector3(h, 0f, v);
            if (inputDir.sqrMagnitude > 1f) inputDir.Normalize();

            // Giu Left Shift de chay
            float targetSpeed = (Input.GetKey(KeyCode.LeftShift) ? runSpeed : walkSpeed) * inputDir.magnitude;

            // Step 2: Tinh huong di chuyen theo huong camera
            // - Bien WASD sang vector huong theo camera dang nhin

            Vector3 moveDir;
            if (cameraPivot)
            {
                Vector3 camForward = cameraPivot.forward; camForward.y = 0f; camForward.Normalize();
                Vector3 camRight = cameraPivot.right; camRight.y = 0f; camRight.Normalize();
                moveDir = camForward * v + camRight * h;
            }
            else
            {
                // Neu khong co pivot, dung he truc the gioi
                moveDir = new Vector3(h, 0f, v);
            }
            if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

            // Muc tieu van toc XZ (m/s)
            Vector3 targetVelocity = moveDir * targetSpeed;

            // Chon thoi gian mem hoa: khac nhau giua tang toc va giam toc
            float smoothTime = (targetSpeed > 0.01f) ? Mathf.Max(0.0001f, acceleration)
                                                     : Mathf.Max(0.0001f, deceleration);

            // Mem hoa van toc XZ
            currentVelocity = Vector3.SmoothDamp(currentVelocity, targetVelocity, ref velocityRef, smoothTime);

            // Step 3: Goi CharacterController.Move()
            // - Cong trong truong va (tuy chon) nhay
            if (controller.isGrounded)
            {
                // Gia tri nho am de bam dat chac
                verticalVel = -0.5f;

                if (enableJump && Input.GetKeyDown(KeyCode.Space))
                {
                    // v = sqrt(2 * g * h)
                    verticalVel = Mathf.Sqrt(2f * gravity * Mathf.Max(0.01f, jumpHeight));
                }
            }
            else
            {
                verticalVel -= gravity * Time.deltaTime;
            }

            Vector3 frameMotion = new Vector3(currentVelocity.x, verticalVel, currentVelocity.z) * Time.deltaTime;
            controller.Move(frameMotion);

            // Step 4: Xu ly xoay chuot phai (Right Mouse Button)
            // - Cap nhat yaw/pitch va ap dung vao cameraPivot
            if (cameraPivot && Input.GetMouseButton(1))
            {
                yaw += Input.GetAxis("Mouse X") * camSensitivityX;
                pitch -= Input.GetAxis("Mouse Y") * camSensitivityY;
                pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

                cameraPivot.rotation = Quaternion.Euler(pitch, yaw, 0f);
            }

            // Dam bao pivot follow vi tri player (giu nguyen offset Y cua pivot)
            if (cameraPivot)
            {
                Vector3 p = cameraPivot.position;
                p.x = transform.position.x;
                p.z = transform.position.z;
                cameraPivot.position = p;
            }

            // Step 5: Xoay nhan vat ve huong di chuyen (smooth rotation)
            Vector3 flatVel = currentVelocity; flatVel.y = 0f;
            bool isMoving = flatVel.sqrMagnitude > 0.0001f;

            if (isMoving)
            {
                Quaternion targetRot = Quaternion.LookRotation(flatVel, Vector3.up);
                if (smoothRotate)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
                }
                else
                {
                    transform.rotation = targetRot;
                }
            }

            // Animator: cap nhat IsMoving va Speed
            if (animator)
            {
                float speedParam = new Vector3(controller.velocity.x, 0f, controller.velocity.z).magnitude;
                animator.SetFloat("Speed", speedParam, 0.1f, Time.deltaTime);
                animator.SetBool("IsMoving", isMoving);
            }

            // Su kien di chuyen: bat dau / dung lai
            if (isMoving && !wasMovingLastFrame) OnPlayerMoveStart?.Invoke();
            else if (!isMoving && wasMovingLastFrame) OnPlayerMoveStop?.Invoke();

            wasMovingLastFrame = isMoving;
        }

        // ================== CameraManager kết hop ở đây ==================

        // Ham xu ly khi CameraManager doi camera
        private void HandleCameraChanged(CinemachineCamera oldCam, CinemachineCamera newCam)
        {
            // Neu camera moi dung voi camera third person duoc chi dinh -> bat controller
            // Nguoc lai -> tat controller (de controller isometric hoat dong)
            bool shouldEnable = (newCam != null && thirdPersonCamRef != null && newCam == thirdPersonCamRef);
            if (enabled != shouldEnable) enabled = shouldEnable;
        }

        // Dong bo trang thai enable theo camera dang active luc bat dau
        private void SyncEnableWithCurrentCamera()
        {
            if (!CameraManager.HasInstance || thirdPersonCamRef == null) return;
            var current = CameraManager.Instance.CurrentCamera;
            bool shouldEnable = (current != null && current == thirdPersonCamRef);
            if (enabled != shouldEnable) enabled = shouldEnable;
        }

        // ================== Public API ở day==================

        // Chuyen che do camera de dong bo goc quay
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
            // Gioi han pitch hop ly
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
