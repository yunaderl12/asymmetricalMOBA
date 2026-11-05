// 魔物の頭の上に出る「！」。影響を受けず、遮られても見えるように手前に描く。
Shader "Game/VFX/AlertMark"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.82, 0.08, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+50" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
            CBUFFER_END

            float4 vert(float4 positionOS : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(positionOS.xyz);
            }

            float4 frag() : SV_Target
            {
                return _Color;
            }
            ENDHLSL
        }
    }
}
