// 爆発で飛ぶボクセルの破片。簡単な陰影を付けた色で、_Fade を下げると透明になって消える。
Shader "Game/VFX/VoxelChunk"
{
    Properties
    {
        _Color ("Color", Color) = (0.4, 0.6, 0.25, 1)
        _Fade ("Fade", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Blend One OneMinusSrcAlpha
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Fade;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float lit = saturate(dot(normalize(i.normalWS), normalize(float3(0.4, 1.0, -0.3))));
                float3 c = _Color.rgb * (0.55 + 0.6 * lit);
                return float4(c * _Fade, _Fade);
            }
            ENDHLSL
        }
    }
}
