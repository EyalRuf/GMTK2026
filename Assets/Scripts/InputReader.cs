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
        public bool SacrificePressed;
        public bool RestartPressed;

        public void Sample()
        {
            Move = 0f;
            JumpPressed = JumpHeld = SacrificePressed = RestartPressed = false;

            var k = Keyboard.current;
            if (k != null)
            {
                if (k.aKey.isPressed || k.leftArrowKey.isPressed) Move -= 1f;
                if (k.dKey.isPressed || k.rightArrowKey.isPressed) Move += 1f;
                JumpPressed |= k.spaceKey.wasPressedThisFrame || k.wKey.wasPressedThisFrame || k.upArrowKey.wasPressedThisFrame;
                JumpHeld |= k.spaceKey.isPressed || k.wKey.isPressed || k.upArrowKey.isPressed;
                SacrificePressed |= k.qKey.wasPressedThisFrame || k.eKey.wasPressedThisFrame || k.leftShiftKey.wasPressedThisFrame;
                RestartPressed |= k.rKey.wasPressedThisFrame;
            }

            var g = Gamepad.current;
            if (g != null)
            {
                float stick = g.leftStick.x.ReadValue();
                if (Mathf.Abs(stick) > 0.2f) Move += stick;
                JumpPressed |= g.buttonSouth.wasPressedThisFrame;
                JumpHeld |= g.buttonSouth.isPressed;
                SacrificePressed |= g.buttonWest.wasPressedThisFrame || g.buttonNorth.wasPressedThisFrame;
                RestartPressed |= g.selectButton.wasPressedThisFrame;
            }

            Move = Mathf.Clamp(Move, -1f, 1f);
        }
    }
}
