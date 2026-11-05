// 城のまわりを渦巻く雲。かたまりを対数らせんの腕に並べ、頂点シェーダーで回転と中心への吸い込みを動かす。
// 面ごとに色を分けて、ステージと同じ角ばった質感にする。_StormRadius より外のかたまりは縮んで消える。
Shader "Game/VFX/StormCloud"
{
    Properties
    {
        _UnderColor ("Under", Color) = (0.44, 0.44, 0.47, 1)
        _SideColor ("Side", Color) = (0.6, 0.6, 0.63, 1)
        _TopColor ("Top", Color) = (0.8, 0.78, 0.8, 1)
        _RedTint ("Red Tint", Color) = (0.55, 0.12, 0.1, 1)
        _RedAmount ("Red Amount", Range(0, 1)) = 0.3
        _EyeRadius ("Eye Radius", Float) = 12
        _MaxRadius ("Max Radius", Float) = 200
        _Winding ("Arm Winding", Float) = 2.5
        _Height ("Height", Float) = 36
        _Spin ("Spin (rad/s)", Float) = 0.04
        _InflowSeconds ("Inflow Seconds", Float) = 900
        _SizeRatio ("Size / Distance", Float) = 0.085
        _MinSize ("Min Size", Float) = 1.3
    }
    SubShader
    {
        // 不透明だが、Depth Prepass を通さずに描くため半透明の列に置く。地形には深度で隠れる。
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-50" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _UnderColor;
                float4 _SideColor;
                float4 _TopColor;
                float4 _RedTint;
                float _RedAmount;
                float _EyeRadius;
                float _MaxRadius;
                float _Winding;
                float _Height;
                float _Spin;
                float _InflowSeconds;
                float _SizeRatio;
                float _MinSize;
            CBUFFER_END
            float3 _StormCenter;
            float _StormRadius;
            float _StormTime;

            struct Attributes
            {
                float4 positionOS : POSITION; // かたまり内の位置（半径1）
                float2 flow : TEXCOORD0;      // x: らせん上の位置 0〜1 / y: 腕の角度
                float2 shape : TEXCOORD1;     // x: 高さのばらつき -1〜1 / y: 大きさの倍率
                float4 color : COLOR;         // r: 明るさの倍率
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float shade : TEXCOORD1;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float u = frac(v.flow.x - _StormTime / max(_InflowSeconds, 1));
                float span = log(_MaxRadius / _EyeRadius);
                float logR = u * span;
                float r = _EyeRadius * exp(logR);

                float size = max(r * _SizeRatio, _MinSize) * v.shape.y;
                // 嵐の外周と、らせんの両端では縮めて消す。
                size *= smoothstep(0, 1, saturate((_StormRadius - r) / max(_StormRadius * 0.2, 1)));
                size *= saturate(u / 0.03) * saturate((1 - u) / 0.03);

                float theta = v.flow.y + _Winding * logR + _StormTime * _Spin;
                float s = sin(theta), c = cos(theta);
                float3 offset = v.positionOS.xyz * size;
                offset = float3(offset.x * c - offset.z * s, offset.y, offset.x * s + offset.z * c);

                float3 world = _StormCenter + float3(c * r, _Height + size * (0.25 + 0.35 * v.shape.x), s * r) + offset;
                o.positionWS = world;
                o.positionCS = TransformWorldToHClip(world);
                o.shade = v.color.r;
                // 真上からの平行投影（魔王の俯瞰）では地図を隠さないように描かない。
                if (unity_OrthoParams.w > 0.5) o.positionCS = float4(2, 2, 2, 1);
                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(cross(ddy(i.positionWS), ddx(i.positionWS)));
                if (dot(n, _WorldSpaceCameraPos - i.positionWS) < 0) n = -n;
                float3 sun = normalize(float3(0.45, 0.35, 0.6));
                float side = saturate(dot(n, sun) * 0.5 + 0.5);
                float3 col = lerp(_UnderColor.rgb, _SideColor.rgb, saturate(n.y + 1));
                col = lerp(col, _TopColor.rgb, saturate(n.y));
                col *= lerp(0.82, 1.1, side) * i.shade;
                // 上の赤い空を受けて、上向きの面ほど赤く染まる。
                col = lerp(col, col * 0.5 + _RedTint.rgb * 0.7, _RedAmount * saturate(n.y * 0.5 + 0.5));
                return float4(col, 1);
            }
            ENDHLSL
        }
    }
}
