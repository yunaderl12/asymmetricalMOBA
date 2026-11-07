// ワープの門。いくつもの泡（メタボール）がくっついた形の穴で、縁がピンク→紫→水色→緑のネオンに光る。
// _Linked=1 で、中に反対側の門のカメラの絵（_PortalTex、画面の座標で読む）を映す。表からも裏からも映す。つながる前は星の空。
// _Open で泡が小さな粒から育ってくっつく（0=閉じている 1=開ききった）。
Shader "Game/VFX/WarpGate"
{
    Properties
    {
        _PortalTex ("Portal View", 2D) = "black" {}
        _Open ("Open", Range(0, 1)) = 1
        _Linked ("Linked", Range(0, 1)) = 0
        _Seed ("Seed", Float) = 0
        _Size ("Size (width, height)", Vector) = (2.6, 3.0, 0, 0)
        _Glow ("Rim Glow", Float) = 1.6
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

            TEXTURE2D(_PortalTex);
            SAMPLER(sampler_PortalTex);

            CBUFFER_START(UnityPerMaterial)
                float _Open;
                float _Linked;
                float _Seed;
                float4 _Size;
                float _Glow;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.screenPos = ComputeScreenPos(o.positionCS);
                o.uv = v.uv;
                return o;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // 1つの泡の寄与（半径 r の円で 1）。
            float Blob(float2 p, float2 c, float r)
            {
                float2 d = p - c;
                return r * r / max(dot(d, d), 1e-4);
            }

            // 門の形。中央の大きな泡のかたまりと、まわりを漂う小さな泡。
            float Field(float2 p, float t, float open)
            {
                float s = _Seed;
                float grow = smoothstep(0.0, 1.0, open);
                float f = 0.0;
                // 中央のかたまり（6つ）。開き始めは小さく散らばっていて、育ちながら寄ってくっつく。
                [unroll] for (int i = 0; i < 6; i++)
                {
                    float a = i * 1.047 + s + sin(t * 0.6 + i * 1.7) * 0.35;
                    float spread = lerp(0.95, 0.42, grow) + sin(t * 0.9 + i * 2.3 + s) * 0.06;
                    float2 c = float2(cos(a) * 0.62, sin(a) * 0.85) * spread;
                    float r = (0.28 + 0.06 * sin(t * 1.3 + i * 1.1 + s)) * lerp(0.15, 1.0, grow);
                    f += Blob(p, c, r);
                }
                f += Blob(p, float2(0.0, 0.0), 0.42 * grow);
                // まわりの小さな泡（離れたりくっついたりする）。
                [unroll] for (int j = 0; j < 7; j++)
                {
                    float a = j * 0.898 + s * 1.7 + t * (0.18 + 0.05 * j) * (j % 2 == 0 ? 1.0 : -1.0);
                    float rad = 0.95 + 0.18 * sin(t * 0.7 + j * 2.1 + s);
                    float2 c = float2(cos(a) * 0.78, sin(a) * 0.98) * rad;
                    float r = (0.07 + 0.045 * sin(t * 1.6 + j * 3.1 + s)) * grow;
                    f += Blob(p, c, max(r, 0.0));
                }
                return f;
            }

            // 縁の色。角度と時間でピンク→紫→水色→緑→黄がめぐる。
            float3 RimColor(float x)
            {
                float3 pink = float3(1.0, 0.25, 0.85);
                float3 violet = float3(0.55, 0.3, 1.0);
                float3 cyan = float3(0.25, 0.9, 1.0);
                float3 green = float3(0.45, 1.0, 0.35);
                float3 yellow = float3(1.0, 0.9, 0.35);
                x = frac(x) * 5.0;
                if (x < 1.0) return lerp(pink, violet, x);
                if (x < 2.0) return lerp(violet, cyan, x - 1.0);
                if (x < 3.0) return lerp(cyan, green, x - 2.0);
                if (x < 4.0) return lerp(green, yellow, x - 3.0);
                return lerp(yellow, pink, x - 4.0);
            }

            // 星の空（つながる前・裏側）。
            float3 Space(float2 p, float t)
            {
                float3 col = lerp(float3(0.05, 0.03, 0.22), float3(0.12, 0.05, 0.42), saturate(0.6 - p.y * 0.4));
                col += float3(0.08, 0.12, 0.45) * (1.0 - saturate(length(p) * 0.9));
                float2 g = p * 9.0 + _Seed * 3.0;
                float2 id = floor(g);
                float h = Hash(id);
                float2 star = frac(g) - 0.5 - (float2(Hash(id + 3.1), Hash(id + 7.7)) - 0.5) * 0.6;
                float tw = 0.6 + 0.4 * sin(t * (2.0 + h * 4.0) + h * 30.0);
                float lit = step(0.82, h) * smoothstep(0.09, 0.0, length(star)) * tw;
                col += float3(1.0, 0.95, 1.0) * lit * 1.6;
                return col;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float t = _Time.y;
                float aspect = _Size.x / max(_Size.y, 0.01);
                // 泡が四角の端で切れないように、少し縮めて真ん中に置く。
                float2 p = (i.uv - 0.5) * 2.0 * 1.32;
                p.x *= aspect;

                float open = saturate(_Open);
                if (open <= 0.001) return 0;
                float f = Field(p, t, open);

                // 縁の帯（f=1 が形の境目）。
                const float edge = 1.0;
                const float rimWidth = 0.32;
                float inside = smoothstep(edge, edge + 0.04, f);
                float rim = smoothstep(edge - 0.02, edge + 0.04, f) * (1.0 - smoothstep(edge + rimWidth * 0.35, edge + rimWidth, f));
                float halo = smoothstep(edge * 0.55, edge, f) * (1.0 - inside);

                float angle = atan2(p.y, p.x) / 6.2832;
                float3 rimCol = RimColor(angle + t * 0.12 + _Seed * 0.13 + p.y * 0.15);

                // 中身。つながっていれば反対側の景色（表からも裏からも）、つながる前は星の空。
                float3 spaceCol = Space(p, t);
                float3 inner = spaceCol;
                if (_Linked > 0.001)
                {
                    float2 suv = i.screenPos.xy / i.screenPos.w;
                    // 縁の近くは少しゆらぐ。
                    float wob = (1.0 - smoothstep(edge, edge + 1.2, f)) * 0.006;
                    suv += float2(sin(t * 3.0 + p.y * 9.0), cos(t * 2.6 + p.x * 8.0)) * wob;
                    float3 view = SAMPLE_TEXTURE2D(_PortalTex, sampler_PortalTex, suv).rgb;
                    // 縁から少しのあいだは星の空がにじむ。
                    float mixSpace = (1.0 - smoothstep(edge + 0.25, edge + 1.4, f)) * 0.75;
                    inner = lerp(spaceCol, lerp(view, spaceCol, mixSpace), saturate(_Linked));
                }

                float3 col = inner * inside;
                col = lerp(col, rimCol * _Glow, rim);
                col += rimCol * halo * halo * 0.35 * _Glow;
                float alpha = saturate(inside + rim + halo * halo * 0.3);
                col *= smoothstep(0.0, 0.25, open) ;
                alpha *= smoothstep(0.0, 0.25, open);
                return float4(col, alpha);
            }
            ENDHLSL
        }
    }
}
