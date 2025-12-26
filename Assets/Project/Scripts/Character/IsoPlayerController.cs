using UnityEngine;
using UnityEngine.InputSystem;

namespace IronIvy.Gameplay
{
    [RequireComponent(typeof(CharacterController))]
    public class IsoPlayerController : MonoBehaviour
    {
        [Header("Input System (optional)")]
        public InputActionReference moveAction;
        public InputActionReference runAction;

        [Header("Legacy Input (optional)")]
        public string legacyHorizontal = "Horizontal";
        public string legacyVertical = "Vertical";
        public KeyCode legacyRunKey = KeyCode.LeftShift;

        [Header("Movement")]
        public float moveSpeed = 5f;
        public float runMultiplier = 1.5f;
        public float accelerationTime = 0.06f;
        public float decelerationTime = 0.08f;
        public float rotationMaxDegree = 900f;
        public float inputDeadZone = 0.06f;

        [Header("References")]
        public Camera mainCamera;
        public Animator animator;

        [Header("Animator Params (BlendTree: X=Side, Y=Speed)")]
        public string paramSide = "Side";                 // float
        public string paramSpeed = "Speed";               // float
        public string paramRun = "run";                   // bool OR float/int (tùy controller)
        public string paramSpeedMultiplier = "speedMultiplier"; // float (optional)

        [Header("Animator Feel")]
        [Tooltip("0 = phản hồi ngay. 0.03-0.08 = mượt nhẹ.")]
        public float animatorDamp = 0.05f;

        [Tooltip("Ngưỡng input nhỏ coi như đứng yên.")]
        public float movingThreshold = 0.05f;

        private CharacterController _cc;
        private bool _useNewInput;

        private Vector2 _moveInput;
        private Vector3 _smoothedVelocity;
        private Vector3 _velRef;

        private int hSide;
        private int hSpeed;
        private int hRun;
        private int hSpeedMul;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            if (!mainCamera) mainCamera = Camera.main;

            // Quan trọng: ưu tiên Animator cùng GameObject (để khỏi trỏ nhầm)
            if (!animator)
            {
                animator = GetComponent<Animator>();
                if (!animator) animator = GetComponentInChildren<Animator>(true);
            }

#if ENABLE_INPUT_SYSTEM
            _useNewInput = (moveAction != null);
#else
            _useNewInput = false;
#endif

            hSide = Animator.StringToHash(paramSide);
            hSpeed = Animator.StringToHash(paramSpeed);
            hRun = Animator.StringToHash(paramRun);
            hSpeedMul = Animator.StringToHash(paramSpeedMultiplier);
        }

        private void OnEnable()
        {
#if ENABLE_INPUT_SYSTEM
            if (_useNewInput && moveAction != null) moveAction.action.Enable();
            if (_useNewInput && runAction != null) runAction.action.Enable();
#endif
        }

        private void OnDisable()
        {
#if ENABLE_INPUT_SYSTEM
            if (_useNewInput && moveAction != null) moveAction.action.Disable();
            if (_useNewInput && runAction != null) runAction.action.Disable();
#endif
        }

        private void Update()
        {
            // 1) Read input
            Vector2 raw = ReadMove();
            if (raw.sqrMagnitude < inputDeadZone * inputDeadZone) raw = Vector2.zero;
            _moveInput = Vector2.ClampMagnitude(raw, 1f);

            bool isRunHeld = ReadRunHeld();

            // 2) World direction by camera
            Vector3 moveDirWorld = Vector3.zero;
            if (_moveInput.sqrMagnitude > 0.0001f)
            {
                if (!mainCamera)
                {
                    moveDirWorld = new Vector3(_moveInput.x, 0f, _moveInput.y);
                }
                else
                {
                    Vector3 camF = mainCamera.transform.forward;
                    camF.y = 0f;
                    camF.Normalize();

                    Vector3 camR = mainCamera.transform.right;
                    camR.y = 0f;
                    camR.Normalize();

                    moveDirWorld = camR * _moveInput.x + camF * _moveInput.y;
                    if (moveDirWorld.sqrMagnitude > 1f) moveDirWorld.Normalize();
                }
            }

            // 3) Smooth velocity
            float finalSpeed = moveSpeed * (isRunHeld ? runMultiplier : 1f);
            Vector3 targetVel = moveDirWorld * finalSpeed;

            float smoothTime = (targetVel.sqrMagnitude > 0.0001f) ? accelerationTime : decelerationTime;

            _smoothedVelocity = Vector3.SmoothDamp(
                _smoothedVelocity,
                targetVel,
                ref _velRef,
                Mathf.Max(0.0001f, smoothTime)
            );

            // 4) Move
            _cc.SimpleMove(_smoothedVelocity);

            // 5) Rotate snappy (theo hướng input/camera)
            if (moveDirWorld.sqrMagnitude > 0.0001f && rotationMaxDegree > 0f)
            {
                Quaternion toRot = Quaternion.LookRotation(moveDirWorld, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    toRot,
                    rotationMaxDegree * Time.deltaTime
                );
            }

            // 6) Animator (no animator.speed hack)
            UpdateAnimator(isRunHeld);
        }

        private void UpdateAnimator(bool isRunHeld)
        {
            if (!animator) return;

            float inputMag01 = Mathf.Clamp01(_moveInput.magnitude);
            bool isMoving = inputMag01 > movingThreshold;

            // Side: trái/phải theo input X (đúng isometric)
            float side = isMoving ? _moveInput.x : 0f;

            // Speed: world speed (m/s) để match BlendTree nếu nó đặt Pos theo giá trị lớn (2..5..6)
            float speed = _smoothedVelocity.magnitude;

            SetFloatSafe(hSide, side, animatorDamp);
            SetFloatSafe(hSpeed, speed, animatorDamp);

            // run + speedMultiplier (optional)
            SetRunSafe(isRunHeld);
            SetFloatSafe(hSpeedMul, isRunHeld ? runMultiplier : 1f, animatorDamp);
        }

        private void SetFloatSafe(int hash, float value, float damp)
        {
            if (!animator) return;

            for (int i = 0; i < animator.parameterCount; i++)
            {
                var p = animator.parameters[i];
                if (p.nameHash != hash) continue;
                if (p.type != AnimatorControllerParameterType.Float) return;

                if (damp <= 0f) animator.SetFloat(hash, value);
                else animator.SetFloat(hash, value, damp, Time.deltaTime);
                return;
            }
        }

        private void SetRunSafe(bool isRunHeld)
        {
            if (!animator) return;

            for (int i = 0; i < animator.parameterCount; i++)
            {
                var p = animator.parameters[i];
                if (p.nameHash != hRun) continue;

                if (p.type == AnimatorControllerParameterType.Bool) animator.SetBool(hRun, isRunHeld);
                else if (p.type == AnimatorControllerParameterType.Float)
                {
                    if (animatorDamp <= 0f) animator.SetFloat(hRun, isRunHeld ? 1f : 0f);
                    else animator.SetFloat(hRun, isRunHeld ? 1f : 0f, animatorDamp, Time.deltaTime);
                }
                else if (p.type == AnimatorControllerParameterType.Int) animator.SetInteger(hRun, isRunHeld ? 1 : 0);

                return;
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

        private bool ReadRunHeld()
        {
#if ENABLE_INPUT_SYSTEM
            if (_useNewInput && runAction != null)
            {
                float v = runAction.action.ReadValue<float>();
                return v > 0.5f;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(legacyRunKey);
#else
            return false;
#endif
        }
    }
}
