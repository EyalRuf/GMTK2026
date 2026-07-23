using System.Collections.Generic;
using UnityEngine;

namespace NineLives
{
    /// A gate or lift that slides to `openOffset` (local-space, relative to wherever
    /// you place it in the editor) while any plate in `plates` is pressed.
    public class LinkedMover : MonoBehaviour
    {
        [Tooltip("Drag the pressure plate(s) that control this mover.")]
        public List<PressurePlate> plates = new();
        [Tooltip("Local-space offset applied when open, e.g. (0,-5,0) sinks a gate into the floor.")]
        public Vector3 openOffset = new Vector3(0f, -5f, 0f);
        public float speed = 6f;

        Vector3 closedLocal, openLocal;

        void Awake()
        {
            closedLocal = transform.localPosition;
            openLocal = closedLocal + openOffset;
        }

        void FixedUpdate()
        {
            bool active = false;
            for (int i = 0; i < plates.Count; i++)
                if (plates[i] != null && plates[i].Pressed) { active = true; break; }

            var target = active ? openLocal : closedLocal;
            transform.localPosition = Vector3.MoveTowards(transform.localPosition, target, speed * Time.fixedDeltaTime);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.9f, 0.5f, 0.6f);
            Vector3 basePos = Application.isPlaying ? closedLocal : transform.localPosition;
            Vector3 worldOpen = transform.parent != null
                ? transform.parent.TransformPoint(basePos + openOffset)
                : basePos + openOffset;
            Gizmos.DrawWireCube(worldOpen, transform.lossyScale);
            Gizmos.DrawLine(transform.position, worldOpen);
        }
    }
}
