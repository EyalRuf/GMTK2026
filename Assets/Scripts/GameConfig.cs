using UnityEngine;

namespace NineLives
{
    /// All tunable numbers. Edit at runtime in play mode and the changes stick.
    [CreateAssetMenu(menuName = "NineLives/Game Config", fileName = "GameConfig")]
    public class GameConfig : ScriptableObject
    {
        [Header("Run")]
        public float maxSpeed = 8f;
        public float groundAcceleration = 90f;
        public float groundDeceleration = 110f;
        public float airAcceleration = 55f;
        public float airDeceleration = 25f;
        [Tooltip("Accel multiplier while reversing direction. Makes turns feel snappy.")]
        public float turnBoost = 2f;

        [Header("Jump")]
        [Tooltip("Peak height in world units of a full-held jump from flat ground.")]
        public float jumpHeight = 3.5f;
        [Tooltip("Seconds from leaving the ground to the top of the arc. Lower = heavier, punchier.")]
        public float timeToApex = 0.4f;
        [Tooltip("Gravity multiplier while falling. >1 makes the fall snappier than the rise.")]
        public float fallGravityMultiplier = 1.8f;
        [Tooltip("Gravity multiplier while rising with jump released. Gives variable jump height.")]
        public float jumpCutGravityMultiplier = 3.2f;
        public float maxFallSpeed = 26f;
        [Tooltip("Grace period after walking off a ledge where jump still works.")]
        public float coyoteTime = 0.12f;
        [Tooltip("How early a jump press is remembered before landing.")]
        public float jumpBuffer = 0.12f;

        [Header("Body")]
        public float playerHeight = 1.2f;
        public float playerRadius = 0.35f;
        public float groundProbeDepth = 0.12f;

        [Header("Corpses")]
        public Vector3 corpseSize = new Vector3(1.05f, 1.2f, 1.2f);
        public float corpseMass = 6f;
        [Tooltip("Fraction of impact speed returned as a bounce.")]
        public float corpseBounciness = 0.55f;
        [Tooltip("Land slower than this and you just stand on the body instead of bouncing.")]
        public float corpseBounceThreshold = 5f;
        public float corpseMaxBounce = 13f;
        [Tooltip("Extra bounce for holding jump as you land. Rewards timing.")]
        public float corpseHoldBounceMultiplier = 1.35f;
        [Tooltip("Bodies freeze solid once they have been still this long. Keeps puzzles predictable.")]
        public float corpseSettleTime = 0.3f;

        [Header("Lives")]
        public int livesPerLevel = 9;
        [Tooltip("Seconds of stillness after respawn before the death timer starts running.")]
        public float respawnGrace = 0.35f;

        [Header("Camera")]
        public Vector3 cameraOffset = new Vector3(0f, 1.5f, -15f);
        public float cameraSmoothing = 0.16f;
        [Tooltip("How far ahead of the player the camera leans, per unit of speed.")]
        public float cameraLookAhead = 0.25f;

        [Header("World")]
        public float killPlaneY = -25f;
        [Tooltip("Z thickness of every greybox block.")]
        public float levelDepth = 3f;

        // Derived — the two numbers the motor actually uses.
        public float JumpGravity => 2f * jumpHeight / (timeToApex * timeToApex);
        public float JumpVelocity => 2f * jumpHeight / timeToApex;
    }
}
