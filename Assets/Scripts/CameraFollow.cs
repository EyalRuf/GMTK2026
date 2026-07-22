using UnityEngine;

namespace NineLives
{
    public class CameraFollow : MonoBehaviour
    {
        GameConfig cfg;
        Transform target;
        PlayerController player;
        Vector3 vel;

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
        }

        public void Snap()
        {
            if (target == null) return;
            transform.position = target.position + cfg.cameraOffset;
            vel = Vector3.zero;
        }
    }
}
