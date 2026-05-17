using System.Collections.Generic;
using UnityEngine;
using Game.Combat;
using Game.Monster;

namespace Game.DemonKing
{
    public class CommanderAura : MonoBehaviour
    {
        [SerializeField] private float auraRadius = 10f;
        [SerializeField] private float atkBoost = 1.1f;
        [SerializeField] private float spdBoost = 1.1f;
        [SerializeField] private float checkInterval = 0.2f;
        [SerializeField] private string sourceId = "CommanderAura";

        private readonly HashSet<Monster.Monster> buffed = new HashSet<Monster.Monster>();

        private void Start()
        {
            InvokeRepeating(nameof(CheckAura), 0f, checkInterval);
        }

        private void OnDisable()
        {
            CancelInvoke(nameof(CheckAura));
            // Clean up buffs so they don't linger if disabled mid-frame
            foreach (var m in buffed)
            {
                if (m == null) continue;
                var smc = m.GetComponent<StatModifierContainer>();
                if (smc != null) smc.RemoveModifier(sourceId);
            }
            buffed.Clear();
        }

        private void CheckAura()
        {
            var hits = Physics.OverlapSphere(transform.position, auraRadius);
            var inRange = new HashSet<Monster.Monster>();
            foreach (var h in hits)
            {
                var m = h.GetComponentInParent<Monster.Monster>();
                if (m != null) inRange.Add(m);
            }

            foreach (var m in inRange)
            {
                if (buffed.Contains(m)) continue;
                var smc = m.GetComponent<StatModifierContainer>();
                if (smc == null) continue;
                smc.AddOrReplaceModifier(new StatModifier(sourceId, atk: atkBoost, spd: spdBoost));
                buffed.Add(m);
                Debug.Log($"[CommanderAura] entered {m.name}");
            }

            var leaving = new List<Monster.Monster>();
            foreach (var m in buffed)
            {
                if (m == null || !inRange.Contains(m)) leaving.Add(m);
            }
            foreach (var m in leaving)
            {
                if (m != null)
                {
                    var smc = m.GetComponent<StatModifierContainer>();
                    if (smc != null) smc.RemoveModifier(sourceId);
                    Debug.Log($"[CommanderAura] exited {m.name}");
                }
                buffed.Remove(m);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.6f, 0f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, auraRadius);
        }
    }
}
