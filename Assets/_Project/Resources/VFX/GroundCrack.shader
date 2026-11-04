// 地面のヒビ。uv.x が _Reveal より先は描かない。はじめは赤熱し、冷めると暗い割れ目になる。
Shader "Game/VFX/GroundCrack"
{
    Properties
    {
        _Reveal ("Reveal", Range(0, 1)) = 1
        _Heat ("Heat", Range(0, 1)) = 0
        _Fade ("Fade", Range(0, 1)) = 1
        _DarkColor ("Dark", Color) = (0.05, 0.035, 0.03, 1)
        [HDR] _HotColor ("Hot", Color) = (3, 1, 0.15, 1)
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

            CBUFFER_START(UnityPerMaterial)
                float _Reveal;
                float _Heat;
                float _Fade;
                float4 _DarkColor;
                float4 _HotColor;
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
                clip(_Reveal - i.uv.x);
                float across = 1.0 - abs(i.uv.y - 0.5) * 2.0;
                float alpha = saturate(across * 3.0) * _Fade;
                float3 color = lerp(_DarkColor.rgb, _HotColor.rgb, _Heat * saturate(across * 1.5) * (1.0 - i.uv.x * 0.5));
                return float4(color * alpha, alpha);
            }
            ENDHLSL
        }
    }
}
