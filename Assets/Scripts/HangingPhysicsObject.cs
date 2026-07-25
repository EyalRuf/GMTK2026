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

        [Header("Keep Alive")]
        [Tooltip("Periodically nudge the object so it never fully settles, e.g. an idle swinging spike trap.")]
        public bool keepSwinging = false;
        public float keepAliveInterval = 4f;
        public float keepAliveForce = 3f;
        [Tooltip("Only nudge if angular speed has dropped below this (rad/s).")]
        public float keepAliveVelocityThreshold = 0.15f;

        Rigidbody rb;
        HingeJoint joint;
        float keepAliveTimer;

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
            joint.anchor = hangAnchor != null
                ? transform.InverseTransformPoint(hangAnchor.position)
                : new Vector3(0f, ropeLength, 0f);
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

            keepAliveTimer = keepAliveInterval;

            if (startBehavior == StartBehavior.Kickstart)
                rb.AddTorque(Vector3.forward * initialSwingForce, ForceMode.Impulse);
        }

        void FixedUpdate()
        {
            if (!Mathf.Approximately(gravityScale, 1f))
                rb.AddForce(Physics.gravity * (gravityScale - 1f) * rb.mass, ForceMode.Force);

            if (!keepSwinging) return;
            keepAliveTimer -= Time.fixedDeltaTime;
            if (keepAliveTimer > 0f) return;
            keepAliveTimer = keepAliveInterval;
            if (rb.angularVelocity.magnitude < keepAliveVelocityThreshold)
                rb.AddTorque(Vector3.forward * keepAliveForce, ForceMode.Impulse);
        }

        /// External hit — from the player's controller, another swinging object, etc.
        public void ApplyImpact(Vector3 worldPoint, Vector3 force) => rb.AddForceAtPosition(force, worldPoint, ForceMode.Impulse);
    }
}
