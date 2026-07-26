using UnityEngine;

namespace NineLives
{
    /// The row of soul icons at the top of the HUD. Populates itself with `count` SoulIcon
    /// instances (spaced via this object's HorizontalLayoutGroup) and drains them right-to-left
    /// from a single continuous timer. Layout, spacing, and colors are all tunable on this prefab.
    public class SoulRow : MonoBehaviour
    {
        [SerializeField] SoulIcon soulIconPrefab;
        [SerializeField] Color fullColor = Color.white;
        [SerializeField] Color lowColor = new Color(1f, 0.55f, 0.5f, 1f);
        [SerializeField, Range(0f, 1f)] float lowThreshold = 0.34f;

        SoulIcon[] souls;

        public RectTransform RectTransform => (RectTransform)transform;

        public void Build(int count)
        {
            if (souls != null && souls.Length == count) return;

            if (souls != null)
                foreach (var s in souls)
                    if (s != null) Destroy(s.gameObject);

            souls = new SoulIcon[count];
            for (int i = 0; i < count; i++)
            {
                var icon = Instantiate(soulIconPrefab, transform);
                icon.SetFill(1f, fullColor);
                souls[i] = icon;
            }
        }

        public void SetProgress(float remaining, float interval)
        {
            if (souls == null || interval <= 0f) return;
            for (int i = 0; i < souls.Length; i++)
            {
                float f = Mathf.Clamp01((remaining - i * interval) / interval);
                souls[i].SetFill(f, f > 0f && f <= lowThreshold ? lowColor : fullColor);
            }
        }
    }
}
