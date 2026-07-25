using UnityEngine;

namespace NineLives
{
    // Drop one into a level scene, position it over the playable area, and size it to
    // where the background art actually covers (esp. above the ceiling). CameraFollow
    // clamps to this so the player can't scroll the camera past the edge of the art.
    public class CameraBounds : MonoBehaviour
    {
        public Vector2 size = new Vector2(20f, 10f);

        public Vector2 Min => (Vector2)transform.position - size * 0.5f;
        public Vector2 Max => (Vector2)transform.position + size * 0.5f;

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0f, 1f, 1f, 0.9f);
            Gizmos.DrawWireCube(transform.position, size);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 1f, 1f, 0.15f);
            Gizmos.DrawCube(transform.position, size);
        }
    }
}
