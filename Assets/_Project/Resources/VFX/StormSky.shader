// 空。雲より上の高さに、城を中心にした赤い空が広がっているように描く。
// 外側と地平線近くは青空のままで、赤との間はなだらかにつながる。
Shader "Game/VFX/StormSky"
{
    Properties
    {
        _HorizonColor ("Horizon", Color) = (0.62, 0.8, 1, 1)
        _ZenithColor ("Zenith", Color) = (0.2, 0.42, 0.85, 1)
        _RedLowColor ("Red Low", Color) = (0.75, 0.2, 0.14, 1)
        _RedHighColor ("Red High", Color) = (0.42, 0.04, 0.07, 1)
        _MidColor ("Between Red And Blue", Color) = (0.52, 0.34, 0.58, 1)
        _RedCeiling ("Height Of The Red (m)", Float) = 50
        _RedInner ("Fully Red Inside (x radius)", Float) = 1.1
        _RedOuter ("Fully Blue Outside (x radius)", Float) = 4
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "RenderPipeline" = "UniversalPipeline" "PreviewType" = "Skybox" }
        Pass
        {
            Cull Off
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _HorizonColor;
                float4 _ZenithColor;
                float4 _RedLowColor;
                float4 _RedHighColor;
                float4 _MidColor;
                float _RedCeiling;
                float _RedInner;
                float _RedOuter;
            CBUFFER_END
            float3 _StormCenter;
            float _StormRadius;

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.dir = v.positionOS.xyz;
                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float3 dir = normalize(i.dir);
                float up = saturate(dir.y);
                float3 blue = lerp(_HorizonColor.rgb, _ZenithColor.rgb, pow(up, 0.6));

                // 視線が雲より上の高さに届く場所と城との距離で赤さを決める。
                float climb = max(_RedCeiling - _WorldSpaceCameraPos.y, 1);
                float2 hit = _WorldSpaceCameraPos.xz + dir.xz * climb / max(up, 0.02);
                float radius = max(_StormRadius, 1);
                float amount = 1 - smoothstep(radius * _RedInner, radius * _RedOuter, distance(hit, _StormCenter.xz));

                // 赤と青の間は、くすんだ赤紫を通る。
                float3 red = lerp(_RedLowColor.rgb, _RedHighColor.rgb, pow(up, 0.7));
                float3 mid = lerp(lerp(blue, red, 0.5), _MidColor.rgb, 0.6);
                float3 col = amount < 0.5 ? lerp(blue, mid, amount * 2) : lerp(mid, red, amount * 2 - 1);
                return float4(col, 1);
            }
            ENDHLSL
        }
    }
}
