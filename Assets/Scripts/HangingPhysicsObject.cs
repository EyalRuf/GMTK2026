using UnityEngine;

namespace NineLives
{
    /// Generic pendulum: a Rigidbody hinged to a fixed anchor point, swinging in the game's
    /// XY plane. Reusable for a cage the player lands on, a swinging spike trap, a lantern, etc
    /// — only the sprite/collider and the Inspector tuning differ per instance. Player contact
    /// (landing on top, walking into it) is driven externally via ApplyImpact, called from
    /// PlayerController.OnControllerColliderHit; this component doesn't know about the player.
    [RequireComponent(typeof(Rigidbody), typeof(HingeJoint))]
    public class HangingPhysicsObject : MonoBehaviour, ILevelResettable
    {
        public enum StartBehavior { AtRest, Kickstart }

        [Header("Rope / Chain")]
        [Tooltip("The fixed point this object hangs from. If left empty, ropeLength is used instead to place the anchor straight above this object's own pivot.")]
        public Transform hangAnchor;
        [Tooltip("How far above this object's own pivot to hinge from. With hangAnchor empty it puts a virtual pivot there. With hangAnchor set it needs `mount` as well — then this is the knob for how far up the chains run.")]
        public float ropeLength = 1.5f;
        [Tooltip("Optional, and only does anything alongside hangAnchor: the parent holding the anchor transforms (the platform prefab's `Mount`). Assign it and ropeLength becomes the per-instance chain-height knob — type a number and the mount slides in the Scene view. Leave empty to place the anchors by hand.")]
        public Transform mount;

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
        Vector3 startPos;
        Quaternion startRot;
        bool freshlyAwoken;
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
            if (hangAnchor != null && mount == null && !Mathf.Approximately(ropeLength, 1.5f))
                Debug.LogWarning($"{name}: ropeLength {ropeLength} does nothing while hangAnchor '{hangAnchor.name}' is assigned and mount is empty — the anchor transform wins. Move the anchor, or assign mount to drive it from ropeLength.", this);
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

            startPos = transform.position;
            startRot = transform.rotation;
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
            freshlyAwoken = true;
        }

        /// Levels are pre-placed and only enabled/disabled now, so a pendulum keeps its pose and its
        /// momentum across a restart — it comes back mid-swing, or parked wherever the player shoved
        /// it, and a rigidbody's velocity survives being disabled. Put the whole body back: authored
        /// pose, no velocity, pivot re-derived, swing tracking cleared.
        public void ResetToInitial()
        {
            if (rb == null) return; // never activated, so there's no captured pose to restore
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            transform.SetPositionAndRotation(startPos, startRot);
            rb.position = startPos;
            rb.rotation = startRot;

            // connectedAnchor is a plain world point that only FixedUpdate refreshes from hangAnchor;
            // re-derive it here so the first step after the reset solves against the right pivot even
            // if a LinkedMover left the mount somewhere else (its own reset may run after this one).
            if (hangAnchor != null)
            {
                joint.anchor = transform.InverseTransformPoint(hangAnchor.position);
                joint.connectedAnchor = hangAnchor.position;
            }
            else joint.connectedAnchor = transform.TransformPoint(joint.anchor);

            swingAmplitude = 0f;
            halfSwingPeak = 0f;
            lastAngularVel = 0f;

            // Awake already kicked on the level's first entry; don't stack a second impulse on it.
            if (startBehavior == StartBehavior.Kickstart && !freshlyAwoken)
                rb.AddTorque(Vector3.forward * initialSwingForce, ForceMode.Impulse);
            freshlyAwoken = false;
        }

        void OnValidate()
        {
            if (!Application.isPlaying) ApplyRopeLength();
        }

        /// Editor-time only: slides `mount`, and every anchor parented under it, so `hangAnchor`
        /// lands exactly `ropeLength` above this object's pivot, then re-authors the HingeJoint so
        /// the Scene gizmo agrees with the pivot Awake will compute. Deliberately not run at
        /// runtime — LinkedMover caches the mount's starting position in its own Awake, and Unity
        /// doesn't promise which Awake goes first, so moving the mount there is a coin flip.
        void ApplyRopeLength()
        {
            if (mount == null || hangAnchor == null || ropeLength <= 0f) return;
            if (!hangAnchor.IsChildOf(mount)) return;

            float delta = transform.position.y + ropeLength - hangAnchor.position.y;
            if (Mathf.Abs(delta) > 0.0001f) mount.position += Vector3.up * delta;

            var hinge = GetComponent<HingeJoint>();
            if (hinge != null) hinge.anchor = transform.InverseTransformPoint(hangAnchor.position);
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
