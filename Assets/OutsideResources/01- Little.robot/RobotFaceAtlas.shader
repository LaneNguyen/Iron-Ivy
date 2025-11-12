Shader "Unlit/RobotFaceAtlas"
{
    Properties
    {
        _BaseMap("FaceAtlas", 2D) = "white" {}
        _Rows("Rows", Float) = 4
        _Cols("Cols", Float) = 4
        _Frame("Frame", Float) = 0
        _UVOffset("UV Offset", Vector) = (0,0,0,0)
        _Tint("Tint", Color) = (1,1,1,1)
        _Emission("Emission", Range(0,5)) = 1
    }
    SubShader
    {
        Tags{ "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags{"LightMode"="UniversalForward"}
            Cull Off ZWrite On ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            float4 _BaseMap_ST;
            float _Rows, _Cols, _Frame, _Emission;
            float4 _UVOffset;
            float4 _Tint;

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings  { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            float2 AtlasUV(float2 uv, float rows, float cols, float frame, float2 uvOff)
            {
                // frame index → (r,c)
                float fi = max(frame, 0);
                float r = floor(fi / cols);
                float c = fi - r * cols;

                float2 cell = float2(1.0/cols, 1.0/rows);
                float2 baseUV = uv * cell + float2(c * cell.x, (rows - 1 - r) * cell.y); // origin at bottom-left
                return baseUV + uvOff * cell; // tiny offset for eye-look
            }

            half4 frag (Varyings i) : SV_Target
            {
                float2 uv = AtlasUV(i.uv, _Rows, _Cols, _Frame, _UVOffset.xy);
                half4 col = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _Tint;
                // simple emissive look
                col.rgb *= _Emission;
                return col;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
