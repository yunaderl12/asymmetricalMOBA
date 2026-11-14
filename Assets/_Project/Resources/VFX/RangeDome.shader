// 塔の射程を示すごく薄い球。正面はほぼ透けて、縁（球の輪郭）ほど色が出る。
// 外から見るときは外側の面だけ、射程の中にいるときは内側の面だけを描く（_Cull。SpawnerRangeDome が切り替える）。
Shader "Game/VFX/RangeDome"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.35, 0.4, 1)
        _Base ("Base Alpha", Range(0, 0.2)) = 0.025
        _Rim ("Rim Alpha", Range(0, 1)) = 0.22
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Base;
                float _Rim;
                float _RimPower;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float3 view = normalize(GetCameraPositionWS() - i.positionWS);
                float facing = abs(dot(normalize(i.normalWS), view));
                float rim = pow(1.0 - facing, _RimPower);
                return float4(_Color.rgb, saturate(_Base + _Rim * rim));
            }
            ENDHLSL
        }
    }
}
