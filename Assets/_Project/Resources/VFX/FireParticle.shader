// パーティクルとトレイル用のシェーダー。_Radial=1 は丸い粒、0 は幅方向だけ柔らかい帯。
Shader "Game/VFX/FireParticle"
{
    Properties
    {
        _Radial ("Radial", Range(0, 1)) = 1
        _Boost ("Boost", Float) = 3
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
                float _Radial;
                float _Boost;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float d = _Radial > 0.5 ? length(i.uv - 0.5) * 2.0 : abs(i.uv.y - 0.5) * 2.0;
                float a = saturate(1.0 - d);
                a *= a;
                return float4(i.color.rgb * i.color.a * a * _Boost, i.color.a * a * 0.6);
            }
            ENDHLSL
        }
    }
}
