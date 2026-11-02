// 魔物の出現演出の地面の円。_Warn は外周から内側へ薄れる警告の円、_Open は渦を巻く闇のポータル。
Shader "Game/VFX/SpawnPortal"
{
    Properties
    {
        _Warn ("Warn", Range(0, 1)) = 0
        _Open ("Open", Range(0, 1.2)) = 0
        _Fade ("Fade", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            Offset -2, -2

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "FireNoise.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Warn;
                float _Open;
                float _Fade;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float2 p = (i.uv - 0.5) * 2.0;
                float r = length(p);

                // 警告の円: 外周が一番濃く、内側へ向かって消えていく
                float edge = 1.0 - smoothstep(0.97, 1.0, r);
                float gradient = pow(saturate(r), 2.5);
                float warn = _Warn * edge * gradient;
                float3 warnColor = lerp(float3(1.0, 0.3, 0.04), float3(1.7, 0.8, 0.2), gradient * gradient);
                float3 col = warnColor * warn;
                float alpha = warn * 0.75;

                // ポータル: 中心へ巻き込む渦
                float portalRadius = _Open * 0.9;
                if (portalRadius > 0.001)
                {
                    float angle = atan2(p.y, p.x);
                    float swirl = angle + 3.0 / (r + 0.3) - _Time.y * 2.2;
                    float2 q = float2(cos(swirl), sin(swirl)) * (0.6 + r * 2.0);
                    float noise = FireFbm(float3(q * 1.8, _Time.y * 0.35));
                    float3 dark = lerp(float3(0.015, 0.01, 0.03), float3(0.2, 0.05, 0.26), smoothstep(0.38, 0.72, noise));
                    float depth = saturate(r / portalRadius);
                    dark += float3(1.1, 0.32, 0.04) * smoothstep(0.6, 0.8, noise) * depth;
                    dark *= smoothstep(0.0, 0.5, depth);

                    float inside = 1.0 - smoothstep(portalRadius - 0.03, portalRadius, r);
                    col = lerp(col, dark, inside);
                    alpha = lerp(alpha, 0.97, inside);

                    // 縁の炎
                    float rim = 1.0 - smoothstep(0.0, 0.05 + noise * 0.07, abs(r - portalRadius));
                    col += float3(2.0, 0.7, 0.1) * rim;
                    alpha = max(alpha, rim);
                }
                return float4(col, alpha) * _Fade;
            }
            ENDHLSL
        }
    }
}
