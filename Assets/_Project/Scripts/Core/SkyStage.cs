using UnityEngine;

namespace Game.Core
{
    public enum SkyStage { BlueSky, LightCloudy, Cloudy, ReddishCloudy, BloodCloudy, OminousPurple }

    public struct SkyParams
    {
        public Color background;
        public Color ambient;
        public Color lightColor;
        public float lightIntensity;
    }

    public static class SkyPalette
    {
        public static SkyParams For(SkyStage stage)
        {
            switch (stage)
            {
                case SkyStage.BlueSky:
                    return new SkyParams { background = new Color(0.50f, 0.70f, 1.00f), ambient = new Color(0.70f, 0.75f, 0.85f), lightColor = new Color(1.00f, 0.95f, 0.85f), lightIntensity = 1.00f };
                case SkyStage.LightCloudy:
                    return new SkyParams { background = new Color(0.65f, 0.65f, 0.70f), ambient = new Color(0.55f, 0.55f, 0.60f), lightColor = new Color(0.90f, 0.90f, 0.95f), lightIntensity = 0.85f };
                case SkyStage.Cloudy:
                    return new SkyParams { background = new Color(0.40f, 0.40f, 0.45f), ambient = new Color(0.35f, 0.35f, 0.40f), lightColor = new Color(0.70f, 0.70f, 0.75f), lightIntensity = 0.70f };
                case SkyStage.ReddishCloudy:
                    return new SkyParams { background = new Color(0.45f, 0.30f, 0.30f), ambient = new Color(0.40f, 0.25f, 0.25f), lightColor = new Color(0.85f, 0.50f, 0.45f), lightIntensity = 0.70f };
                case SkyStage.BloodCloudy:
                    return new SkyParams { background = new Color(0.55f, 0.18f, 0.18f), ambient = new Color(0.45f, 0.20f, 0.20f), lightColor = new Color(0.90f, 0.35f, 0.30f), lightIntensity = 0.65f };
                case SkyStage.OminousPurple:
                    return new SkyParams { background = new Color(0.35f, 0.10f, 0.35f), ambient = new Color(0.30f, 0.15f, 0.35f), lightColor = new Color(0.80f, 0.30f, 0.85f), lightIntensity = 0.60f };
            }
            return new SkyParams { background = Color.black, ambient = Color.gray, lightColor = Color.white, lightIntensity = 1f };
        }

        public static SkyStage StageForLevel(int level)
        {
            if (level >= 16) return SkyStage.OminousPurple;
            if (level >= 13) return SkyStage.BloodCloudy;
            if (level >= 10) return SkyStage.ReddishCloudy;
            if (level >= 7) return SkyStage.Cloudy;
            if (level >= 4) return SkyStage.LightCloudy;
            return SkyStage.BlueSky;
        }
    }
}
