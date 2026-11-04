using UnityEngine;

namespace Game.Skill
{
    /// 僧侶の Blessing。本を開いて杖を掲げ、光の柱に包まれる。効果の間は体のまわりに光の粒が立ちのぼる。
    public class BlessingCast : PriestCast
    {
        private const float PillarRadius = 1f;
        private const float PillarHeight = 4f;
        private const float PillarSeconds = 0.9f;

        protected override void OnRelease()
        {
            ReleaseFlash(2f);
            LightPillar.Spawn(transform.position, PillarRadius * SizeScale, PillarHeight * SizeScale, PillarSeconds, Palette.Main * 1.8f, true, SelfOnlyHiddenLayer);
            ShockRing.Spawn(transform.position, 0.3f, 2.4f * SizeScale, 0.45f, 0.3f, Palette.Core);
            FireballVfx.SpawnSparks(Chest, Vector3.up, Mathf.RoundToInt(24 * SizeScale), Color.white, Palette.Spark);
            BuffAura.Attach(gameObject, Skill.duration, Palette.Light, SizeScale);
        }
    }
}
