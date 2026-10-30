using System.Collections.Generic;
using UnityEngine;
using Game.Combat;
using Game.Core;

namespace Game.Monster
{
    /// Hate table of one monster. Tracked on every monster, but only monsters with MonsterData.useHate
    /// (or all, while ForceAll is on for debugging) use it to pick their target.
    /// Gain: damage dealt to this monster (1 per damage), heals cast near this monster (amount x hateHealRatio), Taunt.
    /// Loss: decays by hateDecayPerSecond; reset to 0 when the hero dies or is farther than hateResetDistance.
    public class MonsterHate : MonoBehaviour
    {
        /// Debug: make every monster use hate (/debug hate on|off).
        public static bool ForceAll;

        private readonly Dictionary<GameObject, float> hate = new Dictionary<GameObject, float>();
        private readonly List<GameObject> scratch = new List<GameObject>();
        private HealthComponent health;

        public IReadOnlyDictionary<GameObject, float> Table => hate;

        private void Awake()
        {
            health = GetComponent<HealthComponent>();
        }

        private void OnEnable()
        {
            HealthComponent.AnyDamage += OnAnyDamage;
            SupportTracker.OnSupport += OnSupport;
        }

        private void OnDisable()
        {
            HealthComponent.AnyDamage -= OnAnyDamage;
            SupportTracker.OnSupport -= OnSupport;
        }

        public float Get(GameObject hero)
        {
            return hero != null && hate.TryGetValue(hero, out var v) ? v : 0f;
        }

        public void Add(GameObject hero, float amount)
        {
            if (hero == null || amount <= 0f) return;
            hate[hero] = Get(hero) + amount;
        }

        private void OnAnyDamage(HealthComponent victim, int amount, GameObject attacker)
        {
            if (victim != health || attacker == null) return;
            Add(attacker, amount);
        }

        private void OnSupport(GameObject supporter, GameObject recipient, int amount)
        {
            if (supporter == null || amount <= 0) return;
            var cfg = BalanceConfig.Instance;
            if (Vector3.Distance(transform.position, supporter.transform.position) > cfg.hateHealRadius) return;
            Add(supporter, amount * cfg.hateHealRatio);
        }

        private void Update()
        {
            if (hate.Count == 0) return;
            var cfg = BalanceConfig.Instance;
            float keep = Mathf.Max(0f, 1f - cfg.hateDecayPerSecond * Time.deltaTime);
            scratch.Clear();
            scratch.AddRange(hate.Keys);
            foreach (var hero in scratch)
            {
                if (hero == null || !hero.activeInHierarchy) { hate.Remove(hero); continue; }
                var hc = hero.GetComponent<HealthComponent>();
                if ((hc != null && hc.IsDead)
                    || Vector3.Distance(transform.position, hero.transform.position) > cfg.hateResetDistance)
                {
                    hate.Remove(hero);
                    continue;
                }
                float v = hate[hero] * keep;
                if (v < 0.5f) hate.Remove(hero);
                else hate[hero] = v;
            }
        }
    }
}
