// HexWard の六角形セル。uv.x=縁までの近さ、uv.y=セルの乱数、uv1=セルの中心。_Shatter を上げると中心から順にセルが飛び散る。
Shader "Game/VFX/HexWard"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (0.45, 1.3, 1.7, 1)
        _Fade ("Fade", Range(0, 1)) = 1
        _Shatter ("Shatter", Range(0, 1)) = 0
        _Ripple ("Ripple", Float) = 0
        _Flash ("Flash", Range(0, 1)) = 0
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

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Fade;
                float _Shatter;
                float _Ripple;
                float _Flash;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float2 center : TEXCOORD1; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float distanceFromMiddle : TEXCOORD1; float broken : TEXCOORD2; };

            float2 Rotate(float2 p, float radians)
            {
                float s = sin(radians);
                float c = cos(radians);
                return float2(p.x * c - p.y * s, p.x * s + p.y * c);
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                float seed = v.uv.y;
                float fromMiddle = length(v.center);
                float delay = saturate(fromMiddle) * 0.3 + seed * 0.08;
                float t = saturate((_Shatter - delay) / (1.0 - delay));
                float eased = 1.0 - (1.0 - t) * (1.0 - t);

                // セルごとに回りながら縮み、外へ飛んで落ちる。+Z が攻撃の来た側なので、破片は手前（-Z）へ抜ける。
                float2 offset = v.positionOS.xy - v.center;
                offset = Rotate(offset, t * (seed - 0.5) * 16.0) * (1.0 - t * 0.55);
                float tumble = sin(t * (3.0 + seed * 6.0)) * length(offset);
                float2 outward = fromMiddle > 0.001 ? v.center / fromMiddle : Rotate(float2(1.0, 0.0), seed * 6.28);
                float3 position = float3(v.center + offset, tumble);
                position.xy += outward * eased * (0.7 + seed * 0.9);
                position.z -= eased * (0.25 + seed * 0.75);
                position.y -= t * t * 0.7;

                o.positionCS = TransformObjectToHClip(position);
                o.uv = v.uv;
                o.distanceFromMiddle = fromMiddle;
                o.broken = t;
                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float rim = i.uv.x;
                float edge = smoothstep(0.72, 0.97, rim);
                float fill = 0.1 + 0.08 * i.uv.y;
                float ripple = exp(-pow((i.distanceFromMiddle - _Ripple) / 0.22, 2.0));

                float shape = saturate(fill + edge * 0.9 + ripple * 0.35 + _Flash * 0.5 + i.broken * 0.15);
                float3 rgb = lerp(_Color.rgb, float3(2.2, 2.6, 2.8), saturate(edge * 0.55 + ripple * 0.6 + _Flash)) * shape;
                float a = shape * _Fade * _Color.a;
                return float4(rgb * _Fade * _Color.a, a * 0.8);
            }
            ENDHLSL
        }
    }
}
