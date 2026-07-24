using UnityEngine;

namespace NineLives
{
    /// Data describing an instant-kill hit: the knockback to apply, how long control stays locked
    /// for the hit-reaction before the death sequence continues, and optional per-source SFX/VFX
    /// overrides. Built by whatever notifies the player (DeathTrap today); any future instant-kill
    /// mechanic reuses the same PlayerController.Die(DeathInfo) entry point.
    public class DeathInfo
    {
        public Vector2 KnockbackVelocity;
        public float ControlLockDuration = 0.5f;
        public AudioClip HitSfxOverride;
        public GameObject HitVfxOverride;
    }
}
