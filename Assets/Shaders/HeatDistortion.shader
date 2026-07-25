Shader "Custom/URP/HeatDistortion"
{
    Properties
    {
        _NoiseTex ("Distortion Noise", 2D) = "gray" {}
        _Strength ("Distortion Strength", Range(0, 1)) = 0.1
        _Speed ("Distortion Speed", Float) = 1.0
        _Tiling ("Noise Tiling", Float) = 1.0
        _Color ("Tint", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }

        Pass
        {
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            TEXTURE2D(_CameraOpaqueTexture);
            SAMPLER(sampler_CameraOpaqueTexture);

            float4 _NoiseTex_ST;
            float _Strength;
            float _Speed;
            float _Tiling;
            float4 _Color;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS);
                o.uv = TRANSFORM_TEX(v.uv, _NoiseTex);
                o.screenPos = ComputeScreenPos(o.positionHCS);
                return o;
            }

            float4 frag (Varyings i) : SV_Target
            {
                float2 noiseUV = i.uv * _Tiling + float2(_Time.y * _Speed, 0);
                float2 noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV).rg * 2 - 1;

                float2 offset = noise * _Strength;

                float2 screenUV = (i.screenPos.xy / i.screenPos.w) + offset;

                float4 col = SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, screenUV);

                return col * _Color;
            }
            ENDHLSL
        }
    }
}
