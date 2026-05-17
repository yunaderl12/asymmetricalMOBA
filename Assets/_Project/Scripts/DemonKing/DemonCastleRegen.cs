using System.Collections.Generic;
using UnityEngine;
using Game.Combat;

namespace Game.DemonKing
{
    public class DemonCastleRegen : MonoBehaviour
    {
        [SerializeField] private int regenPerSecond = 10;

        private readonly Dictionary<ManaComponent, float> accumulator = new Dictionary<ManaComponent, float>();
        private readonly HashSet<GameObject> insideHosts = new HashSet<GameObject>();

        public bool IsHostInside(GameObject host)
        {
            return host != null && insideHosts.Contains(host);
        }

        private void OnTriggerEnter(Collider other)
        {
            var mp = other.GetComponentInParent<ManaComponent>();
            if (mp == null) return;
            if (!accumulator.ContainsKey(mp)) accumulator[mp] = 0f;
            insideHosts.Add(mp.gameObject);
            Debug.Log($"[DemonCastle] {mp.gameObject.name} entered Castle");
        }

        private void OnTriggerStay(Collider other)
        {
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
            var mp = other.GetComponentInParent<ManaComponent>();
            if (mp == null) return;
            accumulator.Remove(mp);
            insideHosts.Remove(mp.gameObject);
            Debug.Log($"[DemonCastle] {mp.gameObject.name} exited Castle");
        }
    }
}
