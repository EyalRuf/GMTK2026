using UnityEngine;

namespace NineLives
{
    public class CameraFollow : MonoBehaviour
    {
        GameConfig cfg;
        Transform target;
        PlayerController player;
        CameraBounds bounds;
        Camera cam;
        Vector3 vel;

        float shakeTimeLeft;
        float shakeDuration;
        float shakeMagnitude;

        float lookHoldTimer;
        float lookOffsetY;
        float lookOffsetVel;
        bool lookUpHeld;
        bool lookDownHeld;

        Transform focusA, focusB;
        float focusPadding;
        bool endingFocus;
        float focusSizeVel;
        float defaultOrthoSize;

        public void Configure(GameConfig config, PlayerController p)
        {
            cfg = config; player = p; target = p.transform;
            cam = GetComponent<Camera>();
            defaultOrthoSize = cam.orthographicSize;
        }

        /// Levels are pre-placed and toggled active/inactive rather than loaded, so the bounds
        /// to clamp against must be re-pointed explicitly whenever GameManager starts a level -
        /// a scene-wide FindFirstObjectByType would just grab whichever level's bounds happens
        /// to be first in the hierarchy.
        public void SetBounds(CameraBounds b) => bounds = b;

        /// Fed once per frame from GameManager's sampled input. Holding up/down for
        /// cameraLookHoldDelay seconds pans the camera that direction; releasing eases it back.
        public void SetLookInput(bool up, bool down)
        {
            lookUpHeld = up;
            lookDownHeld = down;
        }

        /// Frames both points (with padding) instead of following the player. Used by the ending
        /// cutscene to hold the devil sprite and the cat in the same shot; smooths in on its own
        /// via LateUpdate, same as the normal follow. Overrides bounds clamping and look-panning.
        public void SetEndingFocus(Transform a, Transform b, float padding)
        {
            focusA = a; focusB = b; focusPadding = padding; endingFocus = true;
        }

        /// Restores orthographicSize immediately — nothing in the normal follow path ever touches
        /// it, so without this it would stay wherever the ending-focus framing left it forever
        /// (including into the next Play session, if scene reload on Play is disabled).
        public void ClearEndingFocus()
        {
            endingFocus = false;
            if (cam != null && cam.orthographic) cam.orthographicSize = defaultOrthoSize;
        }

        void LateUpdate()
        {
            if (target == null) return;

            if (endingFocus)
            {
                ApplyEndingFocus();
                return;
            }

            bool wantsLook = lookUpHeld != lookDownHeld;
            if (wantsLook) lookHoldTimer += Time.deltaTime;
            else lookHoldTimer = 0f;

            float lookTarget = 0f;
            if (lookHoldTimer >= cfg.cameraLookHoldDelay)
                lookTarget = (lookUpHeld ? 1f : -1f) * cfg.cameraLookDistance;
            lookOffsetY = Mathf.SmoothDamp(lookOffsetY, lookTarget, ref lookOffsetVel, cfg.cameraLookSmoothing);

            float look = player != null ? player.Velocity.x * cfg.cameraLookAhead : 0f;
            Vector3 goal = target.position + cfg.cameraOffset + Vector3.right * look + Vector3.up * lookOffsetY;
            goal = Clamp(goal);
            transform.position = Vector3.SmoothDamp(transform.position, goal, ref vel, cfg.cameraSmoothing);

            if (shakeTimeLeft > 0f)
            {
                shakeTimeLeft -= Time.deltaTime;
                float t = Mathf.Clamp01(shakeTimeLeft / shakeDuration);
                Vector2 offset = UnityEngine.Random.insideUnitCircle * shakeMagnitude * t;
                transform.position += new Vector3(offset.x, offset.y, 0f);
            }
        }

        /// Positions the camera to keep both focus points on screen with `focusPadding` of margin.
        /// Perspective: dollies back along Z until both points fit inside the FOV. Orthographic:
        /// grows orthographicSize instead. Both smooth in via the same damping as the normal follow.
        void ApplyEndingFocus()
        {
            if (focusA == null || focusB == null) return;

            Vector3 a = focusA.position, b = focusB.position;
            Vector3 mid = (a + b) * 0.5f;
            float horizExtent = Mathf.Abs(a.x - b.x) * 0.5f + focusPadding;
            float vertExtent = Mathf.Abs(a.y - b.y) * 0.5f + focusPadding * 0.5f;

            float baseDist = Mathf.Abs(cfg.cameraOffset.z);
            float neededDist = baseDist;

            if (cam != null && cam.orthographic)
            {
                float neededSize = Mathf.Max(vertExtent, horizExtent / Mathf.Max(0.01f, cam.aspect));
                cam.orthographicSize = Mathf.SmoothDamp(cam.orthographicSize, Mathf.Max(neededSize, 0.01f), ref focusSizeVel, cfg.cameraSmoothing);
            }
            else if (cam != null)
            {
                float vFov = cam.fieldOfView * Mathf.Deg2Rad;
                float hFov = 2f * Mathf.Atan(Mathf.Tan(vFov * 0.5f) * cam.aspect);
                float distForV = vertExtent / Mathf.Tan(vFov * 0.5f);
                float distForH = horizExtent / Mathf.Tan(hFov * 0.5f);
                neededDist = Mathf.Max(baseDist, distForV, distForH);
            }

            Vector3 goal = new Vector3(mid.x, mid.y + cfg.cameraOffset.y, mid.z - neededDist);
            transform.position = Vector3.SmoothDamp(transform.position, goal, ref vel, cfg.cameraSmoothing);
        }

        public void Shake(float duration, float magnitude)
        {
            shakeDuration = duration;
            shakeTimeLeft = duration;
            shakeMagnitude = magnitude;
        }

        public void Snap()
        {
            if (target == null) return;
            shakeTimeLeft = 0f;
            transform.position = Clamp(target.position + cfg.cameraOffset);
            vel = Vector3.zero;
        }

        Vector3 Clamp(Vector3 pos)
        {
            if (bounds == null) return pos;
            Vector2 min = bounds.Min;
            Vector2 max = bounds.Max;

            float halfH = 0f, halfW = 0f;
            if (cam != null && cam.orthographic)
            {
                halfH = cam.orthographicSize;
                halfW = halfH * cam.aspect;
            }

            pos.x = ClampAxis(pos.x, min.x, max.x, halfW);
            pos.y = ClampAxis(pos.y, min.y, max.y, halfH);
            return pos;
        }

        static float ClampAxis(float value, float min, float max, float half)
        {
            min += half;
            max -= half;
            // if the bounds are narrower than the screen, center instead of clamping to an inverted range
            if (min > max) return (min + max) * 0.5f;
            return Mathf.Clamp(value, min, max);
        }
    }
}
