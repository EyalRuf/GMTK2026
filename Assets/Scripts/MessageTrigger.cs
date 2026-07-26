using UnityEngine;

namespace NineLives
{
    /// Editor-placeable hint volume. Drop `MessageTrigger.prefab` into a level, scale the box over
    /// the spot the player needs teaching, and type the line into `message` — it shows above the
    /// cat's head (`PlayerMessage` on the Player prefab) for `duration` seconds.
    [RequireComponent(typeof(BoxCollider))]
    public class MessageTrigger : MonoBehaviour, ILevelResettable
    {
        [TextArea(2, 4)] public string message = "Hint goes here";
        [Tooltip("Seconds to display. 0 = the player's default.")]
        public float duration = 2f;
        [Tooltip("Alpha pulses per second. 0 = the player's default.")]
        public float flashesPerSecond = 0f;
        [Tooltip("Fires once per level attempt. Off = re-shows every time you walk in.")]
        public bool once = true;

        [Header("Editor")]
        public Color gizmoColor = new Color(1f, 0.85f, 0.2f, 0.25f);

        bool fired;

        void Awake() { GetComponent<BoxCollider>().isTrigger = true; }

        public void ResetToInitial() { fired = false; }

        void OnTriggerEnter(Collider other)
        {
            if (once && fired) return;
            var player = other.GetComponentInParent<PlayerController>();
            if (player == null) return;
            var msg = player.GetComponentInChildren<PlayerMessage>(true);
            if (msg == null) return;
            fired = true;
            msg.Show(message, duration, flashesPerSecond);
        }

        void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider>();
            if (box == null) return;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = gizmoColor;
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 1f);
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}
