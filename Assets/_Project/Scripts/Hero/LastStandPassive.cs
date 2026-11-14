using UnityEngine;
using Game.Combat;
using Game.Skill;

namespace Game.Hero
{
    /// 重戦士の「背水の構え」。HP が一定の割合を切ると、しばらく Df と mDf が上がる。クールタイムあり。
    public class LastStandPassive : HeroPassive
    {
        private const string ModifierId = "Passive_LastStand";
        private static readonly Color GuardColor = new Color(1.5f, 1.05f, 0.35f, 1f);

        private StatModifierContainer mods;
        private float activeTimer;
        private float cooldownTimer;
        private float nextSpark;

        public override string GaugeLabel => "背水の構え";
        public override float GaugeFill
        {
            get
            {
                if (Data == null) return 0f;
                if (activeTimer > 0f) return activeTimer / Mathf.Max(0.01f, Data.lastStandSeconds);
                return Data.cooldown > 0f ? 1f - cooldownTimer / Data.cooldown : 1f;
            }
        }
        public override bool GaugeLit => activeTimer > 0f;

        protected override void OnInit()
        {
            mods = GetComponent<StatModifierContainer>();
            if (Health != null) Health.OnHPChanged += OnHpChanged;
        }

        private void OnDestroy()
        {
            if (Health != null) Health.OnHPChanged -= OnHpChanged;
        }

        private void OnHpChanged(int current, int max)
        {
            if (activeTimer > 0f || cooldownTimer > 0f || Health.IsDead || current <= 0) return;
            if (current >= max * Data.lastStandHpRatio) return;
            activeTimer = Data.lastStandSeconds;
            cooldownTimer = Data.cooldown;
            if (mods != null)
                mods.AddOrReplaceModifier(new StatModifier(ModifierId, df: Data.lastStandDefenseMultiplier) { mdfMultiplier = Data.lastStandDefenseMultiplier });
            Vector3 feet = transform.position;
            ShockRing.Spawn(feet + Vector3.up * 0.1f, 0.4f, 3.2f, 0.4f, 0.3f, GuardColor);
            LightPillar.Spawn(feet, 0.9f, 1.1f, 0.45f, GuardColor, true); // 目の高さより低く（画面が色づかないように）
            if (Controller != null) Controller.Shake(0.1f, 0.3f);
            Debug.Log($"[Passive] Last Stand: Df/mDf x{Data.lastStandDefenseMultiplier} for {Data.lastStandSeconds}s");
        }

        private void Update()
        {
            if (cooldownTimer > 0f) cooldownTimer -= Time.deltaTime;
            if (activeTimer <= 0f) return;
            activeTimer -= Time.deltaTime;
            if (Time.time >= nextSpark)
            {
                nextSpark = Time.time + 0.12f;
                Vector2 p = Random.insideUnitCircle.normalized * 0.7f;
                SparkleStar.Spawn(transform.position + new Vector3(p.x, Random.Range(0.2f, 1.6f), p.y), 0.1f, 0.5f, Vector3.up * 1.2f);
            }
            if (activeTimer <= 0f && mods != null) mods.RemoveModifier(ModifierId);
        }
    }
}
