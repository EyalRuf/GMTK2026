using UnityEngine;

namespace NineLives
{
    /// Purely visual chain/rope: stretches and rotates a sprite between a fixed anchor and a
    /// moving target every frame. No physics of its own — pairs with HangingPhysicsObject, whose
    /// hinge already keeps the anchor-to-object distance fixed like a real inextensible chain.
    /// Expects the sprite's pivot at its top (the anchor end) and drawn pointing straight down
    /// at scale 1.
    [ExecuteAlways]
    public class RopeVisual : MonoBehaviour
    {
        public Transform anchor;
        public Transform target;
        [Tooltip("World-space length the sprite covers at localScale.y = 1.")]
        public float spriteBaseLength = 1f;

        [Header("Chain Links")]
        [Tooltip("Optional. Parent of the repeated link sprites. Assign it and the links hold a constant world size no matter how long the rope is — only as many as fit are shown. Leave empty and the children just stretch with the rope (fine for short ropes).")]
        public Transform links;
        [Tooltip("World-space length one link sprite covers. The rope shows as many links as it takes to cover its length, then evens them out so they meet exactly.")]
        public float linkWorldLength = 1.93f;

        SpriteRenderer[] linkSprites;

        void LateUpdate() => Refresh();

        public void Refresh()
        {
            // A half-typed 0 in the Inspector would divide the scale to Infinity and spam errors.
            if (anchor == null || target == null || spriteBaseLength <= 0.0001f) return;

            Vector3 delta = target.position - anchor.position;
            float distance = delta.magnitude;
            if (distance < 0.0001f) return;

            transform.position = anchor.position;
            transform.rotation = Quaternion.FromToRotation(Vector3.down, delta);

            var scale = transform.localScale;
            scale.y = distance / spriteBaseLength;
            transform.localScale = scale;

            if (links != null) TileLinks(anchor.position, delta / distance, distance);
        }

        /// The links sit under a transform this one stretches by the rope's length, so on a long
        /// rope the art would smear. Undo that stretch per link and lay them end to end instead,
        /// hiding whatever doesn't fit — a 30-unit chain then reads as 15 links, not 4 long ones.
        void TileLinks(Vector3 top, Vector3 down, float distance)
        {
            int available = links.childCount;
            if (available == 0 || linkWorldLength <= 0.0001f) return;
            if (linkSprites == null || linkSprites.Length != available) CacheLinkSprites(available);

            int count = Mathf.Clamp(Mathf.CeilToInt(distance / linkWorldLength), 1, available);
            float segment = distance / count;
            float parentScaleY = links.lossyScale.y;

            for (int i = 0; i < available; i++)
            {
                var link = links.GetChild(i);
                bool used = i < count;
                if (link.gameObject.activeSelf != used) link.gameObject.SetActive(used);
                if (!used) continue;

                var sprite = linkSprites[i] != null ? linkSprites[i].sprite : null;
                float artHeight = sprite != null ? sprite.bounds.size.y : 0f;
                if (artHeight > 0.0001f && Mathf.Abs(parentScaleY) > 0.0001f)
                {
                    var s = link.localScale;
                    // A hair of overlap — butted exactly end to end, the tight sprite meshes leave
                    // a visible hairline seam between links.
                    s.y = segment * 1.02f / (artHeight * parentScaleY);
                    link.localScale = s;
                }

                // Only the along-rope axis is ours; whatever lateral offset the link was authored
                // with (the trap's chain sits slightly off-centre) stays put.
                var local = links.InverseTransformPoint(top + down * ((i + 0.5f) * segment));
                var lp = link.localPosition;
                lp.y = local.y;
                link.localPosition = lp;
            }
        }

        void CacheLinkSprites(int available)
        {
            linkSprites = new SpriteRenderer[available];
            for (int i = 0; i < available; i++) linkSprites[i] = links.GetChild(i).GetComponent<SpriteRenderer>();
        }
    }
}
