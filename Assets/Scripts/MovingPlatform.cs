using System.Collections.Generic;
using UnityEngine;

namespace NineLives
{
    /// Editor-placeable platform that shuttles between its start position and start+moveOffset.
    /// With no plates linked it moves continuously; with plates linked it only moves while any
    /// plate in the list is pressed, and holds still otherwise. Player carrying is handled by
    /// PlayerController tracking this platform's position delta while grounded on it.
    public class MovingPlatform : MonoBehaviour
    {
        [Tooltip("World-space offset of the far end of the platform's path from its start position.")]
        public Vector3 moveOffset = new Vector3(4f, 0f, 0f);
        public float speed = 3f;
        [Tooltip("Seconds to pause at each end before reversing.")]
        public float waitTime = 0f;
        [Tooltip("Optional. Leave empty to move continuously; drag plates in to gate movement on them being pressed.")]
        public List<PressurePlate> plates = new();

        Vector3 startPos, endPos;
        bool movingToEnd = true;
        float waitTimer;

        void Awake()
        {
            startPos = transform.position;
            endPos = startPos + moveOffset;
        }

        void Update()
        {
            bool active = plates.Count == 0 || AnyPlatePressed();
            if (!active) return;

            if (waitTimer > 0f)
            {
                waitTimer -= Time.deltaTime;
                return;
            }

            Vector3 target = movingToEnd ? endPos : startPos;
            transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
            if ((transform.position - target).sqrMagnitude < 0.0001f)
            {
                movingToEnd = !movingToEnd;
                waitTimer = waitTime;
            }
        }

        bool AnyPlatePressed()
        {
            for (int i = 0; i < plates.Count; i++)
                if (plates[i] != null && plates[i].Pressed) return true;
            return false;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.95f, 0.65f, 0.2f, 0.8f);
            Vector3 basePos = Application.isPlaying ? startPos : transform.position;
            Vector3 target = basePos + moveOffset;
            Gizmos.DrawLine(basePos, target);
            Gizmos.DrawWireCube(target, transform.lossyScale);
        }
    }
}
