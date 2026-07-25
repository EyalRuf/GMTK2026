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

        public void Configure(GameConfig config, PlayerController p)
        {
            cfg = config; player = p; target = p.transform;
            bounds = FindFirstObjectByType<CameraBounds>();
            cam = GetComponent<Camera>();
        }

        void LateUpdate()
        {
            if (target == null) return;
            float look = player != null ? player.Velocity.x * cfg.cameraLookAhead : 0f;
            Vector3 goal = target.position + cfg.cameraOffset + Vector3.right * look;
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
