using System.Collections.Generic;
using UnityEngine;

namespace Game.Combat
{
    /// Remembers who healed whom recently, so exp can be shared with supporters of a fight.
    public static class SupportTracker
    {
        private struct Record
        {
            public GameObject supporter;
            public GameObject recipient;
            public float time;
        }

        private const float MaxKeepSeconds = 30f;
        private static readonly List<Record> records = new List<Record>();

        public static void RecordSupport(GameObject supporter, GameObject recipient)
        {
            if (supporter == null || recipient == null || supporter == recipient) return;
            float now = Time.time;
            records.RemoveAll(r => r.supporter == null || r.recipient == null || now - r.time > MaxKeepSeconds);
            records.Add(new Record { supporter = supporter, recipient = recipient, time = now });
        }

        /// Supporters who healed / buffed any of the combatants within the last `window` seconds.
        public static HashSet<GameObject> SupportersOf(ICollection<GameObject> combatants, float window)
        {
            var result = new HashSet<GameObject>();
            float now = Time.time;
            foreach (var r in records)
            {
                if (r.supporter == null || r.recipient == null) continue;
                if (now - r.time > window) continue;
                if (combatants.Contains(r.recipient)) result.Add(r.supporter);
            }
            return result;
        }
    }
}
