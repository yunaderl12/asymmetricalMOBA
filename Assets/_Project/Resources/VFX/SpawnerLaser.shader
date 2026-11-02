// スポナーの塔のレーザー。どす暗い紫の芯のまわりをピンクの炎がなめるように流れる。常にカメラへ面を向ける帯。
// _Progress は先端の位置（0〜1）、_Fade は消えていく度合い（0〜1）。
Shader "Game/VFX/SpawnerLaser"
{
    Properties
    {
        _Length ("Length (m)", Float) = 10
        _Progress ("Progress", Range(0, 1)) = 1
        _Fade ("Fade", Range(0, 1)) = 0
        _Seed ("Seed", Float) = 0
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
            #include "FireNoise.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Length;
                float _Progress;
                float _Fade;
                float _Seed;
            CBUFFER_END

            static const float3 HotPink = float3(1.0, 0.2, 0.72);
            static const float3 Magenta = float3(0.62, 0.04, 0.7);
            static const float3 DarkPurple = float3(0.06, 0.01, 0.14);

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 start = TransformObjectToWorld(float3(0, 0, v.positionOS.z));
                float3 axis = TransformObjectToWorldDir(float3(0, 0, 1));
                float width = length(float3(UNITY_MATRIX_M[0].x, UNITY_MATRIX_M[1].x, UNITY_MATRIX_M[2].x));
                float3 side = normalize(cross(axis, _WorldSpaceCameraPos - start));
                o.positionCS = TransformWorldToHClip(start + side * v.positionOS.x * width);
                o.uv = v.uv;
                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float along = i.uv.y * _Length;
                float t = _Time.y + _Seed;

                // 芯ごと少し揺らす。
                float across = i.uv.x + (FireFbm(float3(along * 0.35 - t * 6.0, t * 2.0, _Seed)) - 0.5) * 0.3;
                // 根元と先端は細くする。
                float head = saturate((_Progress * _Length - along) / 0.5);
                float root = saturate(along / 0.6);
                float d = abs(across) / max(0.05, min(head, root));

                // 炎の舌は塔から相手へ向かって流れる。
                float tongue = smoothstep(0.28, 0.72, FireFbm(float3(along * 1.5 - t * 11.0, across * 2.0, t * 1.5)));
                float reach = 0.3 + 0.7 * tongue;
                float flame = smoothstep(reach, reach * 0.3, d);
                flame *= saturate(1.0 - _Fade * 2.0 * (1.0 - tongue));

                float coreRadius = (0.2 + 0.1 * FireFbm(float3(along * 1.4 - t * 5.0, 7.0, t))) * (1.0 - _Fade * 0.8);
                float core = smoothstep(coreRadius, coreRadius * 0.55, d);
                float rim = smoothstep(coreRadius * 1.8, coreRadius, d) * (1.0 - core);

                float3 color = lerp(HotPink, Magenta, smoothstep(0.2, 0.7, flame));
                color += HotPink * rim * 0.9;
                color = lerp(color, DarkPurple, core);

                float visible = step(along, _Progress * _Length) * (1.0 - _Fade);
                // 炎は光として足し、芯だけ後ろを隠す暗さにする。
                float alpha = lerp(flame * 0.4, 1.0, core) * visible * visible;
                return float4(color * max(flame, core) * visible, alpha);
            }
            ENDHLSL
        }
    }
}
