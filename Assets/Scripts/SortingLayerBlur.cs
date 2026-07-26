using System;
using System.Collections.Generic;
using UnityEngine;

namespace NineLives
{
    /// Blurs every sprite in the chosen sorting layers. Put it on the camera (it doesn't
    /// actually need the camera — it's just a convenient home) and fill in the list:
    /// a sorting layer name plus how blurry it should be.
    ///
    /// It works by swapping those sprites onto a shared Custom/URP/SpriteBlur material, one
    /// material per entry, so draw order and transparency behave exactly as before. Runtime
    /// only — originals are put back when the component is disabled, and nothing is written
    /// to the scene.
    ///
    /// Two things to know: blur is measured in the sprite's own texture pixels, so the same
    /// number on a small sprite and a huge parallax backdrop won't look equally strong; and
    /// sprites packed in an atlas can smear in neighbours at high amounts.
    [DisallowMultipleComponent]
    public class SortingLayerBlur : MonoBehaviour
    {
        [Serializable]
        public class Entry
        {
            [Tooltip("Sorting layer name, exactly as it appears in Tags & Layers.")]
            public string sortingLayer = "BG6";

            [Tooltip("Blur radius in the sprite's texture pixels. 0 = off.")]
            [Range(0f, 32f)] public float blur = 3f;

            [NonSerialized] public Material material;
        }

        public List<Entry> layers = new List<Entry>();

        [Tooltip("Re-scan for new sprites this often, in seconds (levels spawned at runtime). 0 = only on enable.")]
        public float rescanInterval = 1f;

        [Tooltip("Also blur sprites that use a custom material. Off by default because it throws that material away.")]
        public bool replaceCustomMaterials;

        static readonly int BlurTexelsId = Shader.PropertyToID("_BlurTexels");

        readonly Dictionary<SpriteRenderer, Material> originals = new Dictionary<SpriteRenderer, Material>();
        readonly HashSet<SpriteRenderer> warned = new HashSet<SpriteRenderer>();
        Shader blurShader;
        float nextScan;

        void OnEnable()
        {
            blurShader = Shader.Find("Custom/URP/SpriteBlur");
            if (blurShader == null)
            {
                Debug.LogError("SortingLayerBlur: shader Custom/URP/SpriteBlur not found.", this);
                enabled = false;
                return;
            }

            Refresh();
        }

        void OnDisable()
        {
            foreach (var kv in originals)
                if (kv.Key != null) kv.Key.sharedMaterial = kv.Value;
            originals.Clear();
            warned.Clear();

            foreach (var e in layers)
            {
                if (e.material != null) Destroy(e.material);
                e.material = null;
            }
        }

        void Update()
        {
            // Keep the materials live so the sliders can be dragged in play mode.
            foreach (var e in layers)
                if (e.material != null) e.material.SetFloat(BlurTexelsId, e.blur);

            if (rescanInterval > 0f && Time.unscaledTime >= nextScan)
            {
                nextScan = Time.unscaledTime + rescanInterval;
                Refresh();
            }
        }

        /// Applies the blur materials to every matching sprite currently in the scene.
        /// Safe to call repeatedly; already-converted sprites are skipped.
        public void Refresh()
        {
            var sprites = FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var e in layers)
            {
                if (string.IsNullOrEmpty(e.sortingLayer) || e.blur <= 0f) continue;

                foreach (var sr in sprites)
                {
                    if (sr.sortingLayerName != e.sortingLayer) continue;
                    if (originals.ContainsKey(sr)) continue;

                    var current = sr.sharedMaterial;
                    if (current != null && current.shader != blurShader && !IsPlainSpriteShader(current.shader))
                    {
                        if (!replaceCustomMaterials)
                        {
                            if (warned.Add(sr))
                                Debug.LogWarning($"SortingLayerBlur: skipping '{sr.name}' — it uses the custom " +
                                                 $"shader '{current.shader.name}'. Tick Replace Custom Materials to blur it anyway.", sr);
                            continue;
                        }
                    }

                    if (e.material == null)
                    {
                        e.material = new Material(blurShader) { name = $"SpriteBlur ({e.sortingLayer})" };
                        e.material.SetFloat(BlurTexelsId, e.blur);
                    }

                    originals[sr] = current;
                    sr.sharedMaterial = e.material;
                }
            }
        }

        static bool IsPlainSpriteShader(Shader shader)
        {
            string n = shader.name;
            return n == "Universal Render Pipeline/2D/Sprite-Unlit-Default"
                || n == "Universal Render Pipeline/2D/Sprite-Lit-Default"
                || n == "Sprites/Default";
        }
    }
}
