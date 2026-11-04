// 魔物の破裂演出。_White で元の色から白へ染め、_Inflate で法線方向へ不規則に膨らませる。_Seed を変えると凹凸が変わる。
Shader "Game/VFX/DeathWhiten"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _White ("White", Range(0, 1)) = 0
        _Inflate ("Inflate", Float) = 0
        _Seed ("Seed", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _White;
                float _Inflate;
                float _Seed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float2 uv : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 normal = TransformObjectToWorldNormal(v.normalOS);
                float3 position = TransformObjectToWorld(v.positionOS.xyz);
                // オブジェクト座標の波を重ねて、部位ごとにこぶのように膨らませる。
                float3 p = v.positionOS.xyz * 11.0 / max(length(v.positionOS.xyz), 0.001) + _Seed;
                float lump = 0.55 + 0.45 * sin(p.x * 1.7) * sin(p.y * 2.3 + 1.1) * sin(p.z * 1.9 + 2.3);
                position += normal * _Inflate * lump;
                o.positionCS = TransformWorldToHClip(position);
                o.normalWS = normal;
                o.uv = v.uv;
                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * _BaseColor.rgb;
                Light light = GetMainLight();
                float diffuse = 0.55 + 0.45 * saturate(dot(normalize(i.normalWS), light.direction));
                // 真っ白でも形が読めるように、白にも少しだけ陰を残す。
                float whiteShade = 0.86 + 0.14 * saturate(dot(normalize(i.normalWS), light.direction));
                return float4(lerp(albedo * diffuse, float3(1.0, 1.0, 1.0) * whiteShade, _White), 1.0);
            }
            ENDHLSL
        }
    }
}
