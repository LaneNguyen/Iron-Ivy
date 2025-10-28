using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
    public class StarterAssetsInputs : MonoBehaviour
    {
        [Header("Giá trị điều khiển nhân vật")]
        public Vector2 move;
        public Vector2 look;
        public bool jump;
        public bool sprint;
        public bool interact;

        [Header("Cài đặt di chuyển")]
        public bool analogMovement;

        [Header("Cài đặt chuột")]
        public bool cursorLocked = true;
        public bool cursorInputForLook = true;

#if ENABLE_INPUT_SYSTEM
        // được gọi bởi PlayerInput khi Behavior = Send Messages
        public void OnMove(InputValue value) { MoveInput(value.Get<Vector2>()); }
        public void OnLook(InputValue value)
        {
            if (cursorInputForLook)
                LookInput(value.Get<Vector2>());
        }
        public void OnJump(InputValue value) { JumpInput(value.isPressed); }
        public void OnSprint(InputValue value) { SprintInput(value.isPressed); }
        public void OnInteract(InputValue value) { InteractInput(value.isPressed); }
#endif

        // xử lý input thực tế
        public void MoveInput(Vector2 newMove) { move = newMove; }
        public void LookInput(Vector2 newLook) { look = newLook; }
        public void JumpInput(bool state) { jump = state; }
        public void SprintInput(bool state) { sprint = state; }
        public void InteractInput(bool state) { interact = state; }

        private void OnApplicationFocus(bool focus) { SetCursorState(cursorLocked); }
        private void SetCursorState(bool locked) { Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None; }

        private void LateUpdate()
        {
            // reset interact mỗi frame
            interact = false;
        }
    }
}
