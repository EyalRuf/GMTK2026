Shader "NineLives/LavaWaterfall"
{
    // Stylized flowing-lava waterfall. Procedural (no textures), URP unlit + transparent.
    //
    // The mesh is a stretched quad; its LOCAL height changes with the raycast hit distance,
    // but the flow must NOT stretch with it. So the LavaWaterfall component pushes the world
    // length into _Length, and we scale the vertical scroll coordinate by (_Length * _Tiling)
    // to keep texel density constant no matter how tall the quad is.
    //
    // "fall" runs 0 at the source (top) to 1 at the impact point (bottom) — all the fades and
    // the flow direction are expressed in that space so the shader is mesh-scale independent.
    Properties
    {
        [HDR] _ColorHot   ("Hot Color", Color)               = (1.0, 0.55, 0.10, 1)
        [HDR] _ColorCool  ("Cool Color", Color)              = (0.55, 0.05, 0.0, 1)
        [HDR] _EdgeColor  ("Edge Glow Color", Color)         = (1.0, 0.85, 0.30, 1)
        _FlowSpeed        ("Flow Speed", Float)              = 1.2
        _Tiling           ("Tiling (pattern per unit)", Float) = 0.5
        _Length           ("Waterfall Length (set by script)", Float) = 5.0
        _NoiseScale       ("Noise Scale", Float)             = 3.0
        _NoiseStrength    ("Distortion Strength", Range(0,1)) = 0.25
        _Emission         ("Emission Intensity", Float)      = 2.5
        _FadeRegion       ("Bottom Fade Region", Range(0.001,1)) = 0.3
        _TopFade          ("Top Fade", Range(0,0.5))         = 0.06
        _EdgeGlow         ("Edge Glow Width", Range(0,0.5))  = 0.18
        _SideSoft         ("Side Softness", Range(0,0.5))    = 0.12
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; };

            CBUFFER_START(UnityPerMaterial)
                half4  _ColorHot;
                half4  _ColorCool;
                half4  _EdgeColor;
                float  _FlowSpeed;
                float  _Tiling;
                float  _Length;
                float  _NoiseScale;
                float  _NoiseStrength;
                float  _Emission;
                float  _FadeRegion;
                float  _TopFade;
                float  _EdgeGlow;
                float  _SideSoft;
            CBUFFER_END

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // value noise
            float vnoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float fbm(float2 p)
            {
                float v = 0.0;
                float amp = 0.5;
                for (int i = 0; i < 4; i++)
                {
                    v += amp * vnoise(p);
                    p *= 2.0;
                    amp *= 0.5;
                }
                return v;
            }

            Varyings vert(Attributes IN)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                o.uv = IN.uv;
                return o;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // 0 at source (top), 1 at impact (bottom). Unity quad: uv.y=1 is +Y (top).
                float fall = 1.0 - IN.uv.y;
                float t = _Time.y * _FlowSpeed;

                // Vertical scroll coordinate — density constant regardless of quad scale.
                float vy = fall * _Length * _Tiling;

                // Horizontal distortion so the stream wobbles instead of scrolling in straight lines.
                float distort = (fbm(float2(IN.uv.x * _NoiseScale, vy - t)) - 0.5) * _NoiseStrength;

                // Two octaves of downward flow at different speeds -> molten churn.
                float flow  = fbm(float2(IN.uv.x * _NoiseScale + distort, vy - t));
                flow += 0.5 * fbm(float2(IN.uv.x * _NoiseScale * 2.0 + distort, vy * 2.0 - t * 1.7));
                flow = saturate(flow);

                half3 col = lerp(_ColorCool.rgb, _ColorHot.rgb, flow);

                // Edge glow along the two vertical sides.
                float sideDist = min(IN.uv.x, 1.0 - IN.uv.x);
                float edge = 1.0 - smoothstep(0.0, _EdgeGlow, sideDist);
                col += _EdgeColor.rgb * edge;

                col *= _Emission;

                // --- Alpha: soft everywhere, no clipping ---
                float sideAlpha = smoothstep(0.0, _SideSoft, sideDist);
                float topAlpha  = smoothstep(0.0, _TopFade, fall);

                // Wobbly dissolving bottom edge rather than a hard cut.
                float dissolve = fbm(float2(IN.uv.x * _NoiseScale, vy * 0.5 - t)) - 0.5;
                float fallEdge = fall + dissolve * _FadeRegion;
                float botAlpha = 1.0 - smoothstep(1.0 - _FadeRegion, 1.0, fallEdge);

                float alpha = sideAlpha * topAlpha * botAlpha;

                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
