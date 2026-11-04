// 地面を削った場所を切り抜く。_DigMaskTex はセルごとの点サンプリングのテクスチャで、GroundDig が更新する。
// 値が 1 なら地面も草も、0.5 なら草だけ（_DIG_DECOR）を切り抜く。
#ifndef GAME_DIG_MASK_INCLUDED
#define GAME_DIG_MASK_INCLUDED

TEXTURE2D(_DigMaskTex);
float4 _DigMaskRect; // xMin, zMin, 1/幅, 1/奥行き

void DigClip(float3 positionWS)
{
    float2 uv = (positionWS.xz - _DigMaskRect.xy) * _DigMaskRect.zw;
    float value = SAMPLE_TEXTURE2D_LOD(_DigMaskTex, sampler_PointClamp, uv, 0).r;
#ifdef _DIG_DECOR
    if (value > 0.25) discard;
#else
    if (value > 0.75) discard;
#endif
}

#endif
