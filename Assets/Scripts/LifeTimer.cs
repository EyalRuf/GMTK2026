namespace NineLives
{
    /// The death countdown for a single life.
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
