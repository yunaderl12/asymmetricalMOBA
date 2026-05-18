using System.Collections;
using UnityEngine;
using Game.Combat;

namespace Game.Core
{
    public class PowerItemBuffRunner : MonoBehaviour
    {
        private Coroutine activeRoutine;

        public void RunRemoval(float duration)
        {
            if (activeRoutine != null) StopCoroutine(activeRoutine);
            activeRoutine = StartCoroutine(RemoveAfter(duration));
        }

        private IEnumerator RemoveAfter(float t)
        {
            yield return new WaitForSeconds(t);
            var stats = GetComponent<StatModifierContainer>();
            if (stats != null) stats.RemoveModifier("PowerItem");
            Debug.Log("[PowerItem] Buff expired on " + name);
            activeRoutine = null;
        }
    }
}
