using System;
using System.Collections;
using UnityEngine;

namespace NineLives
{
    /// Thin MonoBehaviour: reads a MotorInput, drives a CharacterController with the
    /// PlatformerMotor's velocity, and handles corpse-bounce. No game-flow logic here.
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        GameConfig cfg;
        CharacterController cc;
        PlatformerMotor motor;
        Transform mesh;

        [Tooltip("Cat art/sprite root to flip on the X axis when facing changes. Assign in the prefab.")]
        [SerializeField] Transform catSprite;

        bool wasGrounded;
        Transform ridingSurface;
        Vector3 ridingSurfaceLastPos;
        float airborneApexY;
        float hardLandingTimer;
        float footstepTimer;
        const float FootstepInterval = 0.26f;
        public bool Grounded { get; private set; }
        public Vector2 Velocity => motor.Velocity;
        public Vector3 FeetPosition => transform.position;
        public bool JumpedThisStep { get; private set; }
        public bool BouncedThisStep { get; private set; }
        public bool LandedThisStep { get; private set; }
        public bool HardLandedThisStep { get; private set; }
        public bool IsHardLanding => hardLandingTimer > 0f;
        public bool Charging => motor.Charging;
        public bool ChargedJumpThisStep { get; private set; }
        public float SpeedMultiplier = 1f;
        public float JumpMultiplier = 1f;

        [Tooltip("Effective mass used to push HangingPhysicsObjects (cages, swinging traps) on contact.")]
        [SerializeField] float pushMass = 5f;

        /// True from the moment an instant-kill hit lands until the death sequence hands off to
        /// GameManager (which deactivates this object for the respawn). Guards against a second
        /// trap re-triggering mid-sequence and tells GameManager to stop feeding real input.
        public bool IsDying { get; private set; }
        /// Raised once the hit's control-lock has elapsed; GameManager subscribes and continues
        /// the same respawn flow used by falling off the map.
        public event Action<DeathInfo> DeathSequenceReady;
        static AudioClip sDefaultHitClip;
        static readonly int pBaseColor = Shader.PropertyToID("_BaseColor");

        Renderer[] flashRenderers;
        Color[] flashBaseColors;
        MaterialPropertyBlock flashBlock;
        Coroutine flashRoutine;

        public void Configure(GameConfig config)
        {
            cfg = config;
            motor = new PlatformerMotor(cfg);

            cc = GetComponent<CharacterController>();
            cc.height = cfg.playerHeight;
            cc.radius = cfg.playerRadius;
            cc.center = Vector3.up * (cfg.playerHeight * 0.5f);
            cc.skinWidth = 0.02f;
            cc.minMoveDistance = 0f;

            mesh = transform.Find("Cat");

            flashRenderers = mesh.GetComponentsInChildren<Renderer>(true);
            flashBaseColors = new Color[flashRenderers.Length];
            for (int i = 0; i < flashRenderers.Length; i++)
                //flashBaseColors[i] = flashRenderers[i].sharedMaterial.GetColor(pBaseColor);
            flashBlock = new MaterialPropertyBlock();
        }

        public void Spawn(Vector3 feet)
        {
            cc.enabled = false;
            transform.position = new Vector3(feet.x, feet.y, 0f);
            cc.enabled = true;
            motor.Reset();
            wasGrounded = false;
            airborneApexY = feet.y;
            hardLandingTimer = 0f;
            IsDying = false;
            ResetHitFlash();
            gameObject.SetActive(true);
        }

        /// Reusable instant-kill entry point: any hazard (DeathTrap today, future instant-kill
        /// mechanics later) notifies the player here instead of touching game-flow state directly.
        /// Applies knockback immediately (the existing motor's gravity/deceleration carries it
        /// through naturally), fires the hit reaction, then hands off to GameManager once the
        /// control lock elapses so it can continue the exact same respawn flow as falling off the
        /// map — the player dies in place instead of falling first.
        public void Die(DeathInfo info)
        {
            if (IsDying) return;
            IsDying = true;

            motor.Velocity = info.KnockbackVelocity;
            GameEvents.RaiseTrapHit(FeetPosition);
            PlayHitFeedback(info);
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(HitFlash());

            StartCoroutine(FinishDeathSequence(info));
        }

        IEnumerator HitFlash()
        {
            float t = 0f;
            while (t < cfg.hitFlashDuration)
            {
                t += Time.deltaTime;
                float k = 1f - Mathf.Clamp01(t / cfg.hitFlashDuration);
                for (int i = 0; i < flashRenderers.Length; i++)
                {
                    flashBlock.SetColor(pBaseColor, Color.Lerp(flashBaseColors[i], cfg.hitFlashColor, k));
                    flashRenderers[i].SetPropertyBlock(flashBlock);
                }
                yield return null;
            }
            ResetHitFlash();
            flashRoutine = null;
        }

        void ResetHitFlash()
        {
            if (flashRenderers == null) return;
            flashBlock.Clear();
            for (int i = 0; i < flashRenderers.Length; i++)
                flashRenderers[i].SetPropertyBlock(flashBlock);
        }

        void PlayHitFeedback(DeathInfo info)
        {
            var clip = info.HitSfxOverride != null ? info.HitSfxOverride : DefaultHitClip;
            AudioSource.PlayClipAtPoint(clip, FeetPosition);
            if (info.HitVfxOverride != null)
                Destroy(Instantiate(info.HitVfxOverride, FeetPosition, Quaternion.identity), 3f);
        }

        static AudioClip DefaultHitClip => sDefaultHitClip != null ? sDefaultHitClip : (sDefaultHitClip = ProceduralAudio.Hit());

        IEnumerator FinishDeathSequence(DeathInfo info)
        {
            yield return new WaitForSeconds(info.ControlLockDuration);
            DeathSequenceReady?.Invoke(info);
        }

        public void Tick(MotorInput input, float dt)
        {
            JumpedThisStep = BouncedThisStep = LandedThisStep = HardLandedThisStep = false;

            float hardLandingMultiplier = 1f;
            if (hardLandingTimer > 0f)
            {
                hardLandingTimer -= dt;
                float recoverT = Mathf.Clamp01(hardLandingTimer / cfg.hardLandingRecoveryTime);
                hardLandingMultiplier = Mathf.Lerp(1f, cfg.hardLandingSpeedMultiplier, recoverT);
            }

            input.SpeedMultiplier = SpeedMultiplier * hardLandingMultiplier;
            input.JumpMultiplier = JumpMultiplier;

            // Vertical/depth carry applies as a straight position offset - only horizontal carry
            // gets folded into the speed clamp below, so riding a platform can't stack with your
            // own walk speed to exceed maxSpeed.
            float platformVelX = 0f;
            if (ridingSurface != null)
            {
                Vector3 platformDelta = ridingSurface.position - ridingSurfaceLastPos;
                platformVelX = dt > 0f ? platformDelta.x / dt : 0f;
                Vector3 verticalDelta = new Vector3(0f, platformDelta.y, platformDelta.z);
                if (verticalDelta.sqrMagnitude > 0f) cc.Move(verticalDelta);
            }

            bool grounded = Probe(out bool onTrampoline, out Transform surface);
            ridingSurface = grounded ? surface : null;
            if (ridingSurface != null) ridingSurfaceLastPos = ridingSurface.position;
            float impactVy = motor.Velocity.y;

            bool justLanded = grounded && !wasGrounded && impactVy < 0f;
            bool bounce = justLanded && onTrampoline && impactVy < -cfg.trampolineBounceThreshold;

            if (!wasGrounded) airborneApexY = Mathf.Max(airborneApexY, transform.position.y);

            motor.Tick(dt, input, grounded && !bounce);

            if (bounce)
            {
                float mult = input.JumpHeld ? cfg.trampolineHoldBounceMultiplier : 1f;
                float up = Mathf.Min(-impactVy * cfg.trampolineBounciness * mult, cfg.trampolineMaxBounce);
                motor.Bounce(up);
                BouncedThisStep = true;
            }
            else if (justLanded && impactVy < -3f)
            {
                LandedThisStep = true;
            }

            if (justLanded && !bounce)
            {
                float fallHeight = airborneApexY - transform.position.y;
                if (fallHeight >= cfg.hardLandingMinFallHeight)
                {
                    hardLandingTimer = cfg.hardLandingRecoveryTime;
                    HardLandedThisStep = true;
                }
            }
            if (grounded) airborneApexY = transform.position.y;

            JumpedThisStep = motor.JumpedThisStep;
            ChargedJumpThisStep = motor.JumpedThisStep && motor.JumpWasCharged;
            Grounded = motor.Grounded;

            // Raise presentation events at the decoupling boundary — FXManager / the animator
            // driver listen; nothing here knows about particles, clips or sounds.
            if (JumpedThisStep) GameEvents.RaiseJumped(FeetPosition);
            if (HardLandedThisStep) GameEvents.RaiseHardLanded(FeetPosition);
            else if (LandedThisStep) GameEvents.RaiseLanded(FeetPosition);
            TickFootsteps(dt);

            float combinedX = Mathf.Clamp(platformVelX + motor.Velocity.x, -cfg.maxSpeed, cfg.maxSpeed);
            var v = new Vector3(combinedX, motor.Velocity.y, 0f);
            cc.Move(v * dt);

            // stay pinned to z=0
            if (Mathf.Abs(transform.position.z) > 0.0001f)
            {
                var p = transform.position; p.z = 0f;
                cc.enabled = false; transform.position = p; cc.enabled = true;
            }

            if (catSprite != null && Mathf.Abs(motor.Velocity.x) > 0.15f)
                catSprite.localScale = new Vector3(
                    Mathf.Sign(motor.Velocity.x) * Mathf.Abs(catSprite.localScale.x),
                    catSprite.localScale.y, catSprite.localScale.z);

            wasGrounded = grounded;
        }

        /// Standard CharacterController-pushes-Rigidbody hook. HangingPhysicsObject doesn't know
        /// about the player at all — it just receives an impulse and reacts.
        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            var body = hit.collider.attachedRigidbody;
            if (body == null || body.isKinematic) return;
            var hanging = body.GetComponent<HangingPhysicsObject>();
            if (hanging == null) return;

            Vector3 velocity = new Vector3(motor.Velocity.x, motor.Velocity.y, 0f);
            hanging.ApplyImpact(hit.point, hit.moveDirection.normalized * velocity.magnitude * pushMass);
        }

        void TickFootsteps(float dt)
        {
            if (Grounded && Mathf.Abs(motor.Velocity.x) > 1f)
            {
                footstepTimer -= dt;
                if (footstepTimer <= 0f)
                {
                    GameEvents.RaiseFootstep(FeetPosition);
                    footstepTimer = FootstepInterval;
                }
            }
            else footstepTimer = 0f;
        }

        /// Ridable surface can be a MovingPlatform directly underfoot, or a Corpse standing on
        /// one (chains through Corpse's own ridingSurface the same way stacked corpses do).
        bool Probe(out bool onTrampoline, out Transform surface)
        {
            onTrampoline = false;
            surface = null;
            float r = cfg.playerRadius * 0.92f;
            Vector3 origin = transform.position + Vector3.up * (cfg.playerRadius + 0.02f);
            float dist = cfg.playerRadius + cfg.groundProbeDepth;

            var hits = Physics.SphereCastAll(origin, r, Vector3.down, dist, ~0, QueryTriggerInteraction.Ignore);
            bool grounded = false;
            foreach (var h in hits)
            {
                if (h.collider.transform.IsChildOf(transform)) continue;
                if (h.collider.transform == transform) continue;
                grounded = true;
                var corpse = h.collider.GetComponentInParent<Corpse>();
                if (corpse != null && corpse.Kind == CorpseKind.Trampoline) onTrampoline = true;
                if (surface == null)
                {
                    var platform = h.collider.GetComponentInParent<MovingPlatform>();
                    var hanging = h.collider.GetComponentInParent<HangingPhysicsObject>();
                    surface = platform != null ? platform.transform : corpse != null ? corpse.transform : hanging != null ? hanging.transform : null;
                }
            }
            return grounded && motor.Velocity.y <= 0.5f;
        }
    }
}
