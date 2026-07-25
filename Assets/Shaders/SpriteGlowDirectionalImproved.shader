Shader "Custom/URP/SpriteGlowDirectionalImproved"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)

        [HDR] _GlowColor ("Glow Color", Color) = (1, 0.35, 0.05, 1)
        _GlowIntensity ("Glow Intensity", Range(0, 20)) = 4

        _GlowWidth ("Glow Width", Range(0.001, 0.25)) = 0.05
        _GlowFalloff ("Glow Falloff", Range(0.25, 8)) = 2
        _CoreGlow ("Sprite Rim Glow", Range(0, 10)) = 1

        _Direction
        (
            "Glow Direction (1 Right, -1 Left, 2 Up, -2 Down)",
            Float
        ) = 1

        _AlphaCutoff ("Alpha Detection Threshold", Range(0, 1)) = 0.01
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
            Name "SpriteGlow"
            Tags { "LightMode" = "UniversalForward" }

            // Premultiplied transparency gives cleaner glowing edges.
            Blend One OneMinusSrcAlpha

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
                float4 _Color;
                float4 _GlowColor;

                float _GlowIntensity;
                float _GlowWidth;
                float _GlowFalloff;
                float _CoreGlow;
                float _Direction;
                float _AlphaCutoff;
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

                output.positionHCS =
                    TransformObjectToHClip(input.positionOS.xyz);

                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color * _Color;

                return output;
            }

            float2 GetGlowDirection()
            {
                if (_Direction > 1.5)
                    return float2(0.0, 1.0);   // Up

                if (_Direction < -1.5)
                    return float2(0.0, -1.0);  // Down

                if (_Direction < 0.0)
                    return float2(-1.0, 0.0);  // Left

                return float2(1.0, 0.0);       // Right
            }

            float SampleAlpha(float2 uv)
            {
                return SAMPLE_TEXTURE2D(
                    _MainTex,
                    sampler_MainTex,
                    uv
                ).a;
            }

            float CalculateDirectionalGlow(float2 uv, float2 direction)
            {
                float glow = 0.0;

                // Search from the transparent pixel back toward the sprite.
                // A visible alpha sample means this pixel lies within the
                // directional glow distance of the sprite silhouette.

                [unroll]
                for (int sampleIndex = 1; sampleIndex <= 12; sampleIndex++)
                {
                    float normalizedDistance = sampleIndex / 12.0;

                    float2 sampleUV =
                        uv - direction *
                        (_GlowWidth * normalizedDistance);

                    float sampledAlpha = SampleAlpha(sampleUV);

                    float falloff =
                        pow(
                            1.0 - normalizedDistance,
                            _GlowFalloff
                        );

                    glow = max(
                        glow,
                        sampledAlpha * falloff
                    );
                }

                return saturate(glow);
            }

            float CalculateInnerRim(
                float2 uv,
                float currentAlpha,
                float2 direction
            )
            {
                // Look toward the glowing side.
                // If alpha drops there, the current pixel is near that edge.

                float2 edgeSampleUV =
                    uv + direction * (_GlowWidth * 0.2);

                float nearbyAlpha = SampleAlpha(edgeSampleUV);

                return saturate(currentAlpha - nearbyAlpha);
            }

            float4 frag(Varyings input) : SV_Target
            {
                float4 textureColor =
                    SAMPLE_TEXTURE2D(
                        _MainTex,
                        sampler_MainTex,
                        input.uv
                    );

                float4 baseColor = textureColor * input.color;
                float currentAlpha = baseColor.a;

                float2 glowDirection = GetGlowDirection();

                float outerGlow = 0.0;

                // Only calculate the exterior glow on transparent pixels.
                if (currentAlpha <= _AlphaCutoff)
                {
                    outerGlow = CalculateDirectionalGlow(
                        input.uv,
                        glowDirection
                    );
                }

                float innerRim = CalculateInnerRim(
                    input.uv,
                    currentAlpha,
                    glowDirection
                );

                float3 hdrGlow =
                    _GlowColor.rgb *
                    _GlowIntensity;

                // Preserve the sprite, but brighten its directional edge.
                float3 finalRGB =
                    baseColor.rgb +
                    hdrGlow * innerRim * _CoreGlow;

                // Add exterior directional glow.
                finalRGB += hdrGlow * outerGlow;

                float finalAlpha = saturate(
                    currentAlpha +
                    outerGlow * _GlowColor.a
                );

                // Premultiply the ordinary sprite color.
                // Keep HDR glow strong enough to trigger Bloom.
                float3 premultipliedBase =
                    baseColor.rgb * currentAlpha;

                float3 premultipliedGlow =
                    hdrGlow *
                    (
                        outerGlow +
                        innerRim * _CoreGlow * currentAlpha
                    );

                return float4(
                    premultipliedBase + premultipliedGlow,
                    finalAlpha
                );
            }

            ENDHLSL
        }
    }

    FallBack Off
}