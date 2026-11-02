#ifndef GAME_FIRE_SHELL_COMMON_INCLUDED
#define GAME_FIRE_SHELL_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "FireNoise.hlsl"

CBUFFER_START(UnityPerMaterial)
    float _Expand;
    float _Intensity;
    float _Flash;
    float _Dissolve;
    float _Fill;
CBUFFER_END

struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
struct Varyings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    float3 normalWS : TEXCOORD1;
};

Varyings vert(Attributes v)
{
    Varyings o;
    o.normalWS = TransformObjectToWorldNormal(v.normalOS);
    o.positionWS = TransformObjectToWorld(v.positionOS.xyz) + o.normalWS * _Expand;
    o.positionCS = TransformWorldToHClip(o.positionWS);
    return o;
}

#endif
