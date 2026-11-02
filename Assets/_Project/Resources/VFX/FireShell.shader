// 被弾時の白フラッシュと炎の膜。ベイクしたメッシュの複製に使う。
// _Flash=1 なら不透明の真っ白、0 なら外周だけ光る加算の炎。
Shader "Game/VFX/FireShell"
{
    Properties
    {
        _Expand ("Expand", Float) = 0.03
        _Intensity ("Intensity", Float) = 1
        _Flash ("Flash", Range(0, 1)) = 0
        _Dissolve ("Dissolve", Range(0, 1)) = 0
        _Fill ("Fill", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        // 先に深度だけ書いて、一番手前の面だけに炎を描く（内側の線が透けて見えないように）。
        Pass
        {
            Tags { "LightMode" = "SRPDefaultUnlit" }
            ColorMask 0
            ZWrite On
            Cull Back
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragDepth
            #include "FireShellCommon.hlsl"
            float4 fragDepth(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "FireShellCommon.hlsl"
            float4 frag(Varyings i) : SV_Target
            {
                if (_Flash > 0.5) return float4(1.6, 1.6, 1.6, 1.0);

                float3 n = normalize(i.normalWS);
                float3 viewDir = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float rim = pow(1.0 - saturate(dot(n, viewDir)), 1.6);
                float noise = FireFbm(i.positionWS * 3.0 + float3(0.0, -_Time.y * 3.0, 0.0));
                float flame = smoothstep(_Dissolve, _Dissolve + 0.35, rim * (0.6 + noise * 1.2));
                // _Fill: 本体が消えた後は全身を炎で埋める。
                flame = max(flame, _Fill * smoothstep(_Dissolve * 0.8, _Dissolve * 0.8 + 0.4, noise));
                float3 col = lerp(float3(1.2, 0.22, 0.02), float3(1.7, 0.75, 0.12), flame * flame);
                return float4(col * flame * _Intensity, _Fill * flame * _Intensity * 0.8);
            }
            ENDHLSL
        }
    }
}
