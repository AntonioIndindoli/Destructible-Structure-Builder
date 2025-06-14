using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
    public class  StarterAssetsInputs : MonoBehaviour
    {
        [Header("Character Input Values")]
        public Vector2 move;
        public Vector2 look;
        public bool jump;
        public bool sprint;
        public bool shoot;
        public bool escape;
        
        [Header("Movement Settings")]
        public bool analogMovement;

        [Header("Mouse Cursor Settings")]
        public bool cursorLocked = true;
        public bool cursorInputForLook = true;

        // Add flag to enable or disable input
        private bool isInputEnabled = true;

#if ENABLE_INPUT_SYSTEM
        public void OnMove(InputValue value)
        {
            if (isInputEnabled)
            {
                MoveInput(value.Get<Vector2>());
            }
        }

        public void OnLook(InputValue value)
        {
            if (isInputEnabled && cursorInputForLook)
            {
                LookInput(value.Get<Vector2>());
            }
        }

        public void OnJump(InputValue value)
        {
            if (isInputEnabled)
            {
                JumpInput(value.isPressed);
            }
        }

        public void OnSprint(InputValue value)
        {
            if (isInputEnabled)
            {
                SprintInput(value.isPressed);
            }
        }

        public void OnShoot(InputValue value)
        {
            if (isInputEnabled)
            {
                ShootInput(value.isPressed);
            }
        }

        public void OnEscape(InputValue value)
        {
            EscapeInput(value.isPressed);
        }
#endif

        public void MoveInput(Vector2 newMoveDirection)
        {
            move = newMoveDirection;
        }

        public void LookInput(Vector2 newLookDirection)
        {
            look = newLookDirection;
        }

        public void JumpInput(bool newJumpState)
        {
            jump = newJumpState;
        }

        public void SprintInput(bool newSprintState)
        {
            sprint = newSprintState;
        }

        public void ShootInput(bool newShootState)
        {
            shoot = newShootState;
        }

        public void EscapeInput(bool newEscapeState)
        {
            escape = newEscapeState;

            if (escape)
            {
                ToggleCursorState(); // Toggle between locking/unlocking cursor and enabling/disabling input
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            SetCursorState(cursorLocked);
        }

        private void SetCursorState(bool newState)
        {
            Cursor.lockState = newState ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !newState; // Show the cursor when unlocked
        }

        private void ToggleCursorState()
        {
            // Toggle cursor lock and input state
            cursorLocked = !cursorLocked;
            isInputEnabled = cursorLocked;

            SetCursorState(cursorLocked);
        }
    }
}
