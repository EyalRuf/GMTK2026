Shader "Custom/URP/SpriteOutlineFull"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)

        _OutlineColor ("Outline Color", Color) = (0, 0, 0, 1)
        _OutlineThickness ("Outline Thickness", Range(0, 16)) = 2
        _OutlineSoftness ("Outline Softness", Range(0, 1)) = 0

        _AlphaThreshold
        (
            "Alpha Detection Threshold",
            Range(0, 1)
        ) = 0.01
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
            Name "SpriteOutline"
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

            float4 _MainTex_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float4 _OutlineColor;

                float _OutlineThickness;
                float _OutlineSoftness;
                float _AlphaThreshold;
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

                output.uv =
                    TRANSFORM_TEX(input.uv, _MainTex);

                output.color =
                    input.color * _Color;

                return output;
            }

            float SampleAlpha(float2 uv)
            {
                return SAMPLE_TEXTURE2D(
                    _MainTex,
                    sampler_MainTex,
                    uv
                ).a;
            }

            float FindNearbyAlpha(float2 uv)
            {
                float nearbyAlpha = 0.0;

                // Directions around the sprite.
                static const float2 directions[16] =
                {
                    float2( 1.0000,  0.0000),
                    float2( 0.9239,  0.3827),
                    float2( 0.7071,  0.7071),
                    float2( 0.3827,  0.9239),

                    float2( 0.0000,  1.0000),
                    float2(-0.3827,  0.9239),
                    float2(-0.7071,  0.7071),
                    float2(-0.9239,  0.3827),

                    float2(-1.0000,  0.0000),
                    float2(-0.9239, -0.3827),
                    float2(-0.7071, -0.7071),
                    float2(-0.3827, -0.9239),

                    float2( 0.0000, -1.0000),
                    float2( 0.3827, -0.9239),
                    float2( 0.7071, -0.7071),
                    float2( 0.9239, -0.3827)
                };

                // Multiple distances fill the entire outline rather than
                // producing only a thin ring at the maximum radius.
                [unroll]
                for (int stepIndex = 1; stepIndex <= 4; stepIndex++)
                {
                    float stepDistance =
                        stepIndex / 4.0;

                    float2 sampleDistance =
                        _MainTex_TexelSize.xy *
                        _OutlineThickness *
                        stepDistance;

                    [unroll]
                    for (int directionIndex = 0;
                         directionIndex < 16;
                         directionIndex++)
                    {
                        float2 sampleUV =
                            uv +
                            directions[directionIndex] *
                            sampleDistance;

                        nearbyAlpha = max(
                            nearbyAlpha,
                            SampleAlpha(sampleUV)
                        );
                    }
                }

                return nearbyAlpha;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float4 textureColor =
                    SAMPLE_TEXTURE2D(
                        _MainTex,
                        sampler_MainTex,
                        input.uv
                    );

                float4 spriteColor =
                    textureColor * input.color;

                float currentAlpha = spriteColor.a;

                // Visible sprite pixels remain unchanged.
                if (currentAlpha > _AlphaThreshold)
                {
                    return spriteColor;
                }

                float nearbyAlpha =
                    FindNearbyAlpha(input.uv);

                float hardOutline =
                    step(
                        _AlphaThreshold,
                        nearbyAlpha
                    );

                float softOutline =
                    smoothstep(
                        _AlphaThreshold,
                        1.0,
                        nearbyAlpha
                    );

                // Softness 0 gives a solid graphic outline.
                // Softness 1 gives a softer antialiased edge.
                float outlineMask =
                    lerp(
                        hardOutline,
                        softOutline,
                        _OutlineSoftness
                    );

                float4 outlineColor =
                    _OutlineColor;

                outlineColor.a *= outlineMask;

                return outlineColor;
            }

            ENDHLSL
        }
    }

    FallBack Off
}