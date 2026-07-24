using UnityEngine;

namespace NineLives
{
    /// Drop this on any hazard (spikes, lava, saw, poison...) to make it an instant-kill trap.
    /// Detects the player, knocks them back per the configured params, and hands off to
    /// PlayerController.Die() — the same reusable death entry point any instant-kill source uses.
    /// Works on either a trigger collider or a solid one; add whichever the hazard's mesh needs.
    public class DeathTrap : MonoBehaviour
    {
        [Header("Knockback")]
        [Tooltip("Push the player away from this trap's position. Turn off to always use the configured direction below (e.g. a saw that always launches you the same way).")]
        [SerializeField] bool knockbackAwayFromTrap = true;
        [Tooltip("Direction used when 'away from trap' is off, or for its vertical component either way.")]
        [SerializeField] Vector2 knockbackDirection = Vector2.left;
        [SerializeField] float knockbackForce = 6f;
        [SerializeField] float verticalLift = 4f;
        [Tooltip("Seconds the player is locked out of control (knockback + hit reaction) before the death sequence continues.")]
        [SerializeField] float controlLockDuration = 0.5f;

        [Header("Overrides (optional)")]
        [Tooltip("Custom impact sound for this trap. Leave empty to use the default hit sound.")]
        [SerializeField] AudioClip hitSfxOverride;
        [Tooltip("Custom impact VFX prefab for this trap. Leave empty for none.")]
        [SerializeField] GameObject hitVfxOverride;

        void OnTriggerEnter(Collider other) => TryKill(other);
        void OnCollisionEnter(Collision collision) => TryKill(collision.collider);

        void TryKill(Collider other)
        {
            var player = other.GetComponentInParent<PlayerController>();
            if (player == null || player.IsDying) return;

            var dir = knockbackDirection.sqrMagnitude > 0.0001f ? knockbackDirection.normalized : Vector2.left;
            if (knockbackAwayFromTrap)
            {
                float sign = Mathf.Sign(player.FeetPosition.x - transform.position.x);
                dir.x = sign == 0f ? -1f : sign;
            }

            var info = new DeathInfo
            {
                KnockbackVelocity = new Vector2(dir.x * knockbackForce, Mathf.Max(dir.y, 0f) * knockbackForce + verticalLift),
                ControlLockDuration = controlLockDuration,
                HitSfxOverride = hitSfxOverride,
                HitVfxOverride = hitVfxOverride,
            };
            player.Die(info);
        }
    }
}
