using UnityEngine;

namespace NineLives
{
    /// A dropped life. Falls as a physics box, then freezes into a solid platform
    /// once it has come to rest. A Trampoline corpse stays bouncy instead of freezing solid.
    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    public class Corpse : MonoBehaviour
    {
        [SerializeField] Material normalMat;
        [SerializeField] Material normalSettledMat;
        [SerializeField] Material trampolineMat;
        [SerializeField] Material trampolineSettledMat;

        GameConfig cfg;
        Rigidbody rb;
        BoxCollider col;
        MeshRenderer meshRenderer;
        float stillFor;
        MovingPlatform ridingPlatform;
        Vector3 ridingPlatformLastPos;
        public bool Settled { get; private set; }
        public CorpseKind Kind { get; private set; }
        public bool Held { get; private set; }

        public void Init(GameConfig config, Vector2 launchVelocity, CorpseKind kind = CorpseKind.Normal)
        {
            cfg = config;
            Kind = kind;
            rb = GetComponent<Rigidbody>();
            col = GetComponent<BoxCollider>();
            meshRenderer = GetComponent<MeshRenderer>();
            rb.mass = cfg.corpseMass;
            rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            // If it still spawns overlapping geometry (tight room, nowhere clear found), cap how
            // hard PhysX can shove it out so it settles nearby instead of rocketing across the level.
            rb.maxDepenetrationVelocity = 3f;
            rb.linearVelocity = new Vector3(launchVelocity.x, launchVelocity.y, 0f);

            meshRenderer.sharedMaterial = Kind == CorpseKind.Trampoline ? trampolineMat : normalMat;
        }

        void FixedUpdate()
        {
            if (rb == null || Held) return;

            if (Settled)
            {
                RideMovingPlatform();
                return;
            }

            // Keep it pinned to the play plane no matter what.
            var p = transform.position;
            if (Mathf.Abs(p.z) > 0.001f) transform.position = new Vector3(p.x, p.y, 0f);

            if (rb.linearVelocity.sqrMagnitude < 0.35f)
            {
                stillFor += Time.fixedDeltaTime;
                if (stillFor >= cfg.corpseSettleTime) Freeze();
            }
            else stillFor = 0f;
        }

        /// A settled (kinematic) corpse doesn't get pushed by physics, so if it's resting on a
        /// MovingPlatform, ride it the same way PlayerController does: track its position delta.
        void RideMovingPlatform()
        {
            if (ridingPlatform != null)
            {
                Vector3 delta = ridingPlatform.transform.position - ridingPlatformLastPos;
                if (delta.sqrMagnitude > 0f) transform.position += delta;
            }

            float halfHeight = col.size.y * 0.5f * transform.lossyScale.y;
            Vector3 origin = transform.position + Vector3.up * (halfHeight - 0.05f);
            ridingPlatform = Physics.Raycast(origin, Vector3.down, out var hit, 0.15f, ~0, QueryTriggerInteraction.Ignore)
                ? hit.collider.GetComponentInParent<MovingPlatform>()
                : null;
            if (ridingPlatform != null) ridingPlatformLastPos = ridingPlatform.transform.position;
        }

        void Freeze()
        {
            Settled = true;
            rb.linearVelocity = Vector3.zero;
            rb.isKinematic = true;
            meshRenderer.sharedMaterial = Kind == CorpseKind.Trampoline ? trampolineSettledMat : normalSettledMat;
        }

        /// Picked up by the carry ability: goes kinematic and stops colliding until dropped/thrown.
        public void PickUp()
        {
            Held = true;
            Settled = false;
            stillFor = 0f;
            rb.isKinematic = true;
            GetComponent<BoxCollider>().enabled = false;
        }

        public void SetHeldPosition(Vector3 pos) => transform.position = pos;

        /// Gently set down in place: re-freezes immediately as a solid platform.
        public void PutDown()
        {
            Held = false;
            GetComponent<BoxCollider>().enabled = true;
            rb.linearVelocity = Vector3.zero;
            Freeze();
        }

        /// Thrown with a launch velocity: falls and re-settles like a fresh corpse.
        public void Throw(Vector3 velocity)
        {
            Held = false;
            Settled = false;
            stillFor = 0f;
            GetComponent<BoxCollider>().enabled = true;
            rb.isKinematic = false;
            rb.linearVelocity = velocity;
        }
    }
}
