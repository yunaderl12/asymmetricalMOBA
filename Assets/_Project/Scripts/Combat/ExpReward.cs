using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Game.Core;

namespace Game.Combat
{
    [RequireComponent(typeof(HealthComponent))]
    public class ExpReward : MonoBehaviour
    {
        [SerializeField] private int exp = 10;

        public int Exp { get => exp; set => exp = Mathf.Max(0, value); }

        private HealthComponent health;

        private void Awake()
        {
            health = GetComponent<HealthComponent>();
        }

        private void OnEnable()
        {
            if (health != null) health.OnDeathDetailed += OnDeathDetailed;
        }

        private void OnDisable()
        {
            if (health != null) health.OnDeathDetailed -= OnDeathDetailed;
        }

        private void OnDeathDetailed(HealthComponent.DeathInfo info)
        {
            int killerExp = exp;
            int assistExp = exp / 2;

            ExpComponent killerEC = null;
            // 塔など経験値を持たない相手が倒したときは、最後に攻撃した勇者に入る。
            var killer = info.lastHitter != null && info.lastHitter.GetComponentInParent<ExpComponent>() != null ? info.lastHitter : health.LastAttackerWith<ExpComponent>();
            if (killer != null) killerEC = killer.GetComponentInParent<ExpComponent>();
            if (killerEC != null)
            {
                killerEC.AddExp(killerExp);
                Game.Skill.ExpOrbBurst.Spawn(transform.position + Vector3.up, killerEC.transform, killerExp);
            }

            var sb = new StringBuilder();
            int assistCount = 0;
            if (info.assistContributors != null)
            {
                foreach (var a in info.assistContributors)
                {
                    if (a == null) continue;
                    var ec = a.GetComponentInParent<ExpComponent>();
                    if (ec == null || ec == killerEC) continue;
                    ec.AddExp(assistExp);
                    sb.Append(a.name).Append(' ');
                    assistCount++;
                }
            }

            string killerName = killer != null ? killer.name : (info.lastHitter != null ? info.lastHitter.name : "?");
            string assistStr = assistCount > 0 ? sb.ToString().TrimEnd() : "-";
            Debug.Log($"[ExpReward] {name} killed by {killerName} (+{killerExp}), assists: {assistStr} (+{assistExp} each)");

            ShareWithSupporters(info);
        }

        /// Heroes who healed someone in this fight, but neither last-hit nor assisted, get a smaller share
        /// if they are near the kill.
        private void ShareWithSupporters(HealthComponent.DeathInfo info)
        {
            var cfg = BalanceConfig.Instance;
            int shareExp = Mathf.RoundToInt(exp * cfg.expShareRatio);
            if (shareExp <= 0) return;

            var combatants = new HashSet<GameObject>();
            var rewarded = new HashSet<ExpComponent>();
            AddCombatant(info.lastHitter, combatants, rewarded);
            if (info.assistContributors != null)
                foreach (var a in info.assistContributors) AddCombatant(a, combatants, rewarded);
            if (combatants.Count == 0) return;

            var sb = new StringBuilder();
            foreach (var supporter in SupportTracker.SupportersOf(combatants, cfg.expShareSupportWindow))
            {
                var ec = supporter.GetComponentInParent<ExpComponent>();
                if (ec == null || rewarded.Contains(ec)) continue;
                if (Vector3.Distance(ec.transform.position, transform.position) > cfg.expShareRadius) continue;
                ec.AddExp(shareExp);
                rewarded.Add(ec);
                sb.Append(ec.name).Append(' ');
            }
            if (sb.Length > 0) Debug.Log($"[ExpReward] {name} shared with supporters: {sb.ToString().TrimEnd()} (+{shareExp} each)");
        }

        private static void AddCombatant(GameObject go, HashSet<GameObject> combatants, HashSet<ExpComponent> rewarded)
        {
            if (go == null) return;
            var ec = go.GetComponentInParent<ExpComponent>();
            if (ec == null) return;
            combatants.Add(ec.gameObject);
            rewarded.Add(ec);
        }
    }
}
