// 地面に残る氷。ひび割れと光沢を持つ。_Reveal で中心から広がり、_Alpha で消える。_Flash は出現の瞬間のひびの光。
Shader "Game/VFX/IceField"
{
    Properties
    {
        _Reveal ("Reveal", Range(0, 1)) = 1
        _Alpha ("Alpha", Range(0, 1)) = 1
        _Flash ("Flash", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-6" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "FireNoise.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Reveal;
                float _Alpha;
                float _Flash;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = v.uv;
                return o;
            }

            float2 CellPoint(float2 cell)
            {
                float a = FireHash(float3(cell, 1.0));
                float b = FireHash(float3(cell, 7.0));
                return float2(a, b);
            }

            // ボロノイのセル境界からの距離。小さいほどひびに近い。
            float CrackDistance(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                float first = 8.0;
                float second = 8.0;
                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 o = float2(x, y);
                        float d = length(o + CellPoint(cell + o) - f);
                        if (d < first) { second = first; first = d; }
                        else if (d < second) { second = d; }
                    }
                }
                return second - first;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float r = length(i.uv - 0.5) * 2.0;
                float2 p = i.positionWS.xz;
                float wobble = FireFbm(float3(p * 0.7, 0.0)) - 0.5;
                float edge = r + wobble * 0.3;
                float mask = 1.0 - smoothstep(_Reveal - 0.1, _Reveal, edge);
                clip(mask - 0.01);

                float cracks = 1.0 - smoothstep(0.0, 0.07, CrackDistance(p * 0.8 + wobble));
                float fine = 1.0 - smoothstep(0.0, 0.035, CrackDistance(p * 2.1 + wobble * 2.0));
                float frostEdge = smoothstep(_Reveal - 0.3, _Reveal - 0.02, edge);

                float3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float sheen = pow(saturate(1.0 - v.y), 2.0);
                float patches = FireFbm(float3(p * 0.9, 3.0));

                float3 color = lerp(float3(0.3, 0.64, 1.0), float3(0.6, 0.87, 1.0), patches);
                color += float3(0.5, 0.6, 0.6) * sheen * 0.5;
                color = lerp(color, float3(0.9, 0.98, 1.0), frostEdge * 0.6);
                color = lerp(color, float3(1.0, 1.0, 1.0), saturate(cracks * 0.85 + fine * 0.35));
                color += float3(0.5, 0.9, 1.0) * _Flash * (0.4 + cracks);

                float alpha = mask * _Alpha * lerp(0.8, 0.95, saturate(frostEdge + cracks + sheen * 0.4));
                return float4(saturate(color) * alpha, alpha);
            }
            ENDHLSL
        }
    }
}
