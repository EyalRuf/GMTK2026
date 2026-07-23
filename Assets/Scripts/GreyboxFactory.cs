using UnityEngine;

namespace NineLives
{
    /// Runtime materials + primitive spawning so the whole game is greybox-from-code.
    public static class GreyboxFactory
    {
        static Shader lit;
        static Shader Lit => lit != null ? lit : (lit = Shader.Find("Universal Render Pipeline/Lit"));

        public static Material Make(Color c, float smoothness = 0.1f, bool emissive = false)
        {
            var m = new Material(Lit) { color = c };
            m.SetFloat("_Smoothness", smoothness);
            if (emissive)
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                m.SetColor("_EmissionColor", c * 1.6f);
            }
            return m;
        }

        // Palette
        public static readonly Color Ground   = new Color(0.34f, 0.35f, 0.40f);
        public static readonly Color Ground2  = new Color(0.28f, 0.29f, 0.34f);
        public static readonly Color Player   = new Color(0.95f, 0.55f, 0.18f);
        public static readonly Color CorpseCol = new Color(0.45f, 0.62f, 0.95f);
        public static readonly Color Plate    = new Color(0.85f, 0.75f, 0.20f);
        public static readonly Color PlateHit = new Color(0.35f, 0.85f, 0.35f);
        public static readonly Color Gate     = new Color(0.80f, 0.28f, 0.28f);
        public static readonly Color Mover    = new Color(0.55f, 0.45f, 0.70f);
        public static readonly Color Exit     = new Color(0.30f, 0.90f, 0.45f);
        public static readonly Color Hazard   = new Color(0.85f, 0.20f, 0.25f);
        public static readonly Color Trampoline = new Color(0.95f, 0.45f, 0.85f);
        public static readonly Color Carry    = new Color(0.35f, 0.85f, 0.80f);
        public static readonly Color Shuttle  = new Color(0.95f, 0.65f, 0.20f);

        public static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Material mat, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            if (!collider) Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }
    }
}
