using UnityEngine;

namespace NineLives
{
    public struct MotorInput
    {
        public float Move;
        public bool JumpPressed;
        public bool JumpHeld;
    }

    /// Pure movement math. No Unity components, no side effects — the MonoBehaviour
    /// feeds it a grounded flag and gets a velocity back.
    public class PlatformerMotor
    {
        readonly GameConfig cfg;

        public Vector2 Velocity;
        public bool Grounded { get; private set; }
        public bool JumpedThisStep { get; private set; }

        float coyote;
        float buffer;

        public PlatformerMotor(GameConfig config) { cfg = config; }

        public void Reset()
        {
            Velocity = Vector2.zero;
            Grounded = false;
            coyote = 0f;
            buffer = 0f;
        }

        public void Bounce(float upwardSpeed)
        {
            Velocity.y = upwardSpeed;
            Grounded = false;
            coyote = 0f;
        }

        public void Tick(float dt, MotorInput input, bool grounded)
        {
            JumpedThisStep = false;
            Grounded = grounded;

            coyote = grounded ? cfg.coyoteTime : coyote - dt;
            buffer = input.JumpPressed ? cfg.jumpBuffer : buffer - dt;

            // Settle onto the floor so the ground probe keeps finding it.
            if (grounded && Velocity.y < 0f) Velocity.y = -2f;

            Velocity.x = StepHorizontal(dt, input.Move, grounded);

            if (buffer > 0f && coyote > 0f)
            {
                Velocity.y = cfg.JumpVelocity;
                buffer = 0f;
                coyote = 0f;
                Grounded = false;
                JumpedThisStep = true;
            }

            float g = cfg.JumpGravity;
            if (Velocity.y > 0f && !input.JumpHeld) g *= cfg.jumpCutGravityMultiplier;
            else if (Velocity.y < 0f) g *= cfg.fallGravityMultiplier;

            Velocity.y -= g * dt;
            if (Velocity.y < -cfg.maxFallSpeed) Velocity.y = -cfg.maxFallSpeed;
        }

        float StepHorizontal(float dt, float move, bool grounded)
        {
            float target = move * cfg.maxSpeed;
            bool wantsMove = Mathf.Abs(move) > 0.01f;

            float accel = wantsMove
                ? (grounded ? cfg.groundAcceleration : cfg.airAcceleration)
                : (grounded ? cfg.groundDeceleration : cfg.airDeceleration);

            bool reversing = wantsMove && Mathf.Abs(Velocity.x) > 0.01f
                             && Mathf.Sign(target) != Mathf.Sign(Velocity.x);
            if (reversing) accel *= cfg.turnBoost;

            return Mathf.MoveTowards(Velocity.x, target, accel * dt);
        }
    }
}
