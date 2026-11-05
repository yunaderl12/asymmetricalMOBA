using UnityEngine;

namespace Game.Core
{
    public struct SkyParams
    {
        public Color ambient;
        public Color lightColor;
        public float lightIntensity;

        public static SkyParams Lerp(SkyParams a, SkyParams b, float t)
        {
            return new SkyParams
            {
                ambient = Color.Lerp(a.ambient, b.ambient, t),
                lightColor = Color.Lerp(a.lightColor, b.lightColor, t),
                lightIntensity = Mathf.Lerp(a.lightIntensity, b.lightIntensity, t),
            };
        }
    }

    public static class SkyPalette
    {
        // 青空の下と、赤い空の下の明るさ。見ている場所でこの間を混ぜる。
        public static readonly SkyParams Clear = new SkyParams { ambient = new Color(0.70f, 0.75f, 0.85f), lightColor = new Color(1.00f, 0.95f, 0.85f), lightIntensity = 1.00f };
        public static readonly SkyParams Storm = new SkyParams { ambient = new Color(0.42f, 0.30f, 0.32f), lightColor = new Color(0.88f, 0.48f, 0.42f), lightIntensity = 0.72f };

        /// 空の段階（0始まり）。BalanceConfig の段階ごとの半径に対応する。
        public static int StageForLevel(int level)
        {
            var config = BalanceConfig.Instance;
            int last = Mathf.Max(config.stormStageRadius.Length - 1, 0);
            return Mathf.Clamp((level - 1) / Mathf.Max(config.stormLevelsPerStage, 1), 0, last);
        }
    }
}
