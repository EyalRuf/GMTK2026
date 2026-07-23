using UnityEngine;

namespace NineLives
{
    public struct MotorInput
    {
        public float Move;
        public bool JumpPressed;
        public bool JumpHeld;
        public float SpeedMultiplier;
        public float JumpMultiplier;
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

            Velocity.x = StepHorizontal(dt, input.Move, grounded, input.SpeedMultiplier);

            if (buffer > 0f && coyote > 0f)
            {
                float jumpMult = input.JumpMultiplier <= 0f ? 1f : input.JumpMultiplier;
                Velocity.y = cfg.JumpVelocity * jumpMult;
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

        float StepHorizontal(float dt, float move, bool grounded, float speedMultiplier)
        {
            float target = move * cfg.maxSpeed * (speedMultiplier <= 0f ? 1f : speedMultiplier);
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
