using UnityEngine;
using Game.Core;

namespace Game.Combat
{
    /// はじまりのまちの入口を守る塔。範囲内で一番近い敵（魔物と魔王）へ自動で矢を放つ。壊れない。
    public class ArcherTower : MonoBehaviour
    {
        private const float SearchInterval = 0.25f;
        private const float TurnDegreesPerSecond = 360f;
        private const float AimedDegrees = 25f;
        private const float MuzzleForward = 0.9f;
        private const float MuzzleHeight = 1.15f;
        private const string DemonKingTag = "DemonKing";

        // Blender で +x を向いて作った Tower_Archer と Tower_Arrow は、FBX 変換後に -x を向く。Unity の正面（+z）へ合わせる。
        private static readonly Quaternion ModelOffset = Quaternion.Euler(0f, 90f, 0f);

        [SerializeField] private Transform archer;
        [SerializeField] private GameObject arrowTemplate;

        private HealthComponent target;
        private float searchTimer;
        private float shotTimer;

        private void Awake()
        {
            if (arrowTemplate != null) arrowTemplate.SetActive(false);
        }

        private void Update()
        {
            searchTimer -= Time.deltaTime;
            shotTimer -= Time.deltaTime;
            var cfg = BalanceConfig.Instance;
            if (searchTimer <= 0f)
            {
                searchTimer = SearchInterval;
                target = FindTarget(cfg.archerTowerRange);
            }
            if (target == null || target.IsDead || archer == null) return;

            Vector3 flat = target.transform.position - archer.position;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.01f) return;
            Quaternion aim = Quaternion.LookRotation(flat) * ModelOffset;
            archer.rotation = Quaternion.RotateTowards(archer.rotation, aim, TurnDegreesPerSecond * Time.deltaTime);
            if (shotTimer > 0f || Quaternion.Angle(archer.rotation, aim) > AimedDegrees) return;

            shotTimer = cfg.archerTowerInterval;
            Vector3 muzzle = archer.position + flat.normalized * MuzzleForward + Vector3.up * MuzzleHeight;
            ArcherArrow.Launch(arrowTemplate, muzzle, target, gameObject, cfg.archerTowerDamage, cfg.archerArrowSpeed, ModelOffset);
        }

        private HealthComponent FindTarget(float range)
        {
            HealthComponent best = null;
            float bestDistance = float.MaxValue;
            foreach (var col in Physics.OverlapSphere(transform.position, range, ~0, QueryTriggerInteraction.Collide))
            {
                var hp = col.GetComponentInParent<HealthComponent>();
                if (hp == null || hp.IsDead || !IsEnemy(hp)) continue;
                float distance = (hp.transform.position - transform.position).sqrMagnitude;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = hp;
            }
            return best;
        }

        private static bool IsEnemy(HealthComponent hp)
        {
            if (hp.CompareTag(DemonKingTag) || hp.GetComponent<Game.Bot.BotDemonKing>() != null) return true;
            var monster = hp.GetComponent<Game.Monster.Monster>();
            return monster != null && !monster.IsEmerging;
        }
    }
}
