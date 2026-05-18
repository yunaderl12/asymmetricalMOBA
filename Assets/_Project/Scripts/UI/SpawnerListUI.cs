using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Game.Combat;
using Game.Core;
using Game.Monster;

namespace Game.UI
{
    public class SpawnerListUI : MonoBehaviour
    {
        [SerializeField] private string spawnerTag = "Spawner";
        [SerializeField] private Image[] hpFills;
        [SerializeField] private Text[] labels;

        private readonly List<Binding> bindings = new List<Binding>();

        private class Binding
        {
            public Spawner spawner;
            public HealthComponent health;
            public LevelComponent level;
            public Image hpFill;
            public Text label;
        }

        private void Start()
        {
            var spawners = GameObject.FindGameObjectsWithTag(spawnerTag);
            var sorted = new List<GameObject>(spawners);
            sorted.Sort((a, b) =>
            {
                int za = a.transform.position.z > 0f ? 0 : a.transform.position.z < 0f ? 2 : 1;
                int zb = b.transform.position.z > 0f ? 0 : b.transform.position.z < 0f ? 2 : 1;
                if (za != zb) return za.CompareTo(zb);
                return a.transform.position.x.CompareTo(b.transform.position.x);
            });

            for (int i = 0; i < sorted.Count && i < 5; i++)
            {
                var go = sorted[i];
                var b = new Binding
                {
                    spawner = go.GetComponent<Spawner>(),
                    health = go.GetComponent<HealthComponent>(),
                    level = go.GetComponent<LevelComponent>(),
                    hpFill = (hpFills != null && i < hpFills.Length) ? hpFills[i] : null,
                    label = (labels != null && i < labels.Length) ? labels[i] : null,
                };
                if (b.health != null) b.health.OnHPChanged += (cur, max) => Refresh(b);
                if (b.level != null) b.level.OnLevelChanged += lv => Refresh(b);
                bindings.Add(b);
                Refresh(b);
            }
        }

        private void Refresh(Binding b)
        {
            if (b.hpFill != null && b.health != null)
                b.hpFill.fillAmount = b.health.MaxHP > 0 ? (float)b.health.CurrentHP / b.health.MaxHP : 0f;

            if (b.label != null)
            {
                string name = b.spawner != null ? b.spawner.name : "Spawner";
                int curHp = b.health != null ? b.health.CurrentHP : 0;
                int maxHp = b.health != null ? b.health.MaxHP : 0;
                int lv = b.level != null ? b.level.CurrentLevel : 1;
                string inv = "-";
                if (b.spawner != null)
                {
                    var list = b.spawner.SpawnedMonsters; // not exactly inv but a usable proxy
                    inv = list != null && list.Count > 0 ? list.Count + " active" : "ready";
                }
                b.label.text = $"{name}\nHP {curHp}/{maxHp}  Lv {lv}  ({inv})";
            }
        }
    }
}
