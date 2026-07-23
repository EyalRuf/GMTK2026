using UnityEngine;

namespace NineLives
{
    /// The "Carry" upgrade ability: pick up a settled corpse, carry it (slower, trajectory
    /// preview shown), put it down gently, or throw it toward the mouse direction.
    /// Only active while GameManager has this upgrade armed for the current life.
    [RequireComponent(typeof(LineRenderer))]
    public class CorpseCarry : MonoBehaviour
    {
        GameConfig cfg;
        PlayerController player;
        Camera cam;
        LineRenderer line;
        Corpse held;

        public bool Enabled { get; private set; }
        public bool IsHolding => held != null;

        public void Configure(GameConfig config, PlayerController p, Camera camera)
        {
            cfg = config; player = p; cam = camera;
            line = GetComponent<LineRenderer>();
            line.startWidth = line.endWidth = 0.08f;
            line.material = GreyboxFactory.Make(GreyboxFactory.Carry, 0f, true);
            line.positionCount = 0;
        }

        public void SetEnabled(bool value)
        {
            Enabled = value;
            if (!Enabled && IsHolding) DropHeld();
        }

        /// Drop wherever it currently sits — used when the ability is revoked mid-life (e.g. on death).
        public void DropHeld()
        {
            if (!IsHolding) return;
            held.PutDown();
            held = null;
            line.positionCount = 0;
        }

        public void Sample(InputReader input)
        {
            if (!Enabled)
            {
                player.SpeedMultiplier = 1f;
                return;
            }

            if (input.CarryTogglePressed)
            {
                if (IsHolding) DropHeld();
                else TryPickUp();
            }

            if (IsHolding)
            {
                held.SetHeldPosition(transform.position + Vector3.up * cfg.carryHoldHeight);
                player.SpeedMultiplier = cfg.carrySpeedMultiplier;

                Vector3 dir = AimDirection(input.MouseScreenPosition);
                DrawTrajectory(held.transform.position, dir * cfg.carryThrowSpeed);

                if (input.ThrowPressed) Throw(dir);
            }
            else
            {
                player.SpeedMultiplier = 1f;
                line.positionCount = 0;
            }
        }

        void TryPickUp()
        {
            var hits = Physics.OverlapSphere(transform.position, cfg.carryPickupRange);
            Corpse best = null;
            float bestDist = float.MaxValue;
            foreach (var h in hits)
            {
                var c = h.GetComponentInParent<Corpse>();
                if (c == null || !c.Settled || c.Held) continue;
                float d = (c.transform.position - transform.position).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = c; }
            }
            if (best == null) return;
            best.PickUp();
            held = best;
        }

        void Throw(Vector3 dir)
        {
            held.Throw(dir * cfg.carryThrowSpeed);
            held = null;
            line.positionCount = 0;
        }

        Vector3 AimDirection(Vector2 mouseScreenPos)
        {
            if (cam != null)
            {
                Ray ray = cam.ScreenPointToRay(mouseScreenPos);
                Plane plane = new Plane(Vector3.forward, Vector3.zero);
                if (plane.Raycast(ray, out float dist))
                {
                    Vector3 worldPoint = ray.GetPoint(dist);
                    Vector3 d = worldPoint - transform.position;
                    d.z = 0f;
                    if (d.sqrMagnitude > 0.01f) return d.normalized;
                }
            }
            return Vector3.right;
        }

        void DrawTrajectory(Vector3 start, Vector3 initialVelocity)
        {
            int segments = cfg.carryTrajectorySegments;
            line.positionCount = segments;
            Vector3 gravity = Physics.gravity;
            for (int i = 0; i < segments; i++)
            {
                float t = (i / (float)(segments - 1)) * cfg.carryTrajectoryTime;
                Vector3 p = start + initialVelocity * t + 0.5f * gravity * t * t;
                line.SetPosition(i, p);
            }
        }
    }
}
