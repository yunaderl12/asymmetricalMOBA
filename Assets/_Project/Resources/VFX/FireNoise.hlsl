#ifndef GAME_FIRE_NOISE_INCLUDED
#define GAME_FIRE_NOISE_INCLUDED

// テクスチャ不要の炎用ノイズ
float FireHash(float3 p)
{
    p = frac(p * 0.3183099 + 0.1);
    p *= 17.0;
    return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
}

float FireValueNoise(float3 x)
{
    float3 i = floor(x);
    float3 f = frac(x);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(
        lerp(lerp(FireHash(i), FireHash(i + float3(1, 0, 0)), f.x),
             lerp(FireHash(i + float3(0, 1, 0)), FireHash(i + float3(1, 1, 0)), f.x), f.y),
        lerp(lerp(FireHash(i + float3(0, 0, 1)), FireHash(i + float3(1, 0, 1)), f.x),
             lerp(FireHash(i + float3(0, 1, 1)), FireHash(i + float3(1, 1, 1)), f.x), f.y),
        f.z);
}

float FireFbm(float3 p)
{
    return 0.55 * FireValueNoise(p) + 0.3 * FireValueNoise(p * 2.1) + 0.15 * FireValueNoise(p * 4.3);
}

#endif
