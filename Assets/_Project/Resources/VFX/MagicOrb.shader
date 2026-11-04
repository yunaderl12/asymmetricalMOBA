// 魔法使いの通常攻撃の球。中心が白く、外周へ向かって紺色になる。こぼれる粒より手前に描く。
Shader "Game/VFX/MagicOrb"
{
    Properties
    {
        [HDR] _CoreColor ("Core", Color) = (2, 2.1, 2.4, 1)
        _RimColor ("Rim", Color) = (0.04, 0.08, 0.5, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Transparent+10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _CoreColor;
                float4 _RimColor;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; };

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
                float3 view = normalize(GetWorldSpaceViewDir(i.positionWS));
                float facing = saturate(dot(normalize(i.normalWS), view));
                float core = smoothstep(0.62, 0.97, facing);
                return float4(lerp(_RimColor.rgb, _CoreColor.rgb, core), 1.0);
            }
            ENDHLSL
        }
    }
}
