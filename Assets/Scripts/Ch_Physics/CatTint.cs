using UnityEngine;

namespace NineLives
{
    /// Recolors the cat by screen-blending a tint over every body-part sprite, so the black
    /// fur can turn a color while white eyes/whiskers stay white (a plain SpriteRenderer.color
    /// can't do this — it multiplies, and black * anything = black).
    ///
    /// Animator-friendly: keyframe the two fields below on THIS one component and the whole cat
    /// recolors — no need to touch the material or keyframe all ten sprites. Requires the cat
    /// sprites to use the SpriteScreenTint material (Mat_CatTint).
    [ExecuteAlways]
    public class CatTint : MonoBehaviour
    {
        [Header("Cat recolor  (keyframe these in the Animation window)")]
        [Tooltip("Target color the black fur turns toward. White areas stay bright.")]
        public Color tint = Color.black;
        [Tooltip("0 = normal cat, 1 = full color. Keyframe 0->1 to bloom into the color.")]
        [Range(0f, 1f)] public float strength = 0f;

        [Header("Gameplay recolor  (code-driven — never keyframe these)")]
        [Tooltip("Second tint channel, set from script. Separate from the two fields above because " +
                 "Anim_CorpseState keyframes those, so the Animator would stomp anything code wrote.")]
        public Color gameplayTint = Color.black;
        [Range(0f, 1f)] public float gameplayStrength = 0f;

        static readonly int TintID = Shader.PropertyToID("_Tint");
        SpriteRenderer[] sprites;
        MaterialPropertyBlock mpb;

        void OnEnable() => Refresh();

        void Refresh()
        {
            sprites = GetComponentsInChildren<SpriteRenderer>(true);
            mpb ??= new MaterialPropertyBlock();
        }

        void OnValidate()
        {
            if (sprites == null || sprites.Length == 0) Refresh();
            Apply();
        }

        void LateUpdate() => Apply();

        void Apply()
        {
            if (sprites == null) return;
            mpb ??= new MaterialPropertyBlock(); // survives domain reload, which nulls it but not `sprites`
            // Blend the animated channel with the code-driven one by weight, then pack the combined
            // strength into the tint's alpha; the shader multiplies rgb by that alpha, so
            // strength 0 = fully transparent tint = untouched sprite.
            float sum = strength + gameplayStrength;
            Color effective = sum > 0f
                ? (tint * strength + gameplayTint * gameplayStrength) / sum
                : Color.black;
            effective.a = Mathf.Clamp01(sum);

            for (int i = 0; i < sprites.Length; i++)
            {
                var sr = sprites[i];
                if (sr == null) continue;
                sr.GetPropertyBlock(mpb);
                mpb.SetColor(TintID, effective);
                sr.SetPropertyBlock(mpb);
            }
        }
    }
}
