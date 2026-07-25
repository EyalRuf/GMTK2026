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

        void LateUpdate()
        {
            if (anchor == null || target == null) return;

            Vector3 delta = target.position - anchor.position;
            float distance = delta.magnitude;
            if (distance < 0.0001f) return;

            transform.position = anchor.position;
            transform.rotation = Quaternion.FromToRotation(Vector3.down, delta);

            var scale = transform.localScale;
            scale.y = distance / spriteBaseLength;
            transform.localScale = scale;
        }
    }
}
