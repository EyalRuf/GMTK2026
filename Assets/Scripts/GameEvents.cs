using System;
using UnityEngine;

namespace NineLives
{
    /// The decoupling boundary between gameplay and presentation (VFX / SFX / animation).
    /// Gameplay code *raises* these; FXManager and PlayerAnimatorDriver *listen*. Nothing in
    /// gameplay logic references a particle, clip or AudioSource directly — swap the listeners
    /// or the assets they hold and the game code never changes.
    ///
    /// All payloads are a world position (where the effect should play). A couple carry a
    /// direction. Placeholder/hook events (ChargeStarted, Thrown) are raised nowhere yet — the
    /// throw mechanic isn't built — but listeners are wired so it's a one-line raise later.
    public static class GameEvents
    {
        // --- Movement ---
        public static event Action<Vector3> Footstep;      // cadence tick while walking
        public static event Action<Vector3> Jumped;        // quick / charged launch
        public static event Action<Vector3> Landed;        // normal landing
        public static event Action<Vector3> HardLanded;    // landing from a big fall

        // --- Throw (hooks only — mechanic not implemented) ---
        public static event Action<Vector3> ChargeStarted; // began holding to throw
        public static event Action<Vector3, Vector3> Thrown; // release: pos + direction

        // --- Death ---
        public static event Action<Vector3> SacrificeDeath; // recoverable death: soul leaves body
        public static event Action<Vector3> CorpseSpawned;  // corpse form materialises
        public static event Action<Vector3> PoofDeath;      // environmental death: poof + respawn

        // --- Level flow ---
        public static event Action<Vector3> LevelEntered;   // spawn / respawn at level start
        public static event Action<Vector3> LevelExited;    // reached the exit pad

        public static void RaiseFootstep(Vector3 p) => Footstep?.Invoke(p);
        public static void RaiseJumped(Vector3 p) => Jumped?.Invoke(p);
        public static void RaiseLanded(Vector3 p) => Landed?.Invoke(p);
        public static void RaiseHardLanded(Vector3 p) => HardLanded?.Invoke(p);
        public static void RaiseChargeStarted(Vector3 p) => ChargeStarted?.Invoke(p);
        public static void RaiseThrown(Vector3 p, Vector3 dir) => Thrown?.Invoke(p, dir);
        public static void RaiseSacrificeDeath(Vector3 p) => SacrificeDeath?.Invoke(p);
        public static void RaiseCorpseSpawned(Vector3 p) => CorpseSpawned?.Invoke(p);
        public static void RaisePoofDeath(Vector3 p) => PoofDeath?.Invoke(p);
        public static void RaiseLevelEntered(Vector3 p) => LevelEntered?.Invoke(p);
        public static void RaiseLevelExited(Vector3 p) => LevelExited?.Invoke(p);
    }
}
