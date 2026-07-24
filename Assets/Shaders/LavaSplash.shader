Shader "NineLives/LavaSplash"
{
    // Dead-simple additive soft-sprite for the impact particles. Tinted by per-particle color
    // (COLOR) so a ParticleSystem's color-over-lifetime drives the look. Self-contained: no
    // textures, no URP surface/blend keyword juggling.
    Properties
    {
        [HDR] _TintColor ("Tint", Color) = (1.0, 0.5, 0.12, 1)
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" "PreviewType"="Plane" }

        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings   { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };

            CBUFFER_START(UnityPerMaterial)
                half4 _TintColor;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                o.uv = IN.uv;
                o.color = IN.color;
                return o;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float d = distance(IN.uv, float2(0.5, 0.5)) * 2.0;
                float a = saturate(1.0 - d);
                a *= a;                       // tighter falloff -> glowing core
                half4 c = IN.color * _TintColor;
                float outA = a * c.a;
                return half4(c.rgb * outA, outA);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
