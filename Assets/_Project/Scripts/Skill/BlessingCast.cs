using UnityEngine;
using Game.Combat;

namespace Game.Skill
{
    /// 僧侶の Blessing。杖を振り出して味方へ光の玉を送り、相手がやわらかい光に包まれる。照準の先に味方がいなければ自分にかかる。効果の間は相手の体のまわりに光の粒が立ちのぼる。
    public class BlessingCast : PriestCast
    {
        private const float LightSeconds = 1.6f;

        protected override void OnRelease()
        {
            HealthComponent target = FindAllyInAim();
            bool self = target == null;
            if (self) target = GetComponent<HealthComponent>();
            if (target == null) return;

            ApplyHit(target);
            ReleaseFlash(1f);
            BlessEffect(target, LightSeconds);
            BuffAura.Attach(target.gameObject, Skill.duration, Palette.Light, SizeScale);
            if (!self) SendOrbs(target);
            Debug.Log($"[Skill] Blessing on {target.name}");
        }
    }
}
