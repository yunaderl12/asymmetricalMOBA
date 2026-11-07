using System.Collections.Generic;
using UnityEngine;
using Game.Combat;
using Game.Hero;
using Game.Skill;

namespace Game.DemonKing
{
    /// 地震。場所は選ばない。chargeSeconds のあいだ小さく揺れ続け（予震）、そのあと大きく揺れて、
    /// 地面に立っているすべての勇者と地上の魔物（浮いている・飛んでいる魔物は除く）に damage を与える。跳んでいる勇者には当たらない。
    public class FieldSpellEarthquake : FieldSpellEffect
    {
        private const float TremorStep = 0.5f;
        private const float WorldRadius = 2000f;

        private bool quaked;
        private float nextTremor;

        protected override void Begin()
        {
            Game.UI.MatchNotice.Show("地面が揺れ始めた…", 2.5f);
        }

        protected override void Tick(float dt)
        {
            float charge = Mathf.Max(0.01f, data.chargeSeconds);
            if (!quaked)
            {
                // 予震: だんだん強くなる小さな揺れ。勇者の足元から小石が跳ねる。
                if (time >= nextTremor)
                {
                    nextTremor = time + TremorStep;
                    float k = Mathf.Clamp01(time / charge);
                    HeroFPSController.ShakeNear(Vector3.zero, Mathf.Lerp(0.04f, 0.1f, k), WorldRadius, TremorStep + 0.1f);
                    foreach (var hp in Heroes()) EarthChips.Spawn(hp.transform.position, 1.2f, 2);
                }
                if (time >= charge) Quake();
                return;
            }
            if (time >= charge + data.duration) Destroy(gameObject);
            else if (time >= nextTremor)
            {
                // 本震の揺れが続くあいだ、弱まりながら揺らし続ける。
                nextTremor = time + TremorStep;
                float k = 1f - Mathf.Clamp01((time - charge) / Mathf.Max(0.1f, data.duration));
                HeroFPSController.ShakeNear(Vector3.zero, 0.35f * k, WorldRadius, TremorStep + 0.1f);
            }
        }

        private void Quake()
        {
            quaked = true;
            nextTremor = time + TremorStep;
            HeroFPSController.ShakeNear(Vector3.zero, 0.45f, WorldRadius, 0.9f);
            foreach (var hp in new List<HealthComponent>(Heroes()))
            {
                var human = HumanOf(hp);
                if (human != null && !human.IsGrounded) continue;
                Vector3 at = hp.transform.position;
                Hit(hp, data.damage, DamageType.Physical);
                GroundCracks.Spawn(at, 3f, 1.5f);
                EarthChips.Spawn(at, 1.6f, 8);
            }
            foreach (var m in Object.FindObjectsByType<Game.Monster.Monster>(FindObjectsSortMode.None))
            {
                if (m == null || m.Data == null || m.Data.floatHeight > 0f || m.Data.hoverHeight > 0f) continue;
                var hp = m.GetComponent<HealthComponent>();
                if (hp == null || hp.IsDead) continue;
                Hit(hp, data.damage, DamageType.Physical);
                EarthChips.Spawn(m.transform.position, 1f, 3);
            }
        }
    }
}
