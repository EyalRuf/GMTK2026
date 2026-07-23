using UnityEngine;

namespace NineLives
{
    /// Marks the root of a level (prefab or scene object). Drag the Entry/Exit
    /// markers in the Inspector; GameManager reads this after instantiating the level.
    public class LevelRoot : MonoBehaviour
    {
        public string levelName = "New Level";
        [TextArea] public string hint = "";
        [Tooltip("Death-timer duration in seconds for every life on this level.")]
        public float timer = 12f;
        [Tooltip("Empty child marking where the cat spawns (feet position).")]
        public Transform entry;
        [Tooltip("The ExitPad (or any child) marking the exit, for the editor gizmo only.")]
        public Transform exit;

        public Vector3 EntryFeet => entry != null ? entry.position : transform.position;

        void OnDrawGizmos()
        {
            if (entry != null)
            {
                Gizmos.color = new Color(0.95f, 0.55f, 0.18f);
                Gizmos.DrawWireSphere(entry.position + Vector3.up * 0.6f, 0.4f);
                Gizmos.DrawLine(entry.position, entry.position + Vector3.up * 1.2f);
            }
            if (exit != null)
            {
                Gizmos.color = new Color(0.3f, 0.9f, 0.45f);
                Gizmos.DrawWireSphere(exit.position + Vector3.up * 0.6f, 0.4f);
            }
        }
    }
}
