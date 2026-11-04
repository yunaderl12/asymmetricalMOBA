// Explosion のドーム。アニメ塗りの白い半球に、紺の太い縁取りと、横に流れる青い筆の跡を入れる。_Fade が下がると透けて消える。
Shader "Game/VFX/ExplosionDome"
{
    Properties
    {
        _Outline ("Outline", Color) = (0.02, 0.06, 0.3, 1)
        _Blue ("Blue", Color) = (0.1, 0.55, 1, 1)
        _White ("White", Color) = (1, 1, 1, 1)
        _Fade ("Fade", Range(0, 1)) = 1
        _Seed ("Seed", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Blend One OneMinusSrcAlpha
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "FireNoise.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Outline;
                float4 _Blue;
                float4 _White;
                float _Fade;
                float _Seed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; float3 local : TEXCOORD2; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.local = normalize(v.positionOS.xyz);
                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float3 view = normalize(GetWorldSpaceViewDir(i.positionWS));
                float facing = saturate(dot(normalize(i.normalWS), view));
                float time = _Time.y;

                // 筆で引いたように、横に長く引き伸ばしたノイズ。
                float3 streakPos = float3(i.local.x * 2.2 + time * 0.5 + _Seed, i.local.y * 11.0, i.local.z * 2.2 - time * 0.3);
                float streak = FireFbm(streakPos);
                float band = smoothstep(0.54, 0.6, streak) * (1.0 - smoothstep(0.7, 0.86, i.local.y)) * smoothstep(-0.1, 0.08, i.local.y);

                float wobble = FireValueNoise(float3(i.local * 7.0 + _Seed)) * 0.07;
                float outline = 1.0 - smoothstep(0.17, 0.2, facing - wobble);
                float rim = 1.0 - smoothstep(0.27, 0.31, facing - wobble);

                float3 c = _White.rgb;
                c = lerp(c, _Blue.rgb, saturate(band + rim));
                c = lerp(c, _Outline.rgb, outline);

                // 消えるときは輪郭から水色に透けていく。
                float3 faded = lerp(float3(0.6, 0.85, 1.0), c, _Fade);
                float a = _Fade * lerp(0.55, 1.0, _Fade) ;
                return float4(faded * a, a);
            }
            ENDHLSL
        }
    }
}
