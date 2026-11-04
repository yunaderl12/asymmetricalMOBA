// Explosion の着弾点に出る魔法陣。逆向きに回る文字の帯と、太さの違う輪を重ねる。_Fill は内側を満たす光の強さ。
Shader "Game/VFX/RuneCircle"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (0.55, 1.5, 1.7, 1)
        _Fill ("Fill", Range(0, 1)) = 0.1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-5" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
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

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Fill;
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

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float Ring(float r, float center, float halfWidth)
            {
                return 1.0 - smoothstep(halfWidth * 0.6, halfWidth, abs(r - center));
            }

            // 帯を区切って、四角い線を組み合わせた文字のような模様を作る。
            float Runes(float angle, float r, float inner, float outer, float cells, float spin)
            {
                float v = (r - inner) / (outer - inner);
                if (v < 0.0 || v > 1.0) return 0.0;
                float u = frac((angle + spin) / TWO_PI) * cells;
                float cell = floor(u);
                float2 local = float2(frac(u), v);
                float2 sub = floor(local * float2(4.0, 3.0));
                float on = step(0.48, Hash(float2(cell, sub.x * 7.0 + sub.y * 13.0)));
                float2 inCell = frac(local * float2(4.0, 3.0));
                float line_ = step(0.28, inCell.x) * step(inCell.x, 0.72) + step(0.28, inCell.y) * step(inCell.y, 0.72);
                float gap = step(0.1, frac(u)) * step(frac(u), 0.9) * step(0.12, v) * step(v, 0.88);
                return saturate(on * saturate(line_) * gap);
            }

            float4 frag(Varyings i) : SV_Target
            {
                float2 p = (i.uv - 0.5) * 2.0;
                float r = length(p);
                float angle = atan2(p.y, p.x);
                float time = _Time.y;

                float lines = Ring(r, 0.97, 0.022) + Ring(r, 0.8, 0.011) + Ring(r, 0.6, 0.016) + Ring(r, 0.42, 0.009) + Ring(r, 0.25, 0.012);
                float runes = Runes(angle, r, 0.82, 0.95, 36.0, time * 0.35) + Runes(angle, r, 0.45, 0.58, 20.0, -time * 0.6);
                float ticks = step(0.9, frac(angle / TWO_PI * 48.0 + time * 0.2)) * step(0.62, r) * step(r, 0.78);
                float fill = _Fill * (1.0 - smoothstep(0.92, 1.0, r)) * (0.45 + 0.55 * r);

                float shape = saturate(lines + runes + ticks * 0.8);
                float3 rgb = lerp(_Color.rgb, float3(2.2, 2.6, 2.8), shape) * shape + _Color.rgb * fill;
                float a = saturate(shape + fill) * _Color.a;
                return float4(rgb * _Color.a, a);
            }
            ENDHLSL
        }
    }
}
