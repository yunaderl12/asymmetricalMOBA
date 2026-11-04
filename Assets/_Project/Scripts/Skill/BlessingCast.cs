using UnityEngine;
using Game.Combat;

namespace Game.Skill
{
    /// 僧侶の Blessing。杖を味方へ向けて光を送り、相手が光の柱に包まれる。照準の先に味方がいなければ自分にかかる。効果の間は相手の体のまわりに光の粒が立ちのぼる。
    public class BlessingCast : PriestCast
    {
        private const float PillarRadius = 1f;
        private const float PillarHeight = 4f;
        private const float PillarSeconds = 0.9f;

        protected override void OnRelease()
        {
            HealthComponent target = FindAllyInAim();
            bool self = target == null;
            if (self) target = GetComponent<HealthComponent>();
            if (target == null) return;

            ApplyHit(target);
            ReleaseFlash(2f);
            Vector3 feet = target.transform.position;
            LightPillar.Spawn(feet, PillarRadius * SizeScale, PillarHeight * SizeScale, PillarSeconds, Palette.Main * 1.8f, true, PillarLayerFor(target));
            ShockRing.Spawn(feet, 0.3f, 2.4f * SizeScale, 0.45f, 0.3f, Palette.Core);
            FireballVfx.SpawnSparks(feet + Vector3.up * 1.2f, Vector3.up, Mathf.RoundToInt(24 * SizeScale), Color.white, Palette.Spark);
            BuffAura.Attach(target.gameObject, Skill.duration, Palette.Light, SizeScale);
            if (!self) SpawnBeam(WandTip, feet + Vector3.up * 1.1f);
            Debug.Log($"[Skill] Blessing on {target.name}");
        }
    }
}
