using UnityEngine;
using UnityEngine.UI;

namespace NineLives
{
    /// Data-bound to the hand-built Canvas hierarchy in HUD.prefab — no layout or
    /// GameObject construction here, just wiring numbers/strings into the UI.
    /// The one exception is the soul row (built in code): it replaces the prefab's
    /// horizontal timer bar with N radial-draining soul icons.
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

        static readonly Color SoulFull = new Color(0.95f, 0.80f, 0.30f, 1f);
        static readonly Color SoulFullLow = new Color(0.95f, 0.40f, 0.25f, 1f);
        static readonly Color SoulEmpty = new Color(0.28f, 0.30f, 0.38f, 1f);

        [Tooltip("How far up (px) the soul row rises to reclaim the timer's slot when the number is hidden.")]
        [SerializeField] float soulRowHiddenRise = 90f;

        RectTransform soulRow;
        float soulRowBaseY;
        Image[] soulTop;
        Sprite soulSprite;

        void Awake()
        {
            HideBanner();
            // Souls are shown by the top-center soul row now; the old top-right counter is retired.
            if (livesText != null) livesText.gameObject.SetActive(false);
        }

        public void SetLevel(string name, int index, int total) =>
            levelLabel.text = $"LEVEL {index}/{total}\n{name}";

        public void ShowTimerText(bool show)
        {
            timerText.gameObject.SetActive(show);
            if (soulRow != null)
            {
                var p = soulRow.anchoredPosition;
                soulRow.anchoredPosition = new Vector2(p.x, soulRowBaseY + (show ? 0f : soulRowHiddenRise));
            }
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
            if (soulTop != null && soulTop.Length == count) return;

            if (soulRow != null) Destroy(soulRow.gameObject);
            if (soulSprite == null) soulSprite = MakeDisc();

            barBg.gameObject.SetActive(false);
            barFill.gameObject.SetActive(false);

            var barRt = barBg.rectTransform;
            var rowGo = new GameObject("SoulRow", typeof(RectTransform));
            soulRow = rowGo.GetComponent<RectTransform>();
            soulRow.SetParent(barRt.parent, false);
            soulRow.anchorMin = barRt.anchorMin;
            soulRow.anchorMax = barRt.anchorMax;
            soulRow.pivot = barRt.pivot;
            soulRow.anchoredPosition = barRt.anchoredPosition;
            soulRow.sizeDelta = barRt.sizeDelta;
            soulRowBaseY = barRt.anchoredPosition.y;

            soulTop = new Image[count];

            const float size = 30f, gap = 8f;
            float step = size + gap;
            float x0 = -(count * step - gap) * 0.5f + size * 0.5f;

            for (int i = 0; i < count; i++)
            {
                float x = x0 + i * step;
                MakeSoul($"SoulEmpty{i}", x, size, SoulEmpty, Image.Type.Simple);
                var top = MakeSoul($"SoulFull{i}", x, size, SoulFull, Image.Type.Filled);
                top.fillMethod = Image.FillMethod.Radial360;
                top.fillOrigin = (int)Image.Origin360.Top;
                top.fillClockwise = false;
                top.fillAmount = 1f;
                soulTop[i] = top;
            }
        }

        Image MakeSoul(string name, float x, float size, Color col, Image.Type type)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(soulRow, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = new Vector2(x, 0f);
            var img = go.GetComponent<Image>();
            img.sprite = soulSprite;
            img.color = col;
            img.type = type;
            img.raycastTarget = false;
            return img;
        }

        /// Drain the souls right-to-left from a single continuous timer. Soul i (0 = leftmost)
        /// owns the time band [i*interval, (i+1)*interval); its fill is how full that band is.
        public void SetSouls(float remaining, float interval)
        {
            if (soulTop == null || interval <= 0f) return;
            for (int i = 0; i < soulTop.Length; i++)
            {
                float f = Mathf.Clamp01((remaining - i * interval) / interval);
                soulTop[i].fillAmount = f;
                soulTop[i].color = f > 0f && f <= 0.34f ? SoulFullLow : SoulFull;
            }
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

        static Sprite MakeDisc()
        {
            const int s = 64;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float r = s * 0.46f, c = (s - 1) * 0.5f;
            var px = new Color[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    px[y * s + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(r - d));
                }
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
