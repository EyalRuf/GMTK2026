using UnityEngine;
using UnityEngine.UI;

namespace NineLives
{
    /// Data-bound to the hand-built Canvas hierarchy in HUD.prefab — no layout or
    /// GameObject construction here, just wiring numbers/strings into the UI.
    public class HUD : MonoBehaviour
    {
        [SerializeField] Text levelLabel;
        [SerializeField] Text timerText;
        [SerializeField] Text livesText;
        [SerializeField] Text hintText;
        [SerializeField] Text bannerText;
        [SerializeField] Text bannerSub;
        [SerializeField] Image barFill;
        [SerializeField] Image barBg;
        [SerializeField] Image bannerBg;

        void Awake() => HideBanner();

        public void SetLevel(string name, int index, int total) =>
            levelLabel.text = $"LEVEL {index}/{total}\n{name}";

        public void SetTimer(float remaining, float normalized)
        {
            timerText.text = remaining.ToString("0.0");
            timerText.color = normalized < 0.3f ? Color.Lerp(Color.red, new Color(1f, 0.5f, 0.2f), normalized / 0.3f)
                                                : Color.white;
            barFill.rectTransform.sizeDelta = new Vector2(520f * Mathf.Clamp01(normalized), 14f);
            barFill.color = normalized < 0.3f ? new Color(0.9f, 0.25f, 0.2f) : new Color(0.95f, 0.75f, 0.2f);
        }

        public void SetLives(int left, int total)
        {
            var s = "LIVES ";
            for (int i = 0; i < total; i++) s += i < left ? "◆" : "◇";
            livesText.text = s;
            livesText.color = left <= 2 ? new Color(1f, 0.5f, 0.4f) : Color.white;
        }

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
    }
}
