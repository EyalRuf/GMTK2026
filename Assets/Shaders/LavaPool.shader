Shader "NineLives/LavaPool"
{
    // Fully standalone lava-pool material: drop it on any cube, scale the cube, done. No script,
    // no relationship to LavaWaterfall — purely an artist-placed decorative surface.
    //
    // Box-aware tiling: rather than trusting the mesh's per-face UVs (Unity's built-in Cube UVs
    // aren't consistent enough across faces for this), we recover the object's world-space scale
    // from unity_ObjectToWorld's column lengths and multiply the object-space vertex position by
    // it. That gives a coordinate that scales 1:1 with world size on whichever face a fragment
    // belongs to (picked via the dominant axis of the object-space normal), so lava detail density
    // stays constant whether the cube is a small puddle or a big lake. Mesh UV is kept only for
    // the edge/side fade, which just needs a 0..1 coordinate across each face.
    Properties
    {
        [HDR] _ColorHot     ("Hot Color", Color)               = (1.0, 0.55, 0.10, 1)
        [HDR] _ColorCool    ("Cool Color", Color)               = (0.4, 0.03, 0.0, 1)
        [HDR] _EdgeColor    ("Edge / Fresnel Glow Color", Color) = (1.0, 0.85, 0.30, 1)
        _FlowSpeed          ("Lava Speed", Float)                = 0.6
        _Tiling             ("Lava Scale / Tiling (repeats per world unit)", Float) = 0.5
        _NoiseStrength      ("Surface Distortion", Range(0,1))   = 0.25
        _Emission           ("Emission Strength", Float)         = 2.0
        _BubbleIntensity    ("Bubbling Intensity", Range(0,2))   = 0.5
        _EdgeSoftness       ("Edge Softness", Range(0,0.5))      = 0.12
        _FresnelPower       ("Fresnel Power", Range(0.1,8))      = 2.5
        _FresnelIntensity   ("Fresnel Glow Intensity", Range(0,3)) = 0.6
        _DepthFade          ("Depth / Volume Fade", Range(0,1))  = 0.4
        _HeightVariation    ("Surface Height Variation (world units)", Range(0,0.5)) = 0.05
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }

        Cull Back

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 normalOS    : TEXCOORD2;
                float2 uv          : TEXCOORD3;
                float3 objectScale : TEXCOORD4;
                float3 positionOSraw : TEXCOORD5;
            };

            CBUFFER_START(UnityPerMaterial)
                half4  _ColorHot;
                half4  _ColorCool;
                half4  _EdgeColor;
                float  _FlowSpeed;
                float  _Tiling;
                float  _NoiseStrength;
                float  _Emission;
                float  _BubbleIntensity;
                float  _EdgeSoftness;
                float  _FresnelPower;
                float  _FresnelIntensity;
                float  _DepthFade;
                float  _HeightVariation;
            CBUFFER_END

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

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

            float3 GetObjectScale()
            {
                return float3(
                    length(float3(unity_ObjectToWorld._m00, unity_ObjectToWorld._m10, unity_ObjectToWorld._m20)),
                    length(float3(unity_ObjectToWorld._m01, unity_ObjectToWorld._m11, unity_ObjectToWorld._m21)),
                    length(float3(unity_ObjectToWorld._m02, unity_ObjectToWorld._m12, unity_ObjectToWorld._m22)));
            }

            // World-scaled 2D coordinate on whichever face this vertex/fragment belongs to, so
            // noise density stays constant regardless of the cube's scale.
            float2 FacePlaneCoord(float3 positionOS, float3 normalOS, float3 objectScale)
            {
                float3 local = positionOS * objectScale;
                float3 n = abs(normalOS);
                if (n.y >= n.x && n.y >= n.z) return local.xz; // top / bottom
                if (n.x >= n.y && n.x >= n.z) return local.zy; // left / right
                return local.xy;                               // front / back
            }

            Varyings vert(Attributes IN)
            {
                Varyings o;

                float3 objectScale = GetObjectScale();
                float3 posOS = IN.positionOS.xyz;

                // Optional gentle bubbling: displace the top face along its object-space normal.
                // Convert the desired world-space displacement back to object space so it reads
                // the same regardless of how tall the box is scaled.
                if (IN.normalOS.y > 0.5)
                {
                    float2 facePos = FacePlaneCoord(posOS, IN.normalOS, objectScale) * _Tiling;
                    float t = _Time.y * _FlowSpeed;
                    float h = fbm(facePos * 0.5 - t * 0.3) - 0.5;
                    float worldDisp = h * _HeightVariation;
                    posOS.y += worldDisp / max(objectScale.y, 0.0001);
                }

                VertexPositionInputs vpi = GetVertexPositionInputs(posOS);
                o.positionHCS = vpi.positionCS;
                o.positionWS = vpi.positionWS;
                o.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                o.normalOS = IN.normalOS;
                o.uv = IN.uv;
                o.objectScale = objectScale;
                o.positionOSraw = IN.positionOS.xyz;
                return o;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 facePos = FacePlaneCoord(IN.positionOSraw, IN.normalOS, IN.objectScale);
                float2 p = facePos * _Tiling;
                float t = _Time.y * _FlowSpeed;

                float distort = (fbm(p * 2.0 + t * 0.1) - 0.5) * _NoiseStrength;
                float flow  = fbm(float2(p.x + distort, p.y - t));
                flow += 0.5 * fbm(float2(p.x * 2.0 + distort, p.y * 2.0 - t * 1.6));
                flow = saturate(flow);

                half3 col = lerp(_ColorCool.rgb, _ColorHot.rgb, flow);

                // Sparse pulsing bright spots -> bubbling.
                float bubbleNoise = fbm(p * 3.0 + t * 0.4);
                float bubble = smoothstep(0.82, 0.95, bubbleNoise) * _BubbleIntensity;
                col += _EdgeColor.rgb * bubble;

                // Fresnel glow around edges/silhouette.
                float3 viewDirWS = normalize(GetWorldSpaceViewDir(IN.positionWS));
                float fresnel = pow(1.0 - saturate(dot(normalize(IN.normalWS), viewDirWS)), _FresnelPower);
                col += _EdgeColor.rgb * fresnel * _FresnelIntensity;

                // Side faces: darken toward the base to fake a molten body beneath the glowing
                // surface instead of a hollow shell (cheap stand-in for real volumetrics).
                float isSide = 1.0 - saturate(abs(IN.normalOS.y) * 2.0);
                float depthT = saturate(1.0 - IN.uv.y);
                col = lerp(col, col * (1.0 - _DepthFade), isSide * depthT);

                // Soft fade near each face's edges so pools don't look like hard-edged decals.
                float2 edgeDist2 = min(IN.uv, 1.0 - IN.uv);
                float edgeDist = min(edgeDist2.x, edgeDist2.y);
                float edgeFade = smoothstep(0.0, _EdgeSoftness, edgeDist);
                col *= lerp(0.4, 1.0, edgeFade);

                col *= _Emission;

                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
