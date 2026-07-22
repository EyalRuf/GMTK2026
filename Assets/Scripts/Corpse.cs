using UnityEngine;

namespace NineLives
{
    /// A dropped life. Falls as a physics box, then freezes into a solid,
    /// slightly bouncy platform once it has come to rest.
    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    public class Corpse : MonoBehaviour
    {
        GameConfig cfg;
        Rigidbody rb;
        float stillFor;
        public bool Settled { get; private set; }

        public void Init(GameConfig config, Vector2 launchVelocity)
        {
            cfg = config;
            rb = GetComponent<Rigidbody>();
            rb.mass = cfg.corpseMass;
            rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.linearVelocity = new Vector3(launchVelocity.x, launchVelocity.y, 0f);
        }

        void FixedUpdate()
        {
            if (Settled || rb == null) return;

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

        void Freeze()
        {
            Settled = true;
            rb.linearVelocity = Vector3.zero;
            rb.isKinematic = true;
            var r = GetComponent<MeshRenderer>();
            if (r) r.sharedMaterial = GreyboxFactory.Make(GreyboxFactory.CorpseCol * 0.85f, 0.15f);
        }
    }
}
