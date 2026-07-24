using UnityEngine;

namespace NineLives
{
    public struct MotorInput
    {
        public float Move;
        public bool JumpPressed;
        public bool JumpHeld;
        public bool JumpReleased;
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
        float chargeElapsed;
        bool chargingActive;

        bool pendingRelease;
        float pendingReleaseCharge;
        float pendingReleaseTimer;

        public PlatformerMotor(GameConfig config) { cfg = config; }

        public void Reset()
        {
            Velocity = Vector2.zero;
            Grounded = false;
            coyote = 0f;
            chargeElapsed = 0f;
            chargingActive = false;
            pendingRelease = false;
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

            // Settle onto the floor so the ground probe keeps finding it.
            if (grounded && Velocity.y < 0f) Velocity.y = -2f;

            float jumpMult = input.JumpMultiplier <= 0f ? 1f : input.JumpMultiplier;
            bool wantsMove = Mathf.Abs(input.Move) > 0.01f;

            // Moving when jump is pressed: fire the base jump immediately, no charging.
            // Stationary: start charging and wait for release to fire.
            if (input.JumpPressed && (grounded || coyote > 0f))
            {
                if (wantsMove)
                    Fire(0f, jumpMult);
                else
                    chargingActive = true;
            }

            bool charging = chargingActive && grounded && input.JumpHeld;

            Velocity.x = charging
                ? 0f
                : StepHorizontal(dt, input.Move, grounded, input.SpeedMultiplier);

            // chargeElapsed is cleared explicitly after each Fire() — don't also clear it here,
            // that would race the JumpReleased check below and always yield a 0 charge.
            if (charging)
                chargeElapsed = Mathf.Min(chargeElapsed + dt, cfg.jumpHoldTime);
            else if (!grounded)
                chargeElapsed = 0f; // no storing a charged jump for later use in the air

            if (input.JumpReleased && chargingActive)
            {
                if (grounded || coyote > 0f)
                {
                    Fire(chargeElapsed, jumpMult);
                }
                else
                {
                    pendingRelease = true;
                    pendingReleaseCharge = chargeElapsed;
                    pendingReleaseTimer = cfg.jumpBuffer;
                }
                chargeElapsed = 0f;
                chargingActive = false;
            }

            if (pendingRelease)
            {
                if (grounded)
                {
                    Fire(pendingReleaseCharge, jumpMult);
                    pendingRelease = false;
                }
                else
                {
                    pendingReleaseTimer -= dt;
                    if (pendingReleaseTimer <= 0f) pendingRelease = false;
                }
            }

            if (!JumpedThisStep)
            {
                float g = cfg.JumpGravity;
                if (Velocity.y < 0f) g *= cfg.fallGravityMultiplier;
                Velocity.y -= g * dt;
            }

            if (Velocity.y < -cfg.maxFallSpeed) Velocity.y = -cfg.maxFallSpeed;
        }

        void Fire(float charge, float jumpMult)
        {
            float t = Mathf.Clamp01(charge / cfg.jumpHoldTime);
            Velocity.y = Mathf.Lerp(cfg.BaseJumpVelocity, cfg.MaxJumpVelocity, t) * jumpMult;
            Grounded = false;
            coyote = 0f;
            JumpedThisStep = true;
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
