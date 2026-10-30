using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Combat;

namespace Game.DemonKing
{
    /// Castle interior (RegenTrigger): restores the Demon King's MP, and tracks heroes who break in.
    public class DemonCastleRegen : MonoBehaviour
    {
        [SerializeField] private int regenPerSecond = 10;
        [SerializeField] private string heroTag = "Player";

        private readonly Dictionary<ManaComponent, float> accumulator = new Dictionary<ManaComponent, float>();
        private readonly HashSet<GameObject> insideHosts = new HashSet<GameObject>();
        private readonly HashSet<HealthComponent> heroesInside = new HashSet<HealthComponent>();

        /// Fired when a hero enters the castle interior (not on approach).
        public static event Action<GameObject> OnHeroEnteredCastle;

        public bool IsHostInside(GameObject host)
        {
            return host != null && insideHosts.Contains(host);
        }

        public bool IsHeroInside(GameObject hero)
        {
            if (hero == null) return false;
            var hc = hero.GetComponent<HealthComponent>();
            return hc != null && !hc.IsDead && heroesInside.Contains(hc);
        }

        private HealthComponent GetHero(Collider other)
        {
            var hc = other.GetComponentInParent<HealthComponent>();
            return hc != null && hc.CompareTag(heroTag) ? hc : null;
        }

        private void OnTriggerEnter(Collider other)
        {
            // Heroes: intrusion only. Their SP must not regenerate from the castle.
            var hero = GetHero(other);
            if (hero != null)
            {
                if (heroesInside.Add(hero))
                {
                    Debug.Log($"[DemonCastle] hero {hero.name} entered Castle");
                    OnHeroEnteredCastle?.Invoke(hero.gameObject);
                }
                return;
            }

            var mp = other.GetComponentInParent<ManaComponent>();
            if (mp == null) return;
            if (!accumulator.ContainsKey(mp)) accumulator[mp] = 0f;
            insideHosts.Add(mp.gameObject);
            Debug.Log($"[DemonCastle] {mp.gameObject.name} entered Castle");
        }

        private void OnTriggerStay(Collider other)
        {
            if (GetHero(other) != null) return;
            var mp = other.GetComponentInParent<ManaComponent>();
            if (mp == null) return;
            if (!accumulator.ContainsKey(mp)) accumulator[mp] = 0f;
            insideHosts.Add(mp.gameObject);

            accumulator[mp] += regenPerSecond * Time.deltaTime;
            int whole = Mathf.FloorToInt(accumulator[mp]);
            if (whole > 0)
            {
                mp.RestoreMana(whole);
                accumulator[mp] -= whole;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            var hero = GetHero(other);
            if (hero != null)
            {
                if (heroesInside.Remove(hero)) Debug.Log($"[DemonCastle] hero {hero.name} exited Castle");
                return;
            }

            var mp = other.GetComponentInParent<ManaComponent>();
            if (mp == null) return;
            accumulator.Remove(mp);
            insideHosts.Remove(mp.gameObject);
            Debug.Log($"[DemonCastle] {mp.gameObject.name} exited Castle");
        }
    }
}
