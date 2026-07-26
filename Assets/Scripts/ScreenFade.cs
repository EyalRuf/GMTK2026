using UnityEngine;

namespace NineLives
{
    /// Plain full-screen black alpha fade, unscaled time. Distinct from ScreenWipe (which slides a
    /// diagonal edge across for level transitions) — this is a straight crossfade, used by the
    /// ending cutscene to cover/reveal the camera shot and the splash art.
    public class ScreenFade : MonoBehaviour
    {
        [SerializeField] CanvasGroup group;

        float from, to, duration, elapsed;
        bool fading;

        /// True while a fade is still running.
        public bool IsBusy => fading;

        void Awake() => SetAlpha(0f);

        /// Snap to a given opacity with no animation.
        public void SetAlpha(float a)
        {
            fading = false;
            group.alpha = a;
            group.blocksRaycasts = a > 0.001f;
        }

        public void FadeTo(float target, float seconds)
        {
            from = group.alpha; to = target; duration = Mathf.Max(0.0001f, seconds); elapsed = 0f;
            fading = true;
            group.blocksRaycasts = true;
        }

        void Update()
        {
            if (!fading) return;
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            group.alpha = Mathf.Lerp(from, to, t);
            if (t < 1f) return;

            fading = false;
            group.blocksRaycasts = group.alpha > 0.001f;
        }
    }
}
