using UnityEngine;

namespace Game.Skill
{
    /// 僧侶の GroupHeal。杖を高く掲げて振り出すと、足元から光の粒が波のように広がり、範囲内の味方全員がやわらかい光に包まれる。
    public class GroupHealCast : PriestCast
    {
        private const float LightSeconds = 1.3f;
        private const float WaveSeconds = 0.75f;


        protected override void OnRelease()
        {
            ReleaseFlash(1.2f);
            GentleLight.Wave(transform.position, ScaledRange, WaveSeconds, SoftLight);

            var allies = FindAllies(Chest, ScaledRange);
            for (int i = 0; i < allies.Count; i++)
            {
                ApplyHit(allies[i]);
                BlessEffect(allies[i], LightSeconds);
            }
            Debug.Log($"[Skill] GroupHeal healed {allies.Count} ally(s)");
        }
    }
}
