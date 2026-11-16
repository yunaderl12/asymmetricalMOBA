using System.Collections.Generic;
using UnityEngine;
using Game.Combat;
using Game.Core;
using Game.Skill;

namespace Game.DemonKing
{
    /// 魔王本体の剣の一振り（仮。魔王スキルが決まるまでのつなぎ）。人間の魔王も BOT 魔王も同じものを使う。
    ///  - 前方の扇（BalanceConfig.dkAttackRange / dkAttackArc）にいる勇者へ、物理の攻撃値を当てる（勇者の Df で減る）。
    ///  - 攻撃値は魔王レベルで伸び（dkAttackGrowthPerLevel）、城外デバフなど StatModifierContainer の Atk 倍率が掛かる。
    ///  - 押してから当たるまで少し間を置き（dkAttackWindup）、振りかぶりが勇者に見えるようにする。
    ///  - 人間の魔王は TPS の視点で「本体で斬る」キー（初期は左クリック）。フィールド魔法を狙っている間・俯瞰中・メニュー中は振らない。
    public class DemonKingAttack : MonoBehaviour
    {
        private const string HeroTag = "Player";
        private const float HitHeight = 1.2f;
        private const float FlashSize = 1.4f;

        private static readonly Collider[] Hits = new Collider[32];

        private StatModifierContainer mods;
        private LevelComponent level;
        private HealthComponent health;
        private DemonKingController player;
        private DemonKingViewSwitcher view;
        private FieldSpellCaster spells;
        private Game.Hero.CharacterModelAnimator body;
        private float readyAt;
        private float hitAt = -1f;
        private readonly HashSet<HealthComponent> struck = new HashSet<HealthComponent>();

        /// 今の一振りが当たる攻撃値（勇者の Df で減る前）。
        public int AttackPower
        {
            get
            {
                var cfg = BalanceConfig.Instance;
                int lv = level != null ? level.CurrentLevel : 1;
                float growth = 1f + cfg.dkAttackGrowthPerLevel * Mathf.Max(0, lv - 1);
                float mul = mods != null ? mods.TotalAtkMultiplier : 1f;
                return Mathf.Max(1, Mathf.RoundToInt(cfg.dkAttackPower * growth * mul));
            }
        }

        public bool Ready => Time.time >= readyAt && hitAt < 0f;
        public bool Swinging => hitAt >= 0f;

        public static DemonKingAttack Ensure(GameObject demonKing)
        {
            var attack = demonKing.GetComponent<DemonKingAttack>();
            return attack != null ? attack : demonKing.AddComponent<DemonKingAttack>();
        }

        private void Awake()
        {
            mods = GetComponent<StatModifierContainer>();
            level = GetComponent<LevelComponent>();
            health = GetComponent<HealthComponent>();
            player = GetComponent<DemonKingController>();
            view = GetComponent<DemonKingViewSwitcher>();
        }

        /// 届く距離にいるか（相手の体の表面まで）。
        public static bool InReach(Transform from, HealthComponent target)
        {
            if (target == null) return false;
            Vector3 point = ClosestPoint(target, from.position);
            Vector3 flat = point - from.position;
            flat.y = 0f;
            return flat.magnitude <= BalanceConfig.Instance.dkAttackRange;
        }

        /// 振る。間隔が空いていない・倒れている・スタン中なら振らない。
        public bool TryAttack()
        {
            if (!Ready) return false;
            if (health != null && health.IsDead) return false;
            if (StatusEffectController.IsStunnedObj(gameObject)) return false;
            var cfg = BalanceConfig.Instance;
            readyAt = Time.time + cfg.dkAttackInterval;
            hitAt = Time.time + cfg.dkAttackWindup;
            if (body == null) body = GetComponent<Game.Hero.CharacterModelAnimator>();
            if (body != null) body.PlayAttack();
            return true;
        }

        private void Update()
        {
            if (player != null && PlayerWantsToAttack()) TryAttack();
            if (hitAt >= 0f && Time.time >= hitAt)
            {
                hitAt = -1f;
                if (health == null || !health.IsDead) Strike();
            }
        }

        private bool PlayerWantsToAttack()
        {
            if (!player.enabled || !player.MovementEnabled) return false;
            if (Game.UI.InGameMenu.BlocksGameInput) return false;
            if (view != null && view.CurrentMode != DemonKingViewSwitcher.ViewMode.Tps) return false;
            if (spells == null) spells = GetComponent<FieldSpellCaster>();
            // フィールド魔法の狙いの左クリック（と、そのすぐあと）では振らない。
            if (spells != null && spells.UsesClick) return false;
            return KeyBindings.WasPressed(GameKey.DemonAttack);
        }

        // 前方の扇に入った勇者に当てる。同じ相手には1回だけ。
        private void Strike()
        {
            var cfg = BalanceConfig.Instance;
            Vector3 origin = transform.position + Vector3.up * HitHeight;
            Vector3 forward = transform.forward;
            forward.y = 0f;
            forward.Normalize();
            float halfArc = cfg.dkAttackArc * 0.5f;
            int power = AttackPower;
            struck.Clear();
            int n = Physics.OverlapSphereNonAlloc(origin, cfg.dkAttackRange + 1f, Hits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var hp = Hits[i].GetComponentInParent<HealthComponent>();
                if (hp == null || hp.IsDead || !hp.CompareTag(HeroTag) || !struck.Add(hp)) continue;
                Vector3 point = ClosestPoint(hp, transform.position);
                Vector3 flat = point - transform.position;
                flat.y = 0f;
                if (flat.magnitude > cfg.dkAttackRange) continue;
                if (flat.sqrMagnitude > 0.01f && Vector3.Angle(forward, flat) > halfArc) continue;
                hp.TakeDamage(power, gameObject, DamageType.Physical);
                Vector3 at = FireballProjectile.BodyBoundsOf(hp.gameObject, null).center;
                MeleeFx.Impact(at, at - origin, FlashSize, MeleeFx.Ember);
                FireHitEffect.PlayFlash(hp.gameObject);
                ImpactPunch.Play(hp.gameObject, flat);
            }
            // はじまりのまちのコアにも当たる（最終局面の進軍で、魔王がまちまで攻め込んだとき）。
            if (townCore == null) townCore = FindTownCore();
            if (townCore != null && !townCore.IsDead)
            {
                Vector3 point = ClosestPoint(townCore, transform.position);
                Vector3 flat = point - transform.position;
                flat.y = 0f;
                if (flat.magnitude <= cfg.dkAttackRange && (flat.sqrMagnitude <= 0.01f || Vector3.Angle(forward, flat) <= halfArc))
                {
                    townCore.TakeDamage(power, gameObject, DamageType.Physical);
                    MeleeFx.Impact(point, point - origin, FlashSize, MeleeFx.Ember);
                    struck.Add(townCore);
                }
            }
            if (struck.Count > 0) Debug.Log($"[DemonKingAttack] {name} swung (atk {power}) and hit {struck.Count} target(s)");
        }

        public const string TownCoreName = "StartTown_Core";
        private static HealthComponent townCore;

        public static HealthComponent FindTownCore()
        {
            if (townCore != null) return townCore;
            var go = GameObject.Find(TownCoreName);
            townCore = go != null ? go.GetComponent<HealthComponent>() : null;
            return townCore;
        }

        private static Vector3 ClosestPoint(HealthComponent target, Vector3 from)
        {
            var col = target.GetComponent<Collider>();
            return col != null && !col.isTrigger ? col.ClosestPoint(from) : target.transform.position;
        }
    }
}
