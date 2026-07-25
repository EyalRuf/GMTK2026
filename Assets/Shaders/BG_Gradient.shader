Shader "Custom/URP/TwoColorGradient"
{
    Properties
    {
        _TopColor ("Top Color", Color) = (0.1, 0.2, 0.5, 1)
        _BottomColor ("Bottom Color", Color) = (0.8, 0.3, 0.1, 1)

        _GradientPosition
        (
            "Gradient Position",
            Range(0, 1)
        ) = 0.5

        _GradientWidth
        (
            "Gradient Width",
            Range(0.001, 1)
        ) = 1.0

        _GradientPower
        (
            "Gradient Curve",
            Range(0.1, 5)
        ) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Gradient"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _TopColor;
                float4 _BottomColor;

                float _GradientPosition;
                float _GradientWidth;
                float _GradientPower;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;

                output.positionHCS =
                    TransformObjectToHClip(input.positionOS.xyz);

                output.uv = input.uv;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float halfWidth = max(_GradientWidth * 0.5, 0.0001);

                float gradient =
                    smoothstep(
                        _GradientPosition - halfWidth,
                        _GradientPosition + halfWidth,
                        input.uv.y
                    );

                gradient = pow(
                    saturate(gradient),
                    _GradientPower
                );

                return lerp(
                    _BottomColor,
                    _TopColor,
                    gradient
                );
            }

            ENDHLSL
        }
    }

    FallBack Off
}