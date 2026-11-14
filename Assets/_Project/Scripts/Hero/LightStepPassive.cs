using UnityEngine;
using Game.Combat;
using Game.Skill;

namespace Game.Hero
{
    /// 僧侶の「光の足取り」。回復スキルを使うと、しばらく足が速くなる。足元に光の粒が残る。
    public class LightStepPassive : HeroPassive
    {
        private const string ModifierId = "Passive_LightStep";
        private const float TrailInterval = 0.07f;

        private SkillRuntime runtime;
        private StatModifierContainer mods;
        private float timer;
        private float nextTrail;

        public override string GaugeLabel => "光の足取り";
        public override float GaugeFill => Data != null && Data.stepSeconds > 0f ? timer / Data.stepSeconds : 0f;
        public override bool GaugeLit => timer > 0f;

        protected override void OnInit()
        {
            runtime = GetComponent<SkillRuntime>();
            mods = GetComponent<StatModifierContainer>();
            if (runtime != null) runtime.OnSkillCast += OnCast;
        }

        private void OnDestroy()
        {
            if (runtime != null) runtime.OnSkillCast -= OnCast;
        }

        private void OnCast(int slot)
        {
            if (runtime == null || slot < 0 || slot >= runtime.EquippedSkills.Count) return;
            var skill = runtime.EquippedSkills[slot];
            if (skill == null || skill.effectType != SkillEffectType.Heal || mods == null) return;
            mods.AddOrReplaceModifier(new StatModifier(ModifierId, spd: Data.stepSpeedMultiplier));
            timer = Data.stepSeconds;
            LightPillar.Spawn(transform.position, 0.6f, 1.2f, 0.3f, new Color(1.3f, 1.2f, 0.7f, 1f), true);
        }

        private void Update()
        {
            if (timer <= 0f) return;
            timer -= Time.deltaTime;
            if (Time.time >= nextTrail && Body != null && Body.velocity.sqrMagnitude > 1f)
            {
                nextTrail = Time.time + TrailInterval;
                SparkleStar.Spawn(transform.position + Vector3.up * 0.1f + Random.insideUnitSphere * 0.25f, 0.09f, 0.45f, Vector3.up * 0.8f);
            }
            if (timer <= 0f && mods != null) mods.RemoveModifier(ModifierId);
        }
    }
}
