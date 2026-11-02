// 火球本体。中心は白熱、外周はオレンジ。_Taper で後ろ側をすぼめて涙滴形にする（前方は +Z）。
Shader "Game/VFX/FireOrb"
{
    Properties
    {
        _Taper ("Tail Taper", Range(0, 1)) = 0
        _Energy ("Energy", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "FireNoise.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Taper;
                float _Energy;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 p = v.positionOS.xyz;
                // プリミティブの球は半径0.5。後ろ半分だけ細くする。
                float back = saturate(-p.z * 2.0);
                p.xy *= lerp(1.0, 1.0 - _Taper * 0.92, back * back);
                o.positionOS = p;
                o.positionWS = TransformObjectToWorld(p);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float3 viewDir = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float rim = 1.0 - saturate(dot(n, viewDir));
                float noise = FireFbm(i.positionOS * 4.0 + float3(0.0, 0.0, _Time.y * 3.5));
                float t = rim + (noise - 0.5) * 0.6;

                float3 col = lerp(float3(2.2, 1.9, 1.3), float3(1.6, 0.8, 0.15), smoothstep(0.0, 0.35, t));
                col = lerp(col, float3(1.2, 0.3, 0.03), smoothstep(0.35, 0.8, t));
                float a = (1.0 - smoothstep(0.8, 1.05, t)) * _Energy;
                return float4(col * a, a);
            }
            ENDHLSL
        }
    }
}
