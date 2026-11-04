// 涙滴形の炎。外側から紺・青・マゼンタ・薄桃・白の層になり、尾へ行くほど冷えて千切れる。尾は頂点を揺らしてうねらせる。
Shader "Game/VFX/MagicFlame"
{
    Properties
    {
        [HDR] _Outer ("Outer", Color) = (0.02, 0.03, 0.42, 1)
        [HDR] _Body ("Body", Color) = (0.12, 0.26, 1.3, 1)
        [HDR] _Hot ("Hot", Color) = (1.5, 0.12, 0.8, 1)
        [HDR] _Pink ("Pink", Color) = (2, 0.75, 1.7, 1)
        [HDR] _Core ("Core", Color) = (3, 2.8, 3, 1)
        _Seed ("Seed", Float) = 0
        _Bend ("Bend", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
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
                float4 _Outer;
                float4 _Body;
                float4 _Hot;
                float4 _Pink;
                float4 _Core;
                float _Seed;
                float _Bend;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; float2 uv : TEXCOORD2; float3 noisePos : TEXCOORD3; };

            Varyings vert(Attributes v)
            {
                float t = v.uv.y;
                float time = _Time.y + _Seed;
                float3 p = v.positionOS.xyz;
                // 根元は動かさず、尾へ行くほど大きくうねる。
                float bend = pow(t, 1.6) * _Bend;
                p.x += (sin(t * 4.5 - time * 9.0) * 0.2 + 0.3) * bend;
                p.y += cos(t * 3.4 - time * 6.3) * 0.14 * bend;
                // 長さ方向に走る波で輪郭をふくらませる。
                p.xy *= 1.0 + sin(t * 10.0 - time * 13.0) * 0.14 * t;

                Varyings o;
                o.positionWS = TransformObjectToWorld(p);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv;
                o.noisePos = v.positionOS.xyz * 5.0 + float3(_Seed, _Seed * 0.7, -time * 3.2);
                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float3 view = normalize(GetWorldSpaceViewDir(i.positionWS));
                float facing = saturate(dot(normalize(i.normalWS), view));
                float t = i.uv.y;
                float flicker = FireFbm(i.noisePos);

                // 中心かつ根元ほど熱い。
                float heat = facing * facing * (1.25 - t * 1.25) + (flicker - 0.5) * 0.4 * saturate(t * 2.0 + 0.2);

                float3 c = _Outer.rgb;
                c = lerp(c, _Body.rgb, smoothstep(0.1, 0.34, heat));
                c = lerp(c, _Hot.rgb, smoothstep(0.42, 0.58, heat));
                c = lerp(c, _Pink.rgb, smoothstep(0.64, 0.78, heat));
                c = lerp(c, _Core.rgb, smoothstep(0.84, 0.96, heat));

                // 縁はふわっと消し、尾の先はノイズで千切る。
                float edge = smoothstep(0.0, 0.3, facing);
                float tip = 1.0 - smoothstep(0.62, 1.0, t + (flicker - 0.5) * 0.5);
                // 冷たい層は透かして、後ろから見ても芯の光が見えるようにする。
                float a = saturate(edge * tip) * lerp(0.6, 1.0, smoothstep(0.2, 0.5, heat));
                return float4(c * a, a);
            }
            ENDHLSL
        }
    }
}
