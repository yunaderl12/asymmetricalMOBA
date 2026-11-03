// 三日月形の斬撃。uv.x は弧に沿った位置、uv.y は 0=内側 1=外側。_Tail から _Head までを描く。
Shader "Game/VFX/SwordArc"
{
    Properties
    {
        [HDR] _Color ("Core", Color) = (2, 2.4, 2.8, 1)
        [HDR] _EdgeColor ("Edge", Color) = (0.25, 0.65, 1.6, 1)
        _Head ("Head", Range(0, 1)) = 1
        _Tail ("Tail", Range(0, 1)) = 0
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
                float4 _EdgeColor;
                float _Head;
                float _Tail;
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
                float span = max(_Head - _Tail, 0.001);
                float s = (i.uv.x - _Tail) / span; // 0=尾 1=先端
                if (s < 0.0 || s > 1.0) discard;

                // 先端は鋭く、尾へ向かって細くなる三日月の幅。
                float thickness = pow(s, 0.6) * (1.0 - pow(s, 10.0));
                float across = (i.uv.y - (1.0 - thickness)) / max(thickness, 0.001); // 0=内縁 1=外縁
                if (across < 0.0) discard;

                float core = smoothstep(0.68, 0.94, across) * (1.0 - smoothstep(0.97, 1.0, across));
                float glow = across * across;
                float streak = 0.8 + 0.2 * sin(i.uv.y * 90.0 + i.uv.x * 12.0);
                float a = saturate(glow * 0.75 + core) * streak * smoothstep(0.0, 0.2, s);
                float3 rgb = lerp(_EdgeColor.rgb, _Color.rgb, core);
                return float4(rgb * a, a * 0.85);
            }
            ENDHLSL
        }
    }
}
