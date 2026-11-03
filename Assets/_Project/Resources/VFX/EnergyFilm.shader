// 光の膜。縁ほど明るく、中央は透ける。_Hex で六角の模様、_Flash で被弾時の光を足す。残像にも使う。
Shader "Game/VFX/EnergyFilm"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (0.25, 0.65, 1.6, 0.6)
        _Hex ("Hex", Range(0, 1)) = 1
        _Flash ("Flash", Range(0, 1)) = 0
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

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Hex;
                float _Flash;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.positionOS = v.positionOS.xyz;
                return o;
            }

            // 六角形の格子の線（0=線の上 1=セルの中央）。
            float HexCell(float2 p)
            {
                const float2 r = float2(1.0, 1.732);
                float2 a = fmod(abs(p), r) - r * 0.5;
                float2 b = fmod(abs(p - r * 0.5), r) - r * 0.5;
                float2 g = dot(a, a) < dot(b, b) ? a : b;
                g = abs(g);
                return 0.5 - max(dot(g, normalize(r)), g.x);
            }

            float4 frag(Varyings i) : SV_Target
            {
                float3 view = normalize(GetWorldSpaceViewDir(i.positionWS));
                float rim = pow(1.0 - saturate(dot(normalize(i.normalWS), view)), 2.2);
                float cell = HexCell(i.positionOS.xy * 9.0);
                float lines = (1.0 - smoothstep(0.0, 0.06, cell)) * _Hex;
                float strength = rim + lines * (0.14 + rim * 0.5) + _Flash * (0.35 + rim);
                float a = saturate(strength * saturate(_Color.a));
                float3 rgb = lerp(_Color.rgb, float3(2.2, 2.5, 2.8), saturate(_Flash * 0.7 + rim * 0.25));
                return float4(rgb * a, a * 0.75);
            }
            ENDHLSL
        }
    }
}
