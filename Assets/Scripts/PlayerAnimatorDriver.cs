using UnityEngine;

namespace NineLives
{
    /// Drives the placeholder PlayerAnimator from PlayerController state + game events. Sets the
    /// continuous params (Speed / Grounded / Charging / Falling) every frame and fires triggers
    /// for one-shot transitions (jumps, land, death, level flow). Keeps GameManager and the
    /// gameplay scripts unaware that an Animator exists — they only raise GameEvents.
    ///
    /// The Animator's states currently hold empty placeholder clips: the state machine runs and
    /// transitions fire (watch it in the Animator window), but nothing moves the mesh yet, so it
    /// doesn't fight PlayerController's own scale/facing. When real clips are authored, wire their
    /// animation events to the AnimEvent_* methods here to drive VFX/SFX off the animation itself.
    public class PlayerAnimatorDriver : MonoBehaviour
    {
        [Tooltip("Animator on the cat art child. Assign in the prefab; falls back to any Animator in children.")]
        [SerializeField] Animator anim;

        [Tooltip("Seconds standing still before the secondary idle (lick/stretch) plays.")]
        [SerializeField] float idle2Delay = 4f;

        static readonly int pSpeed = Animator.StringToHash("Speed");
        static readonly int pGrounded = Animator.StringToHash("Grounded");
        static readonly int pCharging = Animator.StringToHash("Charging");
        static readonly int pFalling = Animator.StringToHash("Falling");
        static readonly int tJump = Animator.StringToHash("Jump");
        static readonly int tJumpCharged = Animator.StringToHash("JumpCharged");
        static readonly int tLand = Animator.StringToHash("Land");
        static readonly int tHardLand = Animator.StringToHash("HardLand");
        static readonly int tIdle2 = Animator.StringToHash("Idle2");
        static readonly int tDie = Animator.StringToHash("Die");
        static readonly int tHit = Animator.StringToHash("Hit");
        static readonly int tLevelEnter = Animator.StringToHash("LevelEnter");
        static readonly int tLevelExit = Animator.StringToHash("LevelExit");
        static readonly int tThrow = Animator.StringToHash("Throw");
        static readonly int tChargeThrow = Animator.StringToHash("ChargeThrow");

        PlayerController player;
        float idleTimer;
        bool idle2Fired;

        void Awake()
        {
            if (anim == null) anim = GetComponentInChildren<Animator>();
            player = GetComponent<PlayerController>();
        }

        void OnEnable()
        {
            GameEvents.Jumped += OnJumped;
            GameEvents.Landed += OnLanded;
            GameEvents.HardLanded += OnHardLanded;
            GameEvents.SacrificeDeath += OnDeath;
            GameEvents.PoofDeath += OnDeath;
            GameEvents.TrapHit += OnTrapHit;
            GameEvents.LevelEntered += OnLevelEntered;
            GameEvents.LevelExited += OnLevelExited;
            GameEvents.ChargeStarted += OnChargeThrow;
            GameEvents.Thrown += OnThrown;
        }

        void OnDisable()
        {
            GameEvents.Jumped -= OnJumped;
            GameEvents.Landed -= OnLanded;
            GameEvents.HardLanded -= OnHardLanded;
            GameEvents.SacrificeDeath -= OnDeath;
            GameEvents.PoofDeath -= OnDeath;
            GameEvents.TrapHit -= OnTrapHit;
            GameEvents.LevelEntered -= OnLevelEntered;
            GameEvents.LevelExited -= OnLevelExited;
            GameEvents.ChargeStarted -= OnChargeThrow;
            GameEvents.Thrown -= OnThrown;
        }

        void Update()
        {
            if (anim == null || player == null) return;
            // While dying, hold whatever death/hit state we're in: the live params still describe
            // the last living frame (Falling, mid-run Speed) and would transition straight back out.
            if (player.IsDying) return;

            float speed = Mathf.Abs(player.Velocity.x);
            bool grounded = player.Grounded;
            bool charging = player.Charging;
            bool falling = !grounded && player.Velocity.y < -0.01f;

            anim.SetFloat(pSpeed, speed);
            anim.SetBool(pGrounded, grounded);
            anim.SetBool(pCharging, charging);
            anim.SetBool(pFalling, falling);

            // Land is only consumed by the Falling state, so one set on a landing the animator
            // wasn't in Falling for (quick hop, buffered jump straight off a landing) would sit
            // pending and yank us out of the *next* fall mid-air. Landed only ever fires on a
            // grounded frame, so clearing it while airborne can't eat a real one.
            if (!grounded) anim.ResetTrigger(tLand);

            // Secondary idle after standing still long enough; reset the moment anything happens.
            bool idle = grounded && !charging && speed < 0.15f;
            if (idle)
            {
                idleTimer += Time.deltaTime;
                if (!idle2Fired && idleTimer >= idle2Delay) { anim.SetTrigger(tIdle2); idle2Fired = true; }
            }
            else { idleTimer = 0f; idle2Fired = false; }
        }

        void OnJumped(Vector3 _)
        {
            anim.SetTrigger(player.ChargedJumpThisStep ? tJumpCharged : tJump);
            idleTimer = 0f; idle2Fired = false;
        }

        void OnLanded(Vector3 _) => anim.SetTrigger(tLand);
        void OnHardLanded(Vector3 _) => anim.SetTrigger(tHardLand);
        /// Neutralise the params before the trigger so the death state can't be immediately
        /// exited by a stale Falling/Speed left over from the frame the cat died on.
        void OnDeath(Vector3 _)
        {
            anim.SetFloat(pSpeed, 0f);
            anim.SetBool(pGrounded, true);
            anim.SetBool(pCharging, false);
            anim.SetBool(pFalling, false);
            anim.SetTrigger(tDie);
        }

        void OnTrapHit(Vector3 _) => anim.SetTrigger(tHit);
        void OnLevelEntered(Vector3 _) => anim.SetTrigger(tLevelEnter);
        void OnLevelExited(Vector3 _) => anim.SetTrigger(tLevelExit);
        void OnChargeThrow(Vector3 _) => anim.SetTrigger(tChargeThrow);
        void OnThrown(Vector3 p, Vector3 d) => anim.SetTrigger(tThrow);

        // --- Animation-event hooks -------------------------------------------------------------
        // Wire these to animation events on the real clips later (e.g. plant a footstep exactly on
        // the paw-down frame). Until then PlayerController raises the same events off physics.
        public void AnimEvent_Footstep() => GameEvents.RaiseFootstep(player.FeetPosition);
        public void AnimEvent_Land() => GameEvents.RaiseLanded(player.FeetPosition);
        public void AnimEvent_Jump() => GameEvents.RaiseJumped(player.FeetPosition);
    }
}
