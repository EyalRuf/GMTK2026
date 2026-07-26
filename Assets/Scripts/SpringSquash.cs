using UnityEngine;

namespace NineLives
{
    /// Squash-and-stretch spring. Sole writer of this transform's localScale (runs in LateUpdate,
    /// so it beats the Animator). All feel is tuned here in the Inspector — no code, no GameConfig.
    ///
    /// It auto-detects its role from the hierarchy:
    ///  - On the LIVE player cat (a PlayerController sits above it): rigid until the Rubber pickup
    ///    arms it, and reacts to the player's own jump/landing events. The rubber effect clears by
    ///    itself whenever the cat respawns.
    ///  - On a CORPSE cat (no PlayerController above): always springy, and jiggles when the player
    ///    lands on the body.
    public class SpringSquash : MonoBehaviour
    {
        // === SHARED: affects BOTH the corpse AND the live cat once the Rubber pickup is armed. ===
        // (This is the core spring. On the live cat before the pickup it runs but is invisible,
        //  because the amplitude is 0 — so in practice: the rubbery cat and every corpse.)
        [Header("Spring feel  (corpse + rubber-cat)")]
        [Tooltip("Higher = snaps back faster.")]
        public float stiffness = 220f;
        [Tooltip("Higher = settles with fewer wobbles.")]
        public float damping = 14f;
        [Tooltip("Max squash/stretch before amplitude scaling.")]
        public float maxSquash = 0.35f;
        [Tooltip("How much width shrinks as height stretches (fakes volume preservation).")]
        public float poisson = 0.6f;

        // === LIVE PLAYER CAT ONLY. Completely ignored on a corpse. ===
        // The Jump/Land kicks fire all the time, but you only SEE them once Rubber Amplitude > 0
        // (i.e. after the Rubber pickup). Before the pickup the cat is fully rigid.
        [Header("Live cat only  (visible after Rubber pickup)")]
        [Tooltip("Stretch kick when the player jumps.")]
        public float jumpKick = 0.5f;
        [Tooltip("Squash kick on a normal landing.")]
        public float landKick = -0.6f;
        [Tooltip("Squash kick on a hard landing.")]
        public float hardLandKick = -1.1f;
        [Tooltip("THE POWERUP KNOB. How rubbery the cat gets once Rubber is picked up. " +
                 "0 = rigid (the cat's state before the pickup, and after respawn).")]
        public float rubberAmplitude = 2.5f;

        // === CORPSE ONLY. Ignored on the live cat. The corpse is always springy. ===
        [Header("Corpse only  (jiggle when stepped on)")]
        [Tooltip("Jiggle kick when the player lands on this corpse.")]
        public float jiggleKick = -0.7f;

        Vector3 baseScale;
        float value, vel;
        bool isPlayer;

        /// +1 / -1, applied to X. Driven by PlayerController on the live cat.
        public float FacingSign { get; set; } = 1f;
        /// 0 = rigid. Raised by SetRubber on the player; the corpse keeps it at 1.
        public float Amplitude { get; set; }

        void Awake()
        {
            baseScale = transform.localScale;
            FacingSign = Mathf.Sign(baseScale.x == 0f ? 1f : baseScale.x);
            baseScale.x = Mathf.Abs(baseScale.x);
            isPlayer = GetComponentInParent<PlayerController>() != null;
        }

        void OnEnable()
        {
            // Reset each time this object is (re)activated — respawn clears any armed rubber.
            Amplitude = isPlayer ? 0f : 1f;
            value = vel = 0f;
            if (isPlayer)
            {
                GameEvents.Jumped += OnJumped;
                GameEvents.Landed += OnLanded;
                GameEvents.HardLanded += OnHardLanded;
            }
        }

        void OnDisable()
        {
            if (!isPlayer) return;
            GameEvents.Jumped -= OnJumped;
            GameEvents.Landed -= OnLanded;
            GameEvents.HardLanded -= OnHardLanded;
        }

        void OnJumped(Vector3 _) => Kick(jumpKick);
        void OnLanded(Vector3 _) => Kick(landKick);
        void OnHardLanded(Vector3 _) => Kick(hardLandKick);

        public void Kick(float amount) => vel += amount;

        /// Player landed on this body (corpse only).
        public void Jiggle() => Kick(jiggleKick);

        /// Rubber pickup arms / clears the exaggerated squash on the live cat.
        public void SetRubber(bool on) => Amplitude = on ? rubberAmplitude : 0f;

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            vel += (-stiffness * value - damping * vel) * dt;
            value += vel * dt;
            value = Mathf.Clamp(value, -maxSquash, maxSquash);

            float s = value * Amplitude;
            // Divide the authored base by the parent's world scale so baseScale means the same
            // real (in-world) size no matter how the parent is stretched. On the live cat the
            // parent is unscaled (no-op); on a corpse the parent is stretched to corpseSize, and
            // this cancels it — so the same authored value on both prefabs gives the same size.
            Vector3 p = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
            transform.localScale = new Vector3(
                baseScale.x / p.x * (1f - s * poisson) * FacingSign,
                baseScale.y / p.y * (1f + s),
                baseScale.z / p.z);
        }
    }
}
