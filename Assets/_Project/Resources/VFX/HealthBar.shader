// 敵味方の頭上のHPバー。画像を引き伸ばさず計算で描くので、どの比率でも形が崩れず、遠くでも縁がちらつかない。
// 頂点色がバーの色、TEXCOORD1 が (fill, trail)、TEXCOORD2.x が 横/縦 の比。
Shader "Game/UI/HealthBar"
{
    Properties
    {
        [PerRendererData] _MainTex ("Unused", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            ZTest [unity_GUIZTestMode]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            static const float Inset = 0.17;       // 枠の太さ（高さ比）
            static const float3 FrameColor = float3(0.018, 0.016, 0.03);
            static const float3 BorderColor = float3(0.42, 0.36, 0.26);
            static const float3 TrackColor = float3(0.05, 0.045, 0.07);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 values : TEXCOORD1;
                float2 shape : TEXCOORD2;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 values : TEXCOORD1;
                float aspect : TEXCOORD2;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.color = v.color;
                o.uv = v.uv;
                o.values = v.values;
                o.aspect = max(1.0, v.shape.x);
                return o;
            }

            float Pill(float2 p, float aspect, float radius)
            {
                float x = clamp(p.x, 0.5, aspect - 0.5);
                return length(p - float2(x, 0.5)) - radius;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float2 p = float2(i.uv.x * i.aspect, i.uv.y);
                float d = Pill(p, i.aspect, 0.5);
                float inner = Pill(p, i.aspect, 0.5 - Inset);
                float aa = max(fwidth(d), 1e-4);
                float aaInner = max(fwidth(inner), 1e-4);

                // 細い明るい縁取りの内側に濃い枠。
                float3 color = lerp(BorderColor, FrameColor, smoothstep(-0.02, -0.09, d));

                float along = (p.x - Inset) / (i.aspect - Inset * 2.0);
                float aaAlong = max(fwidth(along), 1e-4);
                float fill = smoothstep(i.values.x + aaAlong, i.values.x - aaAlong, along) * step(0.0001, i.values.x);
                float trail = smoothstep(i.values.y + aaAlong, i.values.y - aaAlong, along) * step(0.0001, i.values.y);

                // 上が明るく下が沈むグラデーションに、上半分のつや。
                float3 base = i.color.rgb;
                float3 bar = lerp(base * 0.55, base * 1.1 + 0.06, smoothstep(0.2, 0.75, i.uv.y));
                bar += smoothstep(0.58, 0.68, i.uv.y) * (1.0 - smoothstep(0.68, 0.84, i.uv.y)) * 0.22;

                // 減った分はバーの色を白へ寄せて残す。
                float3 content = lerp(TrackColor, lerp(base, float3(1.0, 0.95, 0.85), 0.55) * 0.8, trail);
                content = lerp(content, bar, fill);

                // 25%ごとの刻み。
                float tick = abs(frac(along * 4.0 + 0.5) - 0.5) * 0.25;
                content *= lerp(0.72, 1.0, smoothstep(aaAlong * 0.6, aaAlong * 1.6 + 0.0015, tick));

                color = lerp(color, content, smoothstep(aaInner, -aaInner, inner));
                return float4(color, smoothstep(aa, -aa, d) * i.color.a);
            }
            ENDHLSL
        }
    }
}
