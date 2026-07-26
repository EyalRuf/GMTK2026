using System.Collections.Generic;
using UnityEngine;

namespace NineLives
{
    /// Procedural verlet spring-bone tail. Walks a single-child bone chain from rootBone, simulates
    /// hanging points under gravity, and writes bone rotations in LateUpdate so it overwrites the
    /// Animator's authored tail keys. All feel is tuned here in the Inspector — no code, no GameConfig.
    ///
    /// Corpse-only by default: on the live player cat it disables itself so the hand-authored tail
    /// plays. Flip enableWhileAlive to drive the walking cat too.
    public class TailPhysics : MonoBehaviour
    {
        [Header("Setup")]
        [Tooltip("First bone of the tail chain (e.g. bone_3). Leave empty to auto-resolve the rig path.")]
        [SerializeField] Transform rootBone;
        [Tooltip("Also run on the live player cat. Off = only runs on a corpse (live cat keeps its authored tail).")]
        [SerializeField] bool enableWhileAlive = false;

        [Header("Feel")]
        [Tooltip("Pulls the tail tips down. Higher = heavier, droops more.")]
        public float gravity = 9f;
        [Tooltip("Pull back toward the authored pose (0..1). 0 = fully floppy, 1 = holds animation.")]
        [Range(0f, 1f)] public float stiffness = 0.15f;
        [Tooltip("Velocity damping (0..1). Higher = settles fast, lower = swings longer.")]
        [Range(0f, 1f)] public float damping = 0.35f;
        [Tooltip("Constraint solver passes. Higher = stiffer, less stretchy.")]
        public int iterations = 2;

        [Header("Collision")]
        [Tooltip("Push the tail out of colliders so it drapes over ledges.")]
        public bool collide = true;
        [Tooltip("Thickness of the tail's collision probe.")]
        public float collisionRadius = 0.15f;
        [Tooltip("Which layers the tail collides against. Exclude the player/cat layers.")]
        public LayerMask collisionMask = ~0;

        readonly List<Transform> bones = new();
        Vector3[] simPoints;
        Vector3[] prevPoints;
        float[] restLength;
        float planeZ;
        bool active;

        void Awake()
        {
            if (rootBone == null)
                rootBone = transform.Find("Cat_Scaler/CAT_CUTS_Resize/Main_Body/Bottom_Body/bone_3");

            if (!enableWhileAlive && GetComponentInParent<PlayerController>() != null)
            {
                enabled = false;
                return;
            }
            if (rootBone == null) { enabled = false; return; }

            Transform b = rootBone;
            while (b != null)
            {
                bones.Add(b);
                b = b.childCount > 0 ? b.GetChild(0) : null;
            }
            if (bones.Count < 3) { enabled = false; return; }

            int n = bones.Count;
            simPoints = new Vector3[n];
            prevPoints = new Vector3[n];
            restLength = new float[n - 1];
            for (int i = 0; i < n; i++) simPoints[i] = prevPoints[i] = bones[i].position;
            for (int i = 0; i < n - 1; i++) restLength[i] = Vector3.Distance(bones[i].position, bones[i + 1].position);
            planeZ = rootBone.position.z;
            active = true;
        }

        void LateUpdate()
        {
            if (!active) return;
            int n = bones.Count;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            simPoints[0] = bones[0].position;
            prevPoints[0] = simPoints[0];

            float g = gravity * dt * dt;
            float keep = 1f - Mathf.Clamp01(damping);
            for (int i = 1; i < n; i++)
            {
                Vector3 temp = simPoints[i];
                Vector3 velocity = (simPoints[i] - prevPoints[i]) * keep;
                simPoints[i] += velocity;
                simPoints[i].y -= g;
                prevPoints[i] = temp;
            }

            int iter = Mathf.Max(1, iterations);
            for (int it = 0; it < iter; it++)
            {
                for (int i = 1; i < n; i++)
                {
                    Vector3 dir = simPoints[i] - simPoints[i - 1];
                    float d = dir.magnitude;
                    if (d > 1e-5f) simPoints[i] = simPoints[i - 1] + dir * (restLength[i - 1] / d);
                    simPoints[i].z = planeZ;
                }
            }

            if (collide)
            {
                for (int i = 1; i < n; i++)
                {
                    var overlaps = Physics.OverlapSphere(simPoints[i], collisionRadius, collisionMask, QueryTriggerInteraction.Ignore);
                    foreach (var c in overlaps)
                    {
                        if (c.transform.IsChildOf(transform)) continue;
                        Vector3 closest = c.ClosestPoint(simPoints[i]);
                        Vector3 push = simPoints[i] - closest;
                        float pd = push.magnitude;
                        if (pd < 1e-5f) continue;
                        simPoints[i] = closest + push * (collisionRadius / pd);
                        simPoints[i].z = planeZ;
                    }
                }
            }

            float stiff = Mathf.Clamp01(stiffness);
            for (int i = 0; i < n - 1; i++)
            {
                Vector3 from = bones[i].position;
                Vector3 currentAim = bones[i + 1].position - from;
                Vector3 desiredAim = simPoints[i + 1] - from;
                if (currentAim.sqrMagnitude < 1e-8f || desiredAim.sqrMagnitude < 1e-8f) continue;
                Quaternion delta = Quaternion.FromToRotation(currentAim.normalized, desiredAim.normalized);
                Quaternion physRot = delta * bones[i].rotation;
                bones[i].rotation = Quaternion.Slerp(physRot, bones[i].rotation, stiff);
            }
        }
    }
}
