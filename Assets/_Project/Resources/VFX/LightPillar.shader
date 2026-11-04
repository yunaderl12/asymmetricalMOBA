// 光の柱。上へ行くほど薄くなり、輪郭は柔らかく消える。
Shader "Game/VFX/LightPillar"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (2, 1.6, 0.8, 1)
        _Fade ("Fade", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Fade;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; float2 uv : TEXCOORD2; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv;
                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float3 view = normalize(GetWorldSpaceViewDir(i.positionWS));
                float facing = abs(dot(normalize(i.normalWS), view));
                float height = 1.0 - i.uv.y;
                float strength = facing * facing * height * sqrt(height) * _Fade;
                return float4(_Color.rgb * strength, 0.0);
            }
            ENDHLSL
        }
    }
}
