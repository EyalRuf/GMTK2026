Shader "Custom/URP/SpriteScreenTint"
{
    // Sprite shader that ADDS a tint into the sprite instead of multiplying it, so a black
    // sprite can be recolored. Blend: rgb = tex + _Tint * (1 - tex) (screen blend).
    //   _Tint = black  -> identity (sprite unchanged)
    //   _Tint = orange -> black areas go orange, white areas stay white.
    // Drive _Tint per-object via MaterialPropertyBlock (CatTint.cs) so one material can serve
    // many objects independently.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Vertex Tint", Color) = (1, 1, 1, 1)
        _Tint ("Screen Tint", Color) = (0, 0, 0, 1)
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
            Name "SpriteScreenTint"
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
                float4 _Color;
                float4 _Tint;
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

            float4 frag(Varyings input) : SV_Target
            {
                float4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;

                // Screen blend the tint into the color; scaled by the tint's own alpha so the
                // strength can be pushed via _Tint alpha too. Leaves the sprite's alpha intact.
                c.rgb += _Tint.rgb * _Tint.a * (1.0 - c.rgb);

                return c;
            }

            ENDHLSL
        }
    }

    FallBack Off
}
