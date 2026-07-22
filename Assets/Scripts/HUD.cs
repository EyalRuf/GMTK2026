using UnityEngine;
using UnityEngine.UI;

namespace NineLives
{
    /// Built entirely in code so nothing needs wiring in the scene.
    public class HUD : MonoBehaviour
    {
        Font font;
        Sprite box;
        Text levelLabel, timerText, livesText, hintText, bannerText, bannerSub, controlsText;
        Image barFill, barBg, bannerBg;

        public void BuildUI()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            box = MakeSprite();

            var canvasGo = new GameObject("HUDCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            var root = canvasGo.transform;

            levelLabel = Label(root, "level", 40, TextAnchor.UpperLeft,
                new Vector2(0, 1), new Vector2(30, -24), new Vector2(700, 60));
            livesText = Label(root, "lives", 42, TextAnchor.UpperRight,
                new Vector2(1, 1), new Vector2(-30, -24), new Vector2(760, 60));

            timerText = Label(root, "0.0", 120, TextAnchor.UpperCenter,
                new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(500, 150));
            timerText.fontStyle = FontStyle.Bold;

            barBg = Bar(root, new Color(1, 1, 1, 0.12f), new Vector2(0.5f, 1), new Vector2(0, -170), new Vector2(520, 14));
            barFill = Bar(root, new Color(0.95f, 0.75f, 0.2f, 0.95f), new Vector2(0.5f, 1), new Vector2(0, -170), new Vector2(520, 14));
            barFill.rectTransform.pivot = new Vector2(0, 0.5f);
            barFill.rectTransform.anchoredPosition = new Vector2(-260, -170);

            hintText = Label(root, "", 30, TextAnchor.LowerCenter,
                new Vector2(0.5f, 0), new Vector2(0, 90), new Vector2(1400, 80));
            hintText.color = new Color(1, 1, 1, 0.7f);

            controlsText = Label(root, "A/D move   Space jump   Q sacrifice   R restart",
                24, TextAnchor.LowerCenter, new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(1400, 40));
            controlsText.color = new Color(1, 1, 1, 0.4f);

            // center banner
            bannerBg = Bar(root, new Color(0, 0, 0, 0.55f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400, 320));
            bannerText = Label(root, "", 90, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(1400, 160));
            bannerText.fontStyle = FontStyle.Bold;
            bannerSub = Label(root, "", 36, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0, -70), new Vector2(1400, 100));
            HideBanner();
        }

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

        Text Label(Transform parent, string s, int size, TextAnchor anchor,
                   Vector2 anch, Vector2 pos, Vector2 sizeDelta)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = font; t.text = s; t.fontSize = size; t.alignment = anchor;
            t.color = Color.white; t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = anch; rt.pivot = anch;
            rt.anchoredPosition = pos; rt.sizeDelta = sizeDelta;
            return t;
        }

        Image Bar(Transform parent, Color c, Vector2 anch, Vector2 pos, Vector2 size)
        {
            var go = new GameObject("Bar");
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = box; img.color = c; img.type = Image.Type.Sliced;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = anch; rt.pivot = anch;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return img;
        }

        static Sprite MakeSprite()
        {
            var tex = new Texture2D(4, 4);
            var px = new Color[16];
            for (int i = 0; i < 16; i++) px[i] = Color.white;
            tex.SetPixels(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, Vector4.one);
        }
    }
}
