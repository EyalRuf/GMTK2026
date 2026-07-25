using UnityEngine;

namespace NineLives
{
    /// Cosmetic only: a prop sitting loose inside a hanging object — the bones and skull rattling
    /// around in the swinging cage. It has no physics and no collider of its own; it just listens
    /// for the impacts the cage already reports, jolts, and springs back to its authored pose.
    public class LooseProp : MonoBehaviour
    {
        [Tooltip("The hanging object this rides inside. Left empty, it finds the nearest one above it.")]
        public HangingPhysicsObject ridesIn;
        [Tooltip("Impact impulse that produces a full-strength jolt. Harder hits clamp to it.")]
        public float fullJoltImpulse = 60f;
        [Tooltip("How far it can slide, in the parent's local units.")]
        public float shakeDistance = 0.06f;
        [Tooltip("How far it can tip, in degrees.")]
        public float shakeAngle = 12f;
        [Tooltip("Higher springs back faster, so it rattles at a higher pitch.")]
        public float stiffness = 120f;
        [Tooltip("Higher settles sooner.")]
        public float damping = 6f;

        Vector3 restPos;
        float restAngle;
        Vector2 offset, offsetVel;
        float angle, angleVel;
        bool settled = true;

        void Awake()
        {
            if (ridesIn == null) ridesIn = GetComponentInParent<HangingPhysicsObject>();
            restPos = transform.localPosition;
            restAngle = transform.localEulerAngles.z;
        }

        void OnEnable() { if (ridesIn != null) ridesIn.Impacted += Jolt; }
        void OnDisable() { if (ridesIn != null) ridesIn.Impacted -= Jolt; }

        void Jolt(float impulse)
        {
            float strength = Mathf.Clamp01(impulse / Mathf.Max(fullJoltImpulse, 0.001f));
            if (strength < 0.02f) return;

            // Kick hard enough that the spring's first swing peaks at roughly shakeDistance —
            // for a spring, peak amplitude is velocity / sqrt(stiffness).
            float toPeak = Mathf.Sqrt(stiffness);
            offsetVel += Random.insideUnitCircle.normalized * (strength * shakeDistance * toPeak);
            angleVel += Random.Range(-1f, 1f) * strength * shakeAngle * toPeak;
            settled = false;
        }

        void Update()
        {
            if (settled) return;

            float dt = Time.deltaTime;
            offsetVel += (-offset * stiffness - offsetVel * damping) * dt;
            offset = Vector2.ClampMagnitude(offset + offsetVel * dt, shakeDistance);
            angleVel += (-angle * stiffness - angleVel * damping) * dt;
            angle = Mathf.Clamp(angle + angleVel * dt, -shakeAngle, shakeAngle);

            if (offset.sqrMagnitude < 1e-8f && offsetVel.sqrMagnitude < 1e-6f
                && Mathf.Abs(angle) < 0.01f && Mathf.Abs(angleVel) < 0.1f)
            {
                offset = Vector2.zero; offsetVel = Vector2.zero;
                angle = 0f; angleVel = 0f;
                settled = true;
            }

            transform.localPosition = restPos + (Vector3)offset;
            transform.localEulerAngles = new Vector3(0f, 0f, restAngle + angle);
        }
    }
}
