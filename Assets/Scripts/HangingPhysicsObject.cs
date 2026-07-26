using UnityEngine;

namespace NineLives
{
    /// Generic pendulum: a Rigidbody hinged to a fixed anchor point, swinging in the game's
    /// XY plane. Reusable for a cage the player lands on, a swinging spike trap, a lantern, etc
    /// — only the sprite/collider and the Inspector tuning differ per instance. Player contact
    /// (landing on top, walking into it) is driven externally via ApplyImpact, called from
    /// PlayerController.OnControllerColliderHit; this component doesn't know about the player.
    [RequireComponent(typeof(Rigidbody), typeof(HingeJoint))]
    public class HangingPhysicsObject : MonoBehaviour
    {
        public enum StartBehavior { AtRest, Kickstart }

        [Header("Rope / Chain")]
        [Tooltip("The fixed point this object hangs from. If left empty, ropeLength is used instead to place the anchor straight above this object's own pivot.")]
        public Transform hangAnchor;
        [Tooltip("Used only when hangAnchor is not assigned: local distance above this object's pivot to hinge from.")]
        public float ropeLength = 1.5f;

        [Header("Physics")]
        public float mass = 3f;
        [Tooltip("Multiplier on world gravity. 1 = normal.")]
        public float gravityScale = 1f;
        [Tooltip("Rigidbody angular damping — higher settles the swing faster.")]
        public float swingDamping = 0.3f;

        [Header("Swing Limit")]
        public bool useSwingLimit = false;
        [Tooltip("Max angle in degrees from straight-down, each direction.")]
        public float maxSwingAngleDeg = 60f;

        [Header("Start Behavior")]
        public StartBehavior startBehavior = StartBehavior.AtRest;
        [Tooltip("Torque impulse applied at spawn for Kickstart (and repeated by the keep-alive below). Object stays at its authored hanging pose — only its velocity changes.")]
        public float initialSwingForce = 5f;

        [Header("Idle Sway")]
        [Tooltip("Continuously pumps the swing so the object never settles into a dead-still pose. Also brings it back down to the idle sway after the player knocks it around.")]
        public bool idleSway = false;
        [Tooltip("Swing amplitude to hold, in degrees from straight-down.")]
        public float idleSwayAngle = 8f;
        [Tooltip("How hard to pump. 1 is a sane default; higher reaches the idle angle faster (and recovers faster after a hit). Auto-scaled by mass and rope length, so it means the same thing on any hanging object.")]
        public float idleSwayResponse = 1f;

        Rigidbody rb;
        HingeJoint joint;
        float restAngleZ;
        float pivotInertia = 1f;
        float naturalFrequency = 1f;
        float swingAmplitude;
        float halfSwingPeak;
        float lastAngularVel;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.mass = mass;
            rb.angularDamping = swingDamping;
            rb.useGravity = true;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezePositionZ;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            // Preprocessing can rigidly lock a HingeJoint into whatever anchor it had the instant
            // it was enabled (see below) and never let it relax out of that state. Turning it off
            // fixes that, but softens the constraint — the anchor point can drift/stretch under
            // strong gravity unless the solver gets extra iterations to compensate.
            rb.solverIterations = Mathf.Max(rb.solverIterations, 30);
            rb.solverVelocityIterations = Mathf.Max(rb.solverVelocityIterations, 30);

            joint = GetComponent<HingeJoint>();
            joint.connectedBody = null;
            joint.axis = Vector3.forward;
            joint.enablePreprocessing = false;
            // Whatever was typed into the HingeJoint in the Inspector is about to be replaced. That
            // silence is a trap: the Scene view keeps drawing the authored pivot while the game
            // swings around a different one. Say so instead of letting it go unnoticed.
            var authoredAnchor = joint.anchor;
            joint.anchor = hangAnchor != null
                ? transform.InverseTransformPoint(hangAnchor.position)
                : new Vector3(0f, ropeLength, 0f);
            if (authoredAnchor.sqrMagnitude > 0.0001f && Vector3.Distance(authoredAnchor, joint.anchor) > 0.05f)
                Debug.LogWarning($"{name}: HingeJoint anchor {authoredAnchor} set in the Inspector is ignored — this component drives the pivot from " +
                                 (hangAnchor != null ? $"hangAnchor '{hangAnchor.name}'" : $"ropeLength {ropeLength}") +
                                 $", which resolves to {joint.anchor}. Move that instead.", this);
            if (hangAnchor != null && !Mathf.Approximately(ropeLength, 1.5f))
                Debug.LogWarning($"{name}: ropeLength {ropeLength} does nothing while hangAnchor '{hangAnchor.name}' is assigned — the anchor transform wins. Move the anchor to change the rope length.", this);
            // Auto-configure computes the world anchor from whatever anchor/axis the joint has
            // the moment it's enabled — which happens before this Awake overrides those fields,
            // so it locks onto Unity's defaults instead of ours. Set it explicitly instead: with
            // connectedBody null, connectedAnchor is a plain world-space point.
            joint.autoConfigureConnectedAnchor = false;
            joint.connectedAnchor = hangAnchor != null ? hangAnchor.position : transform.TransformPoint(joint.anchor);
            joint.useLimits = useSwingLimit;
            if (useSwingLimit)
            {
                var limits = joint.limits;
                limits.min = -maxSwingAngleDeg;
                limits.max = maxSwingAngleDeg;
                joint.limits = limits;
            }

            restAngleZ = transform.eulerAngles.z;
            // A torque that reads the same on a light lantern and a 20kg wrecking ball has to be
            // scaled by what the object actually is: its inertia about the hinge (not its own
            // centre) and the frequency the pendulum wants to swing at.
            float ropeSpan = Vector3.Distance(rb.worldCenterOfMass, joint.connectedAnchor);
            pivotInertia = Mathf.Max(rb.inertiaTensor.z, 0.01f) + mass * ropeSpan * ropeSpan;
            float g = Mathf.Abs(Physics.gravity.y) * gravityScale;
            naturalFrequency = ropeSpan > 0.001f ? Mathf.Sqrt(mass * g * ropeSpan / pivotInertia) : 1f;
            if (idleSway) rb.sleepThreshold = 0f;

            if (startBehavior == StartBehavior.Kickstart)
                rb.AddTorque(Vector3.forward * initialSwingForce, ForceMode.Impulse);
        }

        void FixedUpdate()
        {
            // connectedBody is null, so connectedAnchor is a plain world-space point that PhysX
            // never re-reads from hangAnchor on its own — without this, a moving anchor (e.g. a
            // LinkedMover lowering the mount) leaves the joint clamped to its Awake-time position
            // and the cage stays put while everything else (chain visuals, mount) moves around it.
            if (hangAnchor != null)
                joint.connectedAnchor = hangAnchor.position;

            if (!Mathf.Approximately(gravityScale, 1f))
                rb.AddForce(Physics.gravity * (gravityScale - 1f) * rb.mass, ForceMode.Force);

            if (idleSway) DriveIdleSway();
        }

        /// Feeds just enough energy back in to hold a lazy idle swing forever. Pumping along the
        /// current direction of travel (rather than on a timer) means the drive automatically
        /// matches the pendulum's own rhythm, so it looks like momentum, not like a shove.
        void DriveIdleSway()
        {
            float angle = Mathf.DeltaAngle(restAngleZ, transform.eulerAngles.z);
            float angularVel = rb.angularVelocity.z;

            // Amplitude of the swing = the peak angle reached since it last turned around; latch
            // it in the moment it turns around again.
            halfSwingPeak = Mathf.Max(halfSwingPeak, Mathf.Abs(angle));
            if (angularVel * lastAngularVel < 0f)
            {
                swingAmplitude = halfSwingPeak;
                halfSwingPeak = 0f;
            }
            lastAngularVel = angularVel;

            float missingAngle = idleSwayAngle - swingAmplitude;
            if (missingAngle <= 0f) return; // swinging wider than idle (player hit it) — let damping bleed it off

            float dir = Mathf.Abs(angularVel) > 0.001f ? Mathf.Sign(angularVel)
                      : Mathf.Abs(angle) > 0.01f ? -Mathf.Sign(angle)
                      : 1f;
            float torque = missingAngle * Mathf.Deg2Rad * pivotInertia * naturalFrequency * idleSwayResponse;
            rb.AddTorque(Vector3.forward * dir * torque, ForceMode.Force);
        }

        /// Raised on every hit with the impulse magnitude. Cosmetic listeners (LooseProp on the
        /// bones rattling inside the cage) hook this instead of sniffing the physics themselves.
        public event System.Action<float> Impacted;

        /// External hit — from the player's controller, another swinging object, etc.
        public void ApplyImpact(Vector3 worldPoint, Vector3 force)
        {
            rb.AddForceAtPosition(force, worldPoint, ForceMode.Impulse);
            Impacted?.Invoke(force.magnitude);
        }
    }
}
