using UnityEngine;

/// <summary>
/// Drives a stylized lava waterfall that stops where the environment stops it.
///
/// Why the pieces exist:
/// - A shader can't see colliders, so this component casts downward from the source, finds the
///   first surface, and stretches a quad to exactly that distance. Crisp collision, no shader clip.
/// - The flow is scrolled by the shader independent of mesh height; we only feed it the world
///   length via a MaterialPropertyBlock (per-instance, so many waterfalls share one material with
///   no instancing leaks) so its texel density stays constant at any height.
/// - The splash is a separate object snapped to the exact impact point — that's where a stylized
///   pool/foam/sparks effect belongs, decoupled from the stream mesh.
///
/// Assumes the stream child is a unit quad lying in local XY (Unity's built-in Quad), pivot at its
/// centre, "down" = the root's local -Y. Angle a waterfall by rotating the whole root.
/// </summary>
[ExecuteAlways]
public class LavaWaterfall : MonoBehaviour
{
    [Header("Stream mesh")]
    [Tooltip("The quad that is stretched to the hit distance and renders the lava.")]
    [SerializeField] Transform stream;
    [SerializeField] Renderer streamRenderer;
    [Tooltip("Furthest the waterfall can reach before it just hangs at full length.")]
    [SerializeField] float maxLength = 20f;
    [Tooltip("World width of the stream.")]
    [SerializeField] float width = 1f;

    [Header("Collision cast")]
    [Tooltip("Which layers the waterfall lands on.")]
    [SerializeField] LayerMask hitMask = ~0;
    [Tooltip("0 = thin ray. >0 = spherecast of this radius (rounder, more forgiving landing).")]
    [SerializeField] float castRadius = 0f;
    [Tooltip("Cast direction in the root's local space. Down by default.")]
    [SerializeField] Vector3 castDirection = Vector3.down;
    [Tooltip("Seconds between recasts at play time. 0 = every frame (use for moving floors).")]
    [SerializeField] float updateInterval = 0f;
    [Tooltip("Keep the waterfall live while editing so it fits the scene in the Editor.")]
    [SerializeField] bool updateInEditMode = true;

    [Header("Splash")]
    [Tooltip("Optional impact effect (particle system / mesh) snapped to the hit point.")]
    [SerializeField] Transform splash;
    [Tooltip("Nudge the splash back up the cast direction so it sits on the surface, not in it.")]
    [SerializeField] float splashSurfaceOffset = 0.05f;

    static readonly int LengthID = Shader.PropertyToID("_Length");

    MaterialPropertyBlock mpb;
    float recastTimer;

    void OnEnable()
    {
        mpb ??= new MaterialPropertyBlock();
        Rebuild();
    }

    void Update()
    {
        if (Application.isPlaying)
        {
            if (updateInterval > 0f)
            {
                recastTimer -= Time.deltaTime;
                if (recastTimer > 0f) return;
                recastTimer = updateInterval;
            }
        }
        else if (!updateInEditMode)
        {
            return;
        }

        Rebuild();
    }

    void Rebuild()
    {
        if (stream == null) return;

        Vector3 localDir = castDirection.sqrMagnitude > 1e-6f ? castDirection.normalized : Vector3.down;
        Vector3 worldDir = transform.TransformDirection(localDir);
        Vector3 origin = transform.position;

        float length = maxLength;
        bool hit;
        RaycastHit info;
        if (castRadius > 0f)
            hit = Physics.SphereCast(origin, castRadius, worldDir, out info, maxLength, hitMask, QueryTriggerInteraction.Ignore);
        else
            hit = Physics.Raycast(origin, worldDir, out info, maxLength, hitMask, QueryTriggerInteraction.Ignore);

        if (hit) length = info.distance;

        // Hang the quad from the source: pivot is centred, so drop it half its length and stretch.
        stream.localPosition = localDir * (length * 0.5f);
        stream.localScale = new Vector3(width, length, 1f);

        if (streamRenderer != null)
        {
            streamRenderer.GetPropertyBlock(mpb);
            mpb.SetFloat(LengthID, length);
            streamRenderer.SetPropertyBlock(mpb);
        }

        if (splash != null)
        {
            if (splash.gameObject.activeSelf != hit)
                splash.gameObject.SetActive(hit);
            if (hit)
                splash.position = info.point - worldDir * splashSurfaceOffset;
        }
    }

    void OnDrawGizmosSelected()
    {
        Vector3 localDir = castDirection.sqrMagnitude > 1e-6f ? castDirection.normalized : Vector3.down;
        Vector3 worldDir = transform.TransformDirection(localDir);
        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, transform.position + worldDir * maxLength);
    }
}
