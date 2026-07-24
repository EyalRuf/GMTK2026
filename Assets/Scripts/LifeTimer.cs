using UnityEngine;

namespace NineLives
{
    /// A countdown, paused/resumed across respawn grace periods without losing its remaining time.
    public class LifeTimer
    {
        public float Duration { get; private set; }
        public float Remaining { get; private set; }
        public bool Running { get; private set; }

        public float Normalized => Duration <= 0f ? 0f : Remaining / Duration;

        public void Restart(float duration)
        {
            Duration = duration;
            Remaining = duration;
            Running = true;
        }

        public void Stop() { Running = false; }

        /// Resumes ticking from the current Remaining, e.g. after a respawn grace period.
        public void Resume() { Running = true; }

        /// Snaps Remaining down (never up) — used to drop the rest of the current soul's
        /// slot immediately when the player manually sacrifices mid-interval.
        public void SetRemaining(float remaining) { Remaining = Mathf.Clamp(remaining, 0f, Remaining); }

        /// Returns true on the tick the timer hits zero.
        public bool Tick(float dt)
        {
            if (!Running) return false;
            Remaining -= dt;
            if (Remaining > 0f) return false;
            Remaining = 0f;
            Running = false;
            return true;
        }
    }
}
