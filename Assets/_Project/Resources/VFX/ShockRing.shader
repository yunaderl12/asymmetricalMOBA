// 地面を走る光の輪。_Ring は輪の位置（0〜1）、_Width は輪の太さ（同じ比率）。
Shader "Game/VFX/ShockRing"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (0.2, 1, 1, 1)
        _Ring ("Ring", Range(0, 1)) = 0.5
        _Width ("Width", Range(0, 1)) = 0.1
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
                float _Ring;
                float _Width;
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
                float r = length(i.uv - 0.5) * 2.0;
                // 外側はくっきり、内側へ尾を引く。
                float d = r - _Ring;
                float band = d > 0.0 ? 1.0 - smoothstep(0.0, _Width * 0.25, d) : 1.0 - smoothstep(0.0, _Width, -d);
                band *= band;
                float a = band * saturate(_Color.a);
                return float4(_Color.rgb * a, a * 0.7);
            }
            ENDHLSL
        }
    }
}
