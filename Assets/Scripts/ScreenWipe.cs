using UnityEngine;
using UnityEngine.UI;

namespace NineLives
{
    /// The diagonal black cut between levels. One oversized Image rotated on Z, so its leading
    /// edge is a diagonal line; sliding it across the screen is the whole effect — no shader, no
    /// mask, and the Image's sprite can be swapped for a torn//ragged edge in the art pass without
    /// touching this.
    ///
    /// It always sweeps the same way: enters from the right to cover, then keeps going and exits
    /// left to reveal, so a cover+reveal pair reads as one continuous slide rather than a
    /// fade-in/fade-out. Driven on unscaled time so a stray timescale can never strand it.
    public class ScreenWipe : MonoBehaviour
    {
        [SerializeField] Canvas canvas;
        [Tooltip("The black panel. Give it a sprite later for a ragged edge; the sweep is unchanged.")]
        [SerializeField] Image panel;
        [Tooltip("Tilt of the wipe edge, degrees. 0 = a straight vertical cut.")]
        [SerializeField] float angle = 18f;
        [Tooltip("Extra pixels of travel past the screen edge, so the panel is fully clear at rest.")]
        [SerializeField] float overshoot = 40f;

        RectTransform rt;
        float offRight, offLeft;
        float from, to, duration, elapsed;
        bool sweeping;

        /// True while a cover or reveal is still moving.
        public bool IsBusy => sweeping;

        void Awake()
        {
            rt = panel.rectTransform;
            Layout();
            Clear();
        }

        // The Canvas may not have sized its RectTransform yet in Awake, which would leave the
        // panel measured against a zero rect. Re-measure once everything is up.
        void Start()
        {
            Layout();
            if (!sweeping) Clear();
        }

        /// Sizes and rotates the panel so that at x=0 the rotated rect fully contains the screen,
        /// and works out how far off either side it has to sit to be completely clear of it.
        void Layout()
        {
            var canvasRt = (RectTransform)canvas.transform;
            float w = canvasRt.rect.width, h = canvasRt.rect.height;
            float c = Mathf.Cos(angle * Mathf.Deg2Rad), s = Mathf.Abs(Mathf.Sin(angle * Mathf.Deg2Rad));

            // Screen corners projected into the panel's rotated frame give the minimum size that
            // still covers every corner; +8 keeps a rounding error from showing a sliver.
            float pw = w * c + h * s + 8f;
            float ph = w * s + h * c + 8f;

            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(pw, ph);
            rt.localEulerAngles = new Vector3(0f, 0f, angle);

            offRight = (pw * c + ph * s + w) * 0.5f + overshoot;
            offLeft = -offRight;
        }

        /// Snap to fully hidden, parked at the start of a cover.
        public void Clear()
        {
            sweeping = false;
            SetX(offRight);
            panel.enabled = false;
        }

        /// Snap to fully black with no animation (entering a level straight from the menu).
        public void SetCoveredInstant()
        {
            sweeping = false;
            panel.enabled = true;
            SetX(0f);
        }

        public void Cover(float seconds) => Sweep(offRight, 0f, seconds);
        public void Reveal(float seconds) => Sweep(0f, offLeft, seconds);

        void Sweep(float a, float b, float seconds)
        {
            panel.enabled = true;
            from = a; to = b; duration = Mathf.Max(0.0001f, seconds); elapsed = 0f;
            sweeping = true;
            SetX(a);
        }

        void Update()
        {
            if (!sweeping) return;
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetX(Mathf.LerpUnclamped(from, to, Mathf.SmoothStep(0f, 1f, t)));
            if (t < 1f) return;

            sweeping = false;
            // Parked off the left after a reveal: hop back to the cover start so the next
            // transition enters from the right again instead of backing in.
            if (Mathf.Approximately(to, offLeft)) Clear();
        }

        /// The game view resized (WebGL fullscreen, window drag) — the panel size and the
        /// off-screen distances are both in screen pixels, so they have to be recomputed.
        void OnRectTransformDimensionsChange()
        {
            if (rt == null || sweeping) return;
            bool covered = panel.enabled && Mathf.Abs(rt.anchoredPosition.x) < 1f;
            Layout();
            if (covered) SetCoveredInstant(); else Clear();
        }

        void SetX(float x) => rt.anchoredPosition = new Vector2(x, 0f);
    }
}
