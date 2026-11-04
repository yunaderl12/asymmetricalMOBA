// 探知した敵の頭上の印。壁越しでも見える菱形の枠。
Shader "Game/VFX/RevealMark"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (2, 0.3, 0.2, 1)
        _Fade ("Fade", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+50" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Fade;
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
                float d = abs(i.uv.x - 0.5) + abs(i.uv.y - 0.5);
                float frame = smoothstep(0.3, 0.33, d) * (1.0 - smoothstep(0.45, 0.48, d));
                float core = 1.0 - smoothstep(0.1, 0.13, d);
                float alpha = saturate(frame + core) * _Fade;
                return float4(_Color.rgb * alpha, alpha);
            }
            ENDHLSL
        }
    }
}
