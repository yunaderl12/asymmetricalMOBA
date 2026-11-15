using UnityEngine;
using Game.Combat;

namespace Game.Skill
{
    /// 僧侶の Heal。杖を振り出して味方へ光の玉を送り、相手がやわらかい光に包まれる。照準の先に味方がいなければ自分を回復する。
    public class HealCast : PriestCast
    {
        private const float LightSeconds = 1.1f;

        protected override void OnRelease()
        {
            HealthComponent target = FindAllyInAim();
            bool self = target == null;
            if (self) target = GetComponent<HealthComponent>();
            if (target == null) return;

            ApplyHit(target);
            ReleaseFlash(0.8f);
            BlessEffect(target, LightSeconds);
            if (!self) SendOrbs(target);
            Debug.Log($"[Skill] Heal on {target.name}");
        }
    }
}
