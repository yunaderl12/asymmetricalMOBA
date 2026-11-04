// 魔力の軌跡。紺色は光らせると消えてしまうので、不透明に近い帯として塗り、中心だけ少し明るくする。
Shader "Game/VFX/MagicTrail"
{
    Properties
    {
        _CoreColor ("Core", Color) = (0.03, 0.08, 0.5, 1)
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
                float4 _CoreColor;
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
                float edge = saturate(1.0 - abs(i.uv.y - 0.5) * 2.0);
                float alpha = saturate(edge * 2.4) * i.color.a;
                float core = edge * edge * edge;
                return float4((i.color.rgb + _CoreColor.rgb * core) * alpha, alpha);
            }
            ENDHLSL
        }
    }
}
