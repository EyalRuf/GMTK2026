using UnityEngine;

namespace NineLives
{
    /// Thin MonoBehaviour: reads a MotorInput, drives a CharacterController with the
    /// PlatformerMotor's velocity, and handles corpse-bounce. No game-flow logic here.
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        GameConfig cfg;
        CharacterController cc;
        PlatformerMotor motor;
        Transform mesh;

        bool wasGrounded;
        public bool Grounded { get; private set; }
        public Vector2 Velocity => motor.Velocity;
        public Vector3 FeetPosition => transform.position;
        public bool JumpedThisStep { get; private set; }
        public bool BouncedThisStep { get; private set; }
        public bool LandedThisStep { get; private set; }

        public void Configure(GameConfig config)
        {
            cfg = config;
            motor = new PlatformerMotor(cfg);

            cc = GetComponent<CharacterController>();
            cc.height = cfg.playerHeight;
            cc.radius = cfg.playerRadius;
            cc.center = Vector3.up * (cfg.playerHeight * 0.5f);
            cc.skinWidth = 0.02f;
            cc.minMoveDistance = 0f;

            var m = GameObject.CreatePrimitive(PrimitiveType.Cube);
            m.name = "CatMesh";
            Destroy(m.GetComponent<Collider>());
            m.transform.SetParent(transform, false);
            m.transform.localScale = new Vector3(cfg.playerRadius * 2f, cfg.playerHeight, cfg.playerRadius * 2f);
            m.transform.localPosition = Vector3.up * (cfg.playerHeight * 0.5f);
            m.GetComponent<MeshRenderer>().sharedMaterial = GreyboxFactory.Make(GreyboxFactory.Player, 0.2f);
            mesh = m.transform;
            // little "ear" nub so facing direction reads
            var ear = GreyboxFactory.Box("Ear", mesh, new Vector3(0.25f, 0.55f, 0f),
                new Vector3(0.3f, 0.3f, 0.3f), GreyboxFactory.Make(GreyboxFactory.Player * 0.8f, 0.2f), false);
            ear.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
        }

        public void Spawn(Vector3 feet)
        {
            cc.enabled = false;
            transform.position = new Vector3(feet.x, feet.y, 0f);
            cc.enabled = true;
            motor.Reset();
            wasGrounded = false;
            gameObject.SetActive(true);
        }

        public void Tick(MotorInput input, float dt)
        {
            JumpedThisStep = BouncedThisStep = LandedThisStep = false;

            bool grounded = Probe(out bool onCorpse);
            float impactVy = motor.Velocity.y;

            bool justLanded = grounded && !wasGrounded && impactVy < 0f;
            bool bounce = justLanded && onCorpse && impactVy < -cfg.corpseBounceThreshold;

            motor.Tick(dt, input, grounded && !bounce);

            if (bounce)
            {
                float mult = input.JumpHeld ? cfg.corpseHoldBounceMultiplier : 1f;
                float up = Mathf.Min(-impactVy * cfg.corpseBounciness * mult, cfg.corpseMaxBounce);
                motor.Bounce(up);
                BouncedThisStep = true;
            }
            else if (justLanded && impactVy < -3f)
            {
                LandedThisStep = true;
            }

            JumpedThisStep = motor.JumpedThisStep;
            Grounded = motor.Grounded;

            var v = new Vector3(motor.Velocity.x, motor.Velocity.y, 0f);
            cc.Move(v * dt);

            // stay pinned to z=0
            if (Mathf.Abs(transform.position.z) > 0.0001f)
            {
                var p = transform.position; p.z = 0f;
                cc.enabled = false; transform.position = p; cc.enabled = true;
            }

            if (Mathf.Abs(motor.Velocity.x) > 0.15f)
                mesh.localScale = new Vector3(
                    Mathf.Sign(motor.Velocity.x) * Mathf.Abs(mesh.localScale.x),
                    mesh.localScale.y, mesh.localScale.z);

            wasGrounded = grounded;
        }

        bool Probe(out bool corpse)
        {
            corpse = false;
            float r = cfg.playerRadius * 0.92f;
            Vector3 origin = transform.position + Vector3.up * (cfg.playerRadius + 0.02f);
            float dist = cfg.playerRadius + cfg.groundProbeDepth;

            var hits = Physics.SphereCastAll(origin, r, Vector3.down, dist, ~0, QueryTriggerInteraction.Ignore);
            bool grounded = false;
            foreach (var h in hits)
            {
                if (h.collider.transform.IsChildOf(transform)) continue;
                if (h.collider.transform == transform) continue;
                grounded = true;
                if (h.collider.GetComponentInParent<Corpse>() != null) corpse = true;
            }
            return grounded && motor.Velocity.y <= 0.5f;
        }
    }
}
