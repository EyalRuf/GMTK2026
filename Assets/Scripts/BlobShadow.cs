using UnityEngine;

namespace NineLives
{
    /// Fake cast shadow: a soft dark ellipse pinned to the ground under the cat.
    /// Camera is orthographic side-on, so this faces the camera (XY plane) rather than
    /// lying flat. Self-contained — builds its own texture/sprite at runtime, all tunables
    /// live here so it never touches GameConfig or any shared settings.
    public class BlobShadow : MonoBehaviour
    {
        [Header("Look")]
        [Tooltip("Half-width of the shadow in world units when the cat is grounded.")]
        public float shadowRadius = 0.55f;
        [Tooltip("Vertical squash. 1 = circle, lower = flatter ellipse.")]
        [Range(0.1f, 1f)] public float ellipseSquash = 0.4f;
        [Tooltip("Edge softness. 0 = hard disc, 1 = fully faded from the center out.")]
        [Range(0f, 1f)] public float softness = 0.6f;
        [Tooltip("Shadow tint. Alpha sets the darkest (grounded) opacity.")]
        public Color shadowColor = new Color(0f, 0f, 0f, 0.5f);

        [Header("Placement")]
        [Tooltip("Nudge the shadow along X to line up under the cat art (art sits ~0.07 off the player root).")]
        public float horizontalOffset = 0.07f;
        [Tooltip("Lift the shadow off the ground contact point. Positive = higher.")]
        public float verticalOffset = 0.02f;

        [Header("Behaviour")]
        [Tooltip("Height above ground at which the shadow fully fades out and vanishes.")]
        public float maxHeight = 6f;
        [Tooltip("How far down to look for ground from the feet.")]
        public float groundSearchDistance = 30f;
        [Tooltip("Sorting order for the shadow. Cat art uses 1-5, so keep this below.")]
        public int sortingOrder = 0;
        [Tooltip("Sorting layer to draw on. Leave empty to inherit Default.")]
        public string sortingLayerName = "";

        SpriteRenderer sr;
        Transform shadow;
        int selfLayerMask;

        void Awake()
        {
            var go = new GameObject("BlobShadow");
            shadow = go.transform;
            shadow.SetParent(transform, false);

            sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = BuildSprite();
            sr.color = shadowColor;
            sr.sortingOrder = sortingOrder;
            if (!string.IsNullOrEmpty(sortingLayerName))
                sr.sortingLayerName = sortingLayerName;

            // Everything except the player's own colliders.
            selfLayerMask = ~0;
        }

        void OnValidate()
        {
            // Rebuild the sprite live when radius/softness are tweaked in play mode.
            if (sr != null) sr.sprite = BuildSprite();
        }

        void LateUpdate()
        {
            Vector3 feet = transform.position;
            Vector3 origin = feet + Vector3.up * 0.05f;

            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit,
                    groundSearchDistance, selfLayerMask, QueryTriggerInteraction.Ignore)
                || hit.collider.transform.IsChildOf(transform))
            {
                // Retry ignoring our own body: gather all hits, take the nearest non-self.
                if (!TryGroundIgnoringSelf(origin, out hit))
                {
                    sr.enabled = false;
                    return;
                }
            }

            float height = feet.y - hit.point.y;
            if (height < 0f) height = 0f;
            if (height > maxHeight)
            {
                sr.enabled = false;
                return;
            }

            sr.enabled = true;
            float t = 1f - height / maxHeight;           // 1 grounded -> 0 at maxHeight

            // Sit on the ground, nudged toward the camera (-Z) to avoid z-fighting.
            shadow.position = new Vector3(feet.x + horizontalOffset, hit.point.y + verticalOffset, feet.z - 0.05f);
            shadow.localRotation = Quaternion.identity;
            shadow.localScale = new Vector3(t, t * ellipseSquash, 1f);

            Color c = shadowColor;
            c.a = shadowColor.a * t;
            sr.color = c;
        }

        bool TryGroundIgnoringSelf(Vector3 origin, out RaycastHit best)
        {
            best = default;
            var hits = Physics.RaycastAll(origin, Vector3.down, groundSearchDistance,
                selfLayerMask, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            bool found = false;
            foreach (var h in hits)
            {
                if (h.collider.transform.IsChildOf(transform)) continue;
                if (h.distance < nearest)
                {
                    nearest = h.distance;
                    best = h;
                    found = true;
                }
            }
            return found;
        }

        Sprite BuildSprite()
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            float c = (size - 1) * 0.5f;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - c) / c;
                    float dy = (y - c) / c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);   // 0 center -> 1 edge
                    // softness=0 -> hard disc (solid until the rim); softness=1 -> fade starts at center.
                    float inner = 1f - Mathf.Clamp01(softness);
                    float a = d <= inner ? 1f : Mathf.Clamp01(1f - (d - inner) / Mathf.Max(1e-4f, 1f - inner));
                    a = a * a;
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();

            float ppu = size / (shadowRadius * 2f);
            return Sprite.Create(tex, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), ppu);
        }
    }
}
