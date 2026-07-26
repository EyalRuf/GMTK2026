Shader "Custom/URP/SpriteBlur"
{
    // Blurry sprite. Samples _MainTex in a 17-tap two-ring disc, so a sprite blurs itself in
    // place with no render passes and no draw-order surprises. Driven by SortingLayerBlur.cs,
    // which assigns one material of this shader per configured sorting layer.
    //
    // Blur is measured in source-texture pixels (_BlurTexels), and taps are alpha-weighted so
    // transparent edges don't bleed black.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Vertex Tint", Color) = (1, 1, 1, 1)
        _BlurTexels ("Blur Radius (texels)", Range(0, 64)) = 3
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "CanUseSpriteAtlas" = "True"
        }

        Pass
        {
            Name "SpriteBlur"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha

            Cull Off
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MainTex_TexelSize;
                float4 _Color;
                float _BlurTexels;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 color       : COLOR;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color * _Color;
                return output;
            }

            // Two rings of 8, the outer ring rotated half a step off the inner one.
            static const float2 kRing[16] =
            {
                float2( 1.000,  0.000), float2( 0.707,  0.707), float2( 0.000,  1.000), float2(-0.707,  0.707),
                float2(-1.000,  0.000), float2(-0.707, -0.707), float2( 0.000, -1.000), float2( 0.707, -0.707),
                float2( 0.924,  0.383), float2( 0.383,  0.924), float2(-0.383,  0.924), float2(-0.924,  0.383),
                float2(-0.924, -0.383), float2(-0.383, -0.924), float2( 0.383, -0.924), float2( 0.924, -0.383)
            };

            float4 frag(Varyings input) : SV_Target
            {
                float2 texel = _MainTex_TexelSize.xy * _BlurTexels;

                // Alpha-weighted (premultiplied) accumulation, unpremultiplied at the end.
                float4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float3 rgb = c.rgb * c.a;
                float alpha = c.a;
                float weight = 1.0;

                [unroll]
                for (int i = 0; i < 8; i++)
                {
                    float2 o = kRing[i] * texel * 0.5;
                    float4 s = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + o);
                    rgb += s.rgb * s.a * 0.75;
                    alpha += s.a * 0.75;
                    weight += 0.75;
                }

                [unroll]
                for (int j = 8; j < 16; j++)
                {
                    float2 o = kRing[j] * texel;
                    float4 s = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + o);
                    rgb += s.rgb * s.a * 0.4;
                    alpha += s.a * 0.4;
                    weight += 0.4;
                }

                float4 outC;
                outC.rgb = rgb / max(alpha, 1e-4);
                outC.a = alpha / weight;
                return outC * input.color;
            }

            ENDHLSL
        }
    }

    FallBack Off
}
