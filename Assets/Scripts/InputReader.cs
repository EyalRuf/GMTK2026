using UnityEngine;
using UnityEngine.InputSystem;

namespace NineLives
{
    /// Polls keyboard + gamepad each frame. No asset wiring needed.
    public class InputReader
    {
        public float Move;
        public bool JumpPressed;
        public bool JumpHeld;
        public bool JumpReleased;
        public bool LookUpHeld;
        public bool LookDownHeld;
        public bool SacrificePressed;
        public bool RestartPressed;
        public bool CarryTogglePressed;
        public bool ThrowPressed;
        public Vector2 MouseScreenPosition;

        public bool PausePressed;
        public bool PrevLevelPressed;
        public bool NextLevelPressed;

        public void Sample()
        {
            Move = 0f;
            JumpPressed = JumpHeld = JumpReleased = SacrificePressed = RestartPressed = false;
            LookUpHeld = LookDownHeld = false;
            CarryTogglePressed = ThrowPressed = false;
            PausePressed = PrevLevelPressed = NextLevelPressed = false;

            var k = Keyboard.current;
            if (k != null)
            {
                if (k.aKey.isPressed || k.leftArrowKey.isPressed) Move -= 1f;
                if (k.dKey.isPressed || k.rightArrowKey.isPressed) Move += 1f;
                JumpPressed |= k.spaceKey.wasPressedThisFrame;
                JumpHeld |= k.spaceKey.isPressed;
                JumpReleased |= k.spaceKey.wasReleasedThisFrame;
                LookUpHeld |= k.wKey.isPressed || k.upArrowKey.isPressed;
                LookDownHeld |= k.sKey.isPressed || k.downArrowKey.isPressed;
                SacrificePressed |= k.qKey.wasPressedThisFrame || k.eKey.wasPressedThisFrame || k.leftShiftKey.wasPressedThisFrame;
                RestartPressed |= k.rKey.wasPressedThisFrame;
                PausePressed |= k.escapeKey.wasPressedThisFrame;
                PrevLevelPressed |= k.kKey.wasPressedThisFrame;
                NextLevelPressed |= k.lKey.wasPressedThisFrame;
            }

            var g = Gamepad.current;
            if (g != null)
            {
                float stick = g.leftStick.x.ReadValue();
                if (Mathf.Abs(stick) > 0.2f) Move += stick;
                float stickY = g.leftStick.y.ReadValue();
                LookUpHeld |= stickY > 0.4f || g.dpad.up.isPressed;
                LookDownHeld |= stickY < -0.4f || g.dpad.down.isPressed;
                JumpPressed |= g.buttonSouth.wasPressedThisFrame;
                JumpHeld |= g.buttonSouth.isPressed;
                JumpReleased |= g.buttonSouth.wasReleasedThisFrame;
                SacrificePressed |= g.buttonWest.wasPressedThisFrame || g.buttonNorth.wasPressedThisFrame;
                RestartPressed |= g.selectButton.wasPressedThisFrame;
                CarryTogglePressed |= g.rightShoulder.wasPressedThisFrame;
                ThrowPressed |= g.rightTrigger.wasPressedThisFrame;
                PausePressed |= g.startButton.wasPressedThisFrame;
            }

            var m = Mouse.current;
            if (m != null)
            {
                MouseScreenPosition = m.position.ReadValue();
                CarryTogglePressed |= m.rightButton.wasPressedThisFrame;
                ThrowPressed |= m.leftButton.wasPressedThisFrame;
            }

            Move = Mathf.Clamp(Move, -1f, 1f);
        }
    }
}
