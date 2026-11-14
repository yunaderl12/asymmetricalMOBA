using UnityEngine;
using Game.Combat;
using Game.Skill;

namespace Game.Hero
{
    /// 僧侶の「浄化の手」。回復を当てた味方のスロー・継続ダメージ・沈黙を解く（スタンは解かない）。
    public class CleansePassive : HeroPassive
    {
        private static readonly Color CleanseColor = new Color(1.4f, 1.4f, 1.2f, 1f);

        private SkillExecutor executor;

        protected override void OnInit()
        {
            executor = GetComponent<SkillExecutor>();
            if (executor != null) executor.SupportLanded += OnSupport;
        }

        private void OnDestroy()
        {
            if (executor != null) executor.SupportLanded -= OnSupport;
        }

        private void OnSupport(SkillData skill, HealthComponent target, float value)
        {
            if (skill == null || target == null || skill.effectType != SkillEffectType.Heal) return;
            var status = target.GetComponent<StatusEffectController>();
            if (status == null) return;
            bool cleared = status.Clear(StatusEffectType.Slow);
            cleared |= status.Clear(StatusEffectType.DamageOverTime);
            cleared |= status.Clear(StatusEffectType.Silence);
            if (!cleared) return;
            Vector3 feet = target.transform.position;
            RuneFlash.Spawn(feet + Vector3.up * 0.05f, Quaternion.Euler(90f, 0f, 0f), 0.9f, 0.45f, CleanseColor);
            for (int i = 0; i < 8; i++)
                SparkleStar.Spawn(feet + Vector3.up * Random.Range(0.3f, 1.7f) + Random.insideUnitSphere * 0.5f, 0.12f, 0.6f, Vector3.up * Random.Range(1.5f, 3f));
            Debug.Log($"[Passive] Cleanse cleared statuses on {target.name}");
        }
    }
}
