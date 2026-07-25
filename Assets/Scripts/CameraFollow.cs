using UnityEngine;

namespace NineLives
{
    public class CameraFollow : MonoBehaviour
    {
        GameConfig cfg;
        Transform target;
        PlayerController player;
        Vector3 vel;

        float shakeTimeLeft;
        float shakeDuration;
        float shakeMagnitude;

        public void Configure(GameConfig config, PlayerController p)
        {
            cfg = config; player = p; target = p.transform;
        }

        void LateUpdate()
        {
            if (target == null) return;
            float look = player != null ? player.Velocity.x * cfg.cameraLookAhead : 0f;
            Vector3 goal = target.position + cfg.cameraOffset + Vector3.right * look;
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
            transform.position = target.position + cfg.cameraOffset;
            vel = Vector3.zero;
        }
    }
}
