using System.Collections.Generic;
using UnityEngine;
using Game.Combat;
using Game.Skill;

namespace Game.Hero
{
    /// 僧侶の「癒しの波紋」。回復・バフを当てると、当てた味方の周りにいる別の味方にも効果の一部が広がる。
    /// 1回の発動で広がるのは1人1回まで（範囲回復で何重にもかからないように）。直接当たった味方には広がらない。
    public class HealRipplePassive : HeroPassive
    {
        private static readonly Color RippleColor = new Color(0.55f, 1f, 0.6f, 0.9f);

        private SkillExecutor executor;
        private SkillData pendingSkill;
        private float pendingValue;
        private readonly List<HealthComponent> pendingTargets = new List<HealthComponent>();
        private readonly HashSet<HealthComponent> shared = new HashSet<HealthComponent>();

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
            if (skill == null || target == null) return;
            if (pendingSkill != skill) pendingValue = 0f;
            pendingSkill = skill;
            pendingValue = Mathf.Max(pendingValue, value);
            if (!pendingTargets.Contains(target)) pendingTargets.Add(target);
        }

        // その発動で当たった相手が出そろってから、まとめて広げる。
        private void LateUpdate()
        {
            if (pendingTargets.Count == 0) return;
            shared.Clear();
            foreach (var go in GameObject.FindGameObjectsWithTag("Player"))
            {
                var hp = go.GetComponent<HealthComponent>();
                if (hp == null || hp.IsDead || pendingTargets.Contains(hp)) continue;
                foreach (var t in pendingTargets)
                {
                    if (t == null || Vector3.Distance(t.transform.position, hp.transform.position) > Data.rippleRadius) continue;
                    shared.Add(hp);
                    break;
                }
            }
            foreach (var t in pendingTargets)
                if (t != null) ShockRing.Spawn(t.transform.position + Vector3.up * 0.1f, 0.3f, Data.rippleRadius, 0.5f, 0.14f, RippleColor);
            foreach (var hp in shared)
            {
                executor.ShareSupport(hp, pendingSkill, pendingValue, Data.rippleShare);
                for (int i = 0; i < 4; i++)
                    SparkleStar.Spawn(hp.transform.position + Vector3.up * Random.Range(0.6f, 1.6f) + Random.insideUnitSphere * 0.4f, 0.1f, 0.5f, Vector3.up * Random.Range(1f, 2f));
            }
            pendingTargets.Clear();
            pendingSkill = null;
            pendingValue = 0f;
        }
    }
}
