using UnityEngine;

namespace NineLives
{
    /// Reusable parallax component. Add it to any purely-visual layer (a single large
    /// sprite spanning the level) to have it move relative to the camera by a tunable
    /// amount. One component drives every layer — configure it entirely in the Inspector.
    ///
    /// The multiplier is apparent on-screen movement relative to the play surface:
    ///   1.0 = moves 1:1 with the camera, like the play surface (world-static).
    ///   > 1  = foreground: appears to move faster than the play surface.
    ///   < 1  = background: appears to move slower (0 = pinned to the camera / infinitely far).
    ///
    /// Works per-level: each layer anchors to wherever it and the camera sit the first
    /// frame the level is active, and re-anchors on re-entry, so levels are fully
    /// independent and layers can be freely duplicated between them.
    [DisallowMultipleComponent]
    public class ParallaxLayer : MonoBehaviour
    {
        [Header("Horizontal")]
        public bool enableHorizontal = true;
        [Tooltip("1 = play surface (moves with camera). >1 foreground, <1 background.")]
        public float horizontalMultiplier = 1f;

        [Header("Vertical")]
        public bool enableVertical = true;
        public float verticalMultiplier = 1f;

        [Tooltip("Camera to parallax against. Leave empty to use Camera.main.")]
        public Transform cameraTransform;

        Transform cam;
        Vector3 anchorLayerPos;
        Vector3 anchorCamPos;
        bool anchored;

        void OnEnable() => anchored = false;

        void LateUpdate()
        {
            if (cam == null)
            {
                cam = cameraTransform != null ? cameraTransform
                    : (Camera.main != null ? Camera.main.transform : null);
                if (cam == null) return;
            }

            // Anchor on the first active frame (after the camera has snapped for this level).
            if (!anchored)
            {
                anchorLayerPos = transform.position;
                anchorCamPos = cam.position;
                anchored = true;
                return;
            }

            Vector3 camDelta = cam.position - anchorCamPos;
            Vector3 p = transform.position;
            if (enableHorizontal)
                p.x = anchorLayerPos.x + camDelta.x * (1f - horizontalMultiplier);
            if (enableVertical)
                p.y = anchorLayerPos.y + camDelta.y * (1f - verticalMultiplier);
            transform.position = p;
        }
    }
}
