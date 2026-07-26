using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace NineLives
{
    /// Data-bound to the hand-built Canvas hierarchy in HUD.prefab — no layout or
    /// GameObject construction here, just wiring numbers/strings into the UI.
    /// The one exception is the soul row: it replaces the prefab's horizontal timer bar
    /// with a SoulRow prefab instance of N radial-draining soul icons.
    public class HUD : MonoBehaviour
    {
        [SerializeField] TMP_Text levelLabel;
        [SerializeField] TMP_Text timerText;
        [SerializeField] TMP_Text livesText;
        [SerializeField] TMP_Text hintText;
        [SerializeField] TMP_Text bannerText;
        [SerializeField] TMP_Text bannerSub;
        [SerializeField] Image barFill;
        [SerializeField] Image barBg;
        [SerializeField] Image bannerBg;
        [SerializeField] SoulRow soulRow;

        [Tooltip("How far up (px) the soul row rises to reclaim the timer's slot when the number is hidden.")]
        [SerializeField] float soulRowHiddenRise = 90f;

        float soulRowBaseY;

        void Awake()
        {
            HideBanner();
            // Souls are shown by the top-center soul row now; the old top-right counter is retired.
            if (livesText != null) livesText.gameObject.SetActive(false);
            soulRowBaseY = soulRow.RectTransform.anchoredPosition.y;
        }

        public void SetLevel(string name, int index, int total) =>
            levelLabel.text = $"LEVEL {index}/{total}\n{name}";

        /// Toggles the level name and hint text (the "chrome" a jam config can hide).
        public void SetChromeVisible(bool visible)
        {
            levelLabel.gameObject.SetActive(visible);
            hintText.gameObject.SetActive(visible);
        }

        public void ShowTimerText(bool show)
        {
            timerText.gameObject.SetActive(show);
            var rt = soulRow.RectTransform;
            var p = rt.anchoredPosition;
            rt.anchoredPosition = new Vector2(p.x, soulRowBaseY + (show ? 0f : soulRowHiddenRise));
        }

        public void SetTimer(float remaining, float normalized)
        {
            timerText.text = Mathf.CeilToInt(remaining).ToString();
            timerText.color = normalized < 0.3f ? Color.Lerp(Color.red, new Color(1f, 0.5f, 0.2f), normalized / 0.3f)
                                                : Color.white;
        }

        public void SetLives(int left, int total) { }

        /// (Re)build the soul row for a level with `count` souls, hiding the legacy timer bar.
        public void BuildSouls(int count)
        {
            barBg.gameObject.SetActive(false);
            barFill.gameObject.SetActive(false);
            soulRow.Build(count);
        }

        /// Drain the souls right-to-left from a single continuous timer. Soul i (0 = leftmost)
        /// owns the time band [i*interval, (i+1)*interval); its fill is how full that band is.
        public void SetSouls(float remaining, float interval) => soulRow.SetProgress(remaining, interval);

        public void SetHint(string h) => hintText.text = h;

        public void Banner(string title, string sub, Color col)
        {
            bannerBg.gameObject.SetActive(true);
            bannerText.gameObject.SetActive(true);
            bannerSub.gameObject.SetActive(true);
            bannerText.text = title; bannerText.color = col;
            bannerSub.text = sub;
        }

        public void HideBanner()
        {
            bannerBg.gameObject.SetActive(false);
            bannerText.gameObject.SetActive(false);
            bannerSub.gameObject.SetActive(false);
        }

        /// Hides the timer number and soul icons immediately — used when a cutscene (the ending
        /// sequence) takes over and the countdown no longer applies, ahead of the HUD as a whole
        /// being disabled later in that same sequence.
        public void HideCountdown()
        {
            timerText.gameObject.SetActive(false);
            soulRow.gameObject.SetActive(false);
        }
    }
}
