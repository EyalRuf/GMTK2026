using System.Collections.Generic;
using UnityEngine;

namespace NineLives
{
    /// Editor-placeable pressure plate. Size the trigger by scaling the object.
    /// Presses while any corpse or the player overlaps it.
    [RequireComponent(typeof(BoxCollider))]
    public class PressurePlate : MonoBehaviour
    {
        [Tooltip("Visual cap that dips down when pressed. Optional.")]
        public Transform cap;
        public float pressDepth = 0.18f;
        public float capSpeed = 3f;
        [Tooltip("Cap material while unpressed (Mat_Plate).")]
        public Material matUp;
        [Tooltip("Cap material while pressed (Mat_PlateHit).")]
        public Material matDown;

        public bool Pressed => weights.Count > 0;

        readonly HashSet<Collider> weights = new();
        Vector3 capUpLocal, capDownLocal;
        MeshRenderer capRenderer;

        void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;

            if (cap != null)
            {
                capUpLocal = cap.localPosition;
                capDownLocal = capUpLocal + Vector3.down * pressDepth;
                capRenderer = cap.GetComponent<MeshRenderer>();
                if (capRenderer != null) capRenderer.sharedMaterial = matUp;
            }
        }

        void OnTriggerEnter(Collider other) { if (IsWeight(other)) weights.Add(other); }
        void OnTriggerExit(Collider other) { weights.Remove(other); }

        static bool IsWeight(Collider c) =>
            c.GetComponentInParent<Corpse>() != null || c.GetComponentInParent<PlayerController>() != null;

        void Update()
        {
            // Prune colliders that vanished without firing OnTriggerExit (e.g. the
            // player is SetActive(false) on death rather than destroyed).
            weights.RemoveWhere(c => c == null || !c.gameObject.activeInHierarchy);

            if (cap == null) return;
            cap.localPosition = Vector3.MoveTowards(cap.localPosition, Pressed ? capDownLocal : capUpLocal, capSpeed * Time.deltaTime);
            if (capRenderer != null) capRenderer.sharedMaterial = Pressed ? matDown : matUp;
        }
    }
}
