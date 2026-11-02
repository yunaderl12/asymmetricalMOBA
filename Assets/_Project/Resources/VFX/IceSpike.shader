// 氷柱。面ごとに光り方が変わる平面シェーディング。_Melt で先端から溶ける（0〜1）。
Shader "Game/VFX/IceSpike"
{
    Properties
    {
        _Melt ("Melt", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Blend One OneMinusSrcAlpha
            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "FireNoise.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Melt;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float height : TEXCOORD2;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.height = v.positionOS.y;
                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float melting = 0.0;
                if (_Melt > 0.001)
                {
                    float cut = 1.0 - _Melt * 1.2 + (FireFbm(i.positionWS * 4.0) - 0.5) * 0.25;
                    float d = cut - i.height;
                    clip(d);
                    melting = 1.0 - saturate(d / 0.1);
                }

                float3 n = normalize(i.normalWS);
                float3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float lit = saturate(dot(n, normalize(float3(0.4, 0.8, 0.3)))) * 0.5 + 0.5;
                float rim = pow(1.0 - abs(dot(n, v)), 3.0);
                float height = saturate(i.height);

                float3 deep = float3(0.2, 0.58, 1.0);
                float3 pale = float3(0.6, 0.92, 1.0);
                float3 color = lerp(deep, pale, saturate(height * 0.55 + lit * 0.55));
                color *= 0.88 + 0.22 * lit;
                color += float3(0.7, 0.95, 1.0) * rim * 0.9;
                color += float3(1.0, 1.0, 1.0) * melting;

                const float alpha = 0.92;
                return float4(saturate(color) * alpha, alpha);
            }
            ENDHLSL
        }
    }
}
