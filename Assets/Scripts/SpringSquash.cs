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
        [Header("Spring feel")]
        [Tooltip("Higher = snaps back faster.")]
        public float stiffness = 220f;
        [Tooltip("Higher = settles with fewer wobbles.")]
        public float damping = 14f;
        [Tooltip("Max squash/stretch before amplitude scaling.")]
        public float maxSquash = 0.35f;
        [Tooltip("How much width shrinks as height stretches (fakes volume preservation).")]
        public float poisson = 0.6f;

        [Header("Live cat reactions (ignored on a corpse)")]
        [Tooltip("Stretch kick when the player jumps.")]
        public float jumpKick = 0.5f;
        [Tooltip("Squash kick on a normal landing.")]
        public float landKick = -0.6f;
        [Tooltip("Squash kick on a hard landing.")]
        public float hardLandKick = -1.1f;
        [Tooltip("Squash amount while the Rubber pickup is armed. The base cat is rigid (0).")]
        public float rubberAmplitude = 2.5f;

        [Header("Corpse reaction (ignored on the live cat)")]
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
            transform.localScale = new Vector3(
                baseScale.x * (1f - s * poisson) * FacingSign,
                baseScale.y * (1f + s),
                baseScale.z);
        }
    }
}
