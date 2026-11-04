using UnityEngine;
using Game.Combat;

namespace Game.Skill
{
    /// 僧侶の Heal。杖を味方へ向けて光を送り、相手の足元から光の柱が立つ。照準の先に味方がいなければ自分を回復する。
    public class HealCast : PriestCast
    {
        private const float PillarRadius = 0.8f;
        private const float PillarHeight = 3f;
        private const float PillarSeconds = 0.7f;

        protected override void OnRelease()
        {
            HealthComponent target = FindAllyInAim();
            bool self = target == null;
            if (self) target = GetComponent<HealthComponent>();
            if (target == null) return;

            ApplyHit(target);
            ReleaseFlash(1.4f);
            Vector3 feet = target.transform.position;
            BlessEffect(target, PillarRadius * SizeScale, PillarHeight * SizeScale, PillarSeconds);
            ShockRing.Spawn(feet, 0.2f, 1.6f * SizeScale, 0.4f, 0.25f, Palette.Light);
            if (!self) SpawnBeam(WandTip, feet + Vector3.up * 1.1f);
            Debug.Log($"[Skill] Heal on {target.name}");
        }
    }
}
