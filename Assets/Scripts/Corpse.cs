using System.Collections.Generic;
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

        [Header("Bob (dip when landed on)  — tuned here, not in GameConfig")]
        [Tooltip("Downward velocity impulse when something lands on the body.")]
        [SerializeField] float bobKick = 4f;
        [Tooltip("Higher = springs back faster.")]
        [SerializeField] float bobStiffness = 180f;
        [Tooltip("Higher = settles with fewer wobbles.")]
        [SerializeField] float bobDamping = 12f;
        [Tooltip("Max dip depth in meters. Keep small so the cat never loses footing.")]
        [SerializeField] float bobMaxDepth = 0.12f;

        GameConfig cfg;
        Rigidbody rb;
        BoxCollider col;
        MeshRenderer meshRenderer;
        SpringSquash visualSquash;
        CatTint catTint;
        float stillFor;
        Transform ridingSurface;
        Vector3 ridingSurfaceLastPos;
        Transform probedCarrier;
        Rigidbody probedCarrierBody;
        Transform carrier;
        Vector3 attachLocalPos;
        Quaternion attachLocalRot;
        readonly List<Collider> ignored = new();
        Transform homeParent;
        Vector3 unattachedLocalScale = Vector3.one;
        float bobValue, bobVel, bobApplied;
        public bool Settled { get; private set; }
        public CorpseKind Kind { get; private set; }
        public bool Held { get; private set; }
        /// True while parented onto a moving surface — a platform, a lift, a hanging cage, or a
        /// corpse that is itself attached to one.
        public bool AttachedToCarrier => carrier != null;

        void Awake() => homeParent = transform.parent;

        public void Init(GameConfig config, Vector2 launchVelocity, CorpseKind kind = CorpseKind.Normal)
        {
            cfg = config;
            Kind = kind;
            rb = GetComponent<Rigidbody>();
            col = GetComponent<BoxCollider>();
            meshRenderer = GetComponent<MeshRenderer>();
            if (visualSquash == null) visualSquash = GetComponentInChildren<SpringSquash>();
            // Face the way the cat was moving at death. OnEnable doesn't reset FacingSign, so this
            // sticks across pooled reuse; SpringSquash.LateUpdate applies it to the Cat's scale.x.
            if (visualSquash != null) visualSquash.FacingSign = PlayerController.LastFacingSign;

            // Full reset so a pooled corpse comes back clean, not carrying settled/held state
            // from its previous life.
            Settled = false;
            Held = false;
            stillFor = 0f;
            ridingSurface = null;
            probedCarrier = null;
            probedCarrierBody = null;
            Detach();
            unattachedLocalScale = transform.localScale;
            ResetBob();
            col.enabled = true;
            rb.isKinematic = false; // must precede setting linearVelocity

            rb.mass = cfg.corpseMass;
            rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            // If it still spawns overlapping geometry (tight room, nowhere clear found), cap how
            // hard PhysX can shove it out so it settles nearby instead of rocketing across the level.
            rb.maxDepenetrationVelocity = 3f;
            rb.angularVelocity = Vector3.zero;
            rb.linearVelocity = new Vector3(launchVelocity.x, launchVelocity.y, 0f);

            meshRenderer.sharedMaterial = Kind == CorpseKind.Trampoline ? trampolineMat : normalMat;

            if (catTint == null) catTint = GetComponentInChildren<CatTint>(true);
            if (catTint != null)
            {
                catTint.gameplayTint = cfg.trampolineTint;
                catTint.gameplayStrength = Kind == CorpseKind.Trampoline ? cfg.trampolineTintStrength : 0f;
            }
        }

        void FixedUpdate()
        {
            if (rb == null || Held) return;
            // Attached: the transform hierarchy moves us. Nothing to track, nothing to settle.
            if (carrier != null) return;

            RideSurface();
            if (Settled) return;

            // Keep it pinned to the play plane no matter what.
            var p = transform.position;
            if (Mathf.Abs(p.z) > 0.001f) transform.position = new Vector3(p.x, p.y, 0f);

            // While still loose on a physics-driven surface (a swinging cage) PhysX friction drags
            // this body along, so "still" has to mean still *relative to that surface* — measured
            // absolutely it would never settle, so it would never attach either.
            Vector3 reference = probedCarrierBody != null ? probedCarrierBody.GetPointVelocity(transform.position) : Vector3.zero;
            if ((rb.linearVelocity - reference).sqrMagnitude < 0.35f)
            {
                stillFor += Time.fixedDeltaTime;
                if (stillFor >= cfg.corpseSettleTime) Freeze();
            }
            else stillFor = 0f;
        }

        /// A MovingPlatform has no Rigidbody, so PhysX never imparts its velocity onto things
        /// resting on it - without this, a corpse (still falling/settling, or settled somewhere the
        /// attach probe found nothing) just gets shoved by depenetration instead of carried, and can
        /// end up wedged in the platform. Carries via rb.position so it works whether the rigidbody
        /// is kinematic or still physics-active. The surface ridden can be a platform or another
        /// corpse, so stacked corpses chain their rides transitively.
        void RideSurface()
        {
            if (ridingSurface != null)
            {
                Vector3 delta = ridingSurface.position - ridingSurfaceLastPos;
                if (delta.sqrMagnitude > 0f) rb.position += delta;
            }

            Probe();
            if (ridingSurface != null) ridingSurfaceLastPos = ridingSurface.position;
        }

        /// Short ray under the body: fills what we're resting on (`ridingSurface`), what we'd parent
        /// onto once settled (`probedCarrier`), and that carrier's rigidbody if it has one
        /// (`probedCarrierBody`, used only as the stillness reference above).
        void Probe()
        {
            ridingSurface = null;
            probedCarrier = null;
            probedCarrierBody = null;

            float halfHeight = col.size.y * 0.5f * transform.lossyScale.y;
            Vector3 origin = transform.position - Vector3.up * (halfHeight - 0.05f);
            if (!Physics.Raycast(origin, Vector3.down, out var hit, 0.15f, ~0, QueryTriggerInteraction.Ignore))
                return;

            var hanging = hit.collider.GetComponentInParent<HangingPhysicsObject>();
            if (hanging != null)
            {
                // Leave ridingSurface null: it's a real rigidbody, so PhysX friction is already
                // carrying us and hand-carrying on top of that would double the motion.
                probedCarrier = hanging.transform;
                probedCarrierBody = hanging.GetComponent<Rigidbody>();
                return;
            }

            var platform = hit.collider.GetComponentInParent<MovingPlatform>();
            if (platform != null) probedCarrier = platform.transform;
            else
            {
                var mover = hit.collider.GetComponentInParent<LinkedMover>();
                if (mover != null) probedCarrier = mover.transform;
            }

            var below = hit.collider.GetComponentInParent<Corpse>();
            if (below == this) below = null;
            // Stacking: only ride the pile up onto a carrier if the body under us is already on one,
            // otherwise a corpse resting on plain ground would pointlessly own its neighbours.
            if (probedCarrier == null && below != null && below.AttachedToCarrier) probedCarrier = below.transform;

            ridingSurface = probedCarrier != null ? probedCarrier : below != null ? below.transform : null;
        }

        void Freeze()
        {
            Settled = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            meshRenderer.sharedMaterial = Kind == CorpseKind.Trampoline ? trampolineSettledMat : normalSettledMat;
            // Re-probe rather than trust cached state: PutDown() freezes without a physics step
            // having run since the collider came back on.
            Probe();
            if (probedCarrier != null) Attach(probedCarrier);
        }

        /// Ride a moving surface by becoming its child — a moving platform, a lift, a swinging cage,
        /// or a corpse already on one. Purely a transform relationship: the body is kinematic and
        /// contact with the carrier is switched off, so it has no physics influence on what it rides.
        void Attach(Transform target)
        {
            carrier = target;
            ridingSurface = null;

            // A kinematic body is infinitely heavy, so any contact with the carrier can only resolve
            // by shoving the carrier — which on a hanging cage means a corpse sitting on it kicks it
            // around forever. Glued to it by the hierarchy, that contact has nothing left to solve,
            // so drop the pair. Walk the whole chain up: a corpse riding a corpse riding a cage still
            // needs to ignore the cage.
            for (var t = target; t != null; t = t.parent)
            {
                var body = t.GetComponent<Rigidbody>();
                if (body == null || body.isKinematic) continue;
                foreach (var other in body.GetComponentsInChildren<Collider>())
                {
                    if (other == col || other == null) continue;
                    Physics.IgnoreCollision(col, other, true);
                    ignored.Add(other);
                }
            }

            // Interpolation would smooth this body toward its own (now unchanging) physics pose and
            // fight the pose the hierarchy gives it.
            rb.interpolation = RigidbodyInterpolation.None;
            transform.SetParent(target, true);
            attachLocalPos = transform.localPosition;
            attachLocalRot = transform.localRotation;
        }

        /// Back under the corpse pool root with the authored scale/rotation restored, and colliding
        /// with the world normally again. Anything attached on top of us comes off too — its support
        /// is leaving. Safe to call on a corpse that was never attached, and before Init has ever run.
        public void Detach()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var rider = transform.GetChild(i).GetComponent<Corpse>();
                if (rider != null) rider.Detach();
            }

            if (carrier == null) return;
            carrier = null;
            foreach (var other in ignored)
                if (other != null && col != null) Physics.IgnoreCollision(col, other, false);
            ignored.Clear();
            transform.SetParent(homeParent, true);
            transform.localScale = unattachedLocalScale;
            transform.rotation = Quaternion.identity;
            if (rb != null) rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        /// Picked up by the carry ability: goes kinematic and stops colliding until dropped/thrown.
        public void PickUp()
        {
            Held = true;
            Settled = false;
            stillFor = 0f;
            Detach();
            ResetBob();
            rb.isKinematic = true;
            GetComponent<BoxCollider>().enabled = false;
        }

        public void SetHeldPosition(Vector3 pos) => transform.position = pos;

        /// Soft-body reaction when the player lands/bounces on this body: the art squashes
        /// (visual only) and the whole body dips and springs back (moves the transform, so a
        /// cat riding this surface follows the dip).
        public void Jiggle()
        {
            visualSquash?.Jiggle();
            bobVel -= bobKick;
        }

        /// Vertical dip/spring, applied as an additive Y offset on top of whatever the physics /
        /// settle / ride logic set this frame — remove last frame's offset, add this frame's — so
        /// it never fights the base position. Runs after FixedUpdate; a settled corpse is kinematic
        /// so nothing else writes position.y here.
        void LateUpdate()
        {
            if (carrier != null)
            {
                // Re-assert the pose we attached with. Being a rigidbody, this body's transform can
                // still be written from its own (stale) physics pose, which under a carrier that
                // physics itself moves would cancel out the hierarchy's motion. Setting the local
                // pose every frame makes the attachment rigid whatever else touched the transform.
                transform.localPosition = attachLocalPos;
                transform.localRotation = attachLocalRot;
                bobApplied = 0f; // base pose is re-asserted every frame, so there's nothing to undo
            }

            float dt = Time.deltaTime;
            if (dt > 0f)
            {
                bobVel += (-bobStiffness * bobValue - bobDamping * bobVel) * dt;
                bobValue += bobVel * dt;
                bobValue = Mathf.Clamp(bobValue, -bobMaxDepth, bobMaxDepth);
            }
            float delta = bobValue - bobApplied;
            if (delta != 0f) transform.position += Vector3.up * delta;
            bobApplied = bobValue;
        }

        void ResetBob()
        {
            if (bobApplied != 0f) transform.position -= Vector3.up * bobApplied;
            bobValue = bobVel = bobApplied = 0f;
        }

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
            Detach();
            ResetBob();
            GetComponent<BoxCollider>().enabled = true;
            rb.isKinematic = false;
            rb.linearVelocity = velocity;
        }
    }
}
