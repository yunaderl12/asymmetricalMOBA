using UnityEngine;

namespace Game.Skill
{
    /// 僧侶の GroupHeal。杖を高く掲げ、足元から金色の輪を広げる。範囲内の味方全員の足元に光の柱が立つ。
    public class GroupHealCast : PriestCast
    {
        private const float PillarRadius = 0.9f;
        private const float PillarHeight = 3.4f;
        private const float PillarSeconds = 0.8f;

        protected override void OnWindUp(float k)
        {
            base.OnWindUp(k);
            if (k > 0f && k - Time.deltaTime / Mathf.Max(Skill.castTime, 0.01f) <= 0f)
                ShockRing.Spawn(transform.position, ScaledRange, 0.5f, Skill.castTime, 0.2f, Palette.Main);
        }

        protected override void OnRelease()
        {
            ReleaseFlash(2.4f);
            ShockRing.Spawn(transform.position, 0.5f, ScaledRange, 0.5f, 0.7f, Palette.Core);
            ShockRing.Spawn(transform.position, 0.3f, ScaledRange * 0.6f, 0.6f, 0.35f, Palette.Light);

            var allies = FindAllies(Chest, ScaledRange);
            for (int i = 0; i < allies.Count; i++)
            {
                ApplyHit(allies[i]);
                BlessEffect(allies[i].transform.position, PillarRadius * SizeScale, PillarHeight * SizeScale, PillarSeconds, allies[i].gameObject == gameObject);
            }
            Debug.Log($"[Skill] GroupHeal healed {allies.Count} ally(s)");
            Shake(0.18f, 0.025f * SizeScale);
        }
    }
}
