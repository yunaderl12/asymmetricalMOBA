using System.Text;
using UnityEngine;

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
            if (info.lastHitter != null) killerEC = info.lastHitter.GetComponentInParent<ExpComponent>();
            if (killerEC != null) killerEC.AddExp(killerExp);

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

            string killerName = info.lastHitter != null ? info.lastHitter.name : "?";
            string assistStr = assistCount > 0 ? sb.ToString().TrimEnd() : "-";
            Debug.Log($"[ExpReward] {name} killed by {killerName} (+{killerExp}), assists: {assistStr} (+{assistExp} each)");
        }
    }
}
