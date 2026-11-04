// 四方にとがった星のきらめき。常にカメラを向けて使う。
Shader "Game/VFX/SparkleStar"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (0.5, 1.5, 2, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+30" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
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
                float2 p = abs(i.uv - 0.5) * 2.0;
                float d = sqrt(p.x) + sqrt(p.y);
                float star = 1.0 - smoothstep(0.62, 0.72, d);
                float halo = (1.0 - smoothstep(0.55, 1.0, d)) * 0.55 * saturate(1.0 - length(p));
                float a = saturate(star + halo) * _Color.a;
                float3 rgb = lerp(_Color.rgb, float3(3, 3, 3), star);
                return float4(rgb * a, a);
            }
            ENDHLSL
        }
    }
}
