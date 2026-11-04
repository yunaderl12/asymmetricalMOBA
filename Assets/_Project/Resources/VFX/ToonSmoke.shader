// アニメ塗りの煙の玉。上が明るい2段階の灰色と黒い縁取り。_Dissolve を上げるとノイズで虫食いに消える。
Shader "Game/VFX/ToonSmoke"
{
    Properties
    {
        _Light ("Light", Color) = (0.55, 0.56, 0.6, 1)
        _Dark ("Dark", Color) = (0.34, 0.34, 0.38, 1)
        _Line ("Line", Color) = (0.1, 0.1, 0.13, 1)
        _Dissolve ("Dissolve", Range(0, 1)) = 0
        _Seed ("Seed", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry+10" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "FireNoise.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Light;
                float4 _Dark;
                float4 _Line;
                float _Dissolve;
                float _Seed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; float3 local : TEXCOORD2; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                // 玉の表面をでこぼこさせて、綿のような輪郭にする。
                float bump = FireValueNoise(v.positionOS.xyz * 5.0 + _Seed);
                float3 p = v.positionOS.xyz * (0.86 + bump * 0.28);
                o.positionWS = TransformObjectToWorld(p);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.local = v.positionOS.xyz;
                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float noise = FireFbm(i.local * 6.0 + _Seed);
                clip(noise - _Dissolve * 1.05);

                float3 n = normalize(i.normalWS);
                float3 view = normalize(GetWorldSpaceViewDir(i.positionWS));
                float facing = saturate(dot(n, view));
                float lit = dot(n, normalize(float3(0.3, 1.0, -0.2)));

                float3 c = lerp(_Dark.rgb, _Light.rgb, smoothstep(0.1, 0.16, lit + (noise - 0.5) * 0.25));
                c = lerp(_Line.rgb, c, smoothstep(0.26, 0.31, facing));
                // 消えかけの縁を暗くして、焦げるように見せる。
                c = lerp(_Line.rgb, c, smoothstep(0.0, 0.05, noise - _Dissolve * 1.05));
                return float4(c, 1.0);
            }
            ENDHLSL
        }
    }
}
