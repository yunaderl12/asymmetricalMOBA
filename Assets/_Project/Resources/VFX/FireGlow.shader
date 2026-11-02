// 常にカメラを向く光輪。_Pull でカメラ側へ寄せて、敵のメッシュに埋まらないようにする。
Shader "Game/VFX/FireGlow"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1, 0.5, 0.15, 1)
        _Pull ("Pull Toward Camera", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Pull;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 centerVS = TransformWorldToView(TransformObjectToWorld(float3(0.0, 0.0, 0.0)));
                float scale = length(float3(UNITY_MATRIX_M[0].x, UNITY_MATRIX_M[1].x, UNITY_MATRIX_M[2].x));
                float3 positionVS = centerVS + float3(v.positionOS.xy * scale, _Pull);
                o.positionCS = TransformWViewToHClip(positionVS);
                o.uv = v.uv;
                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float a = saturate(1.0 - length(i.uv - 0.5) * 2.0);
                a = a * a * (0.4 + 0.6 * a);
                // 加算だけだと青空の上でピンクになるので、背景も少し抑える。
                return float4(_Color.rgb * _Color.a * a, a * saturate(_Color.a) * 0.75);
            }
            ENDHLSL
        }
    }
}
