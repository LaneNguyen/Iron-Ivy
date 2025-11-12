Shader "Unlit/RobotFaceAtlas_Simple"
{
    Properties
    {
        _BaseMap("FaceAtlas", 2D) = "white" {}
        _Tint("Tint", Color) = (1,1,1,1)
        _Emission("Emission", Range(0,5)) = 1
    }
    SubShader
    {
        Tags{ "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "UniversalForward"
            Tags{"LightMode"="UniversalForward"}
            Cull Off ZWrite On ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            float4 _Tint;
            float _Emission;

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings  { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _Tint;
                col.rgb *= _Emission;
                return col;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
