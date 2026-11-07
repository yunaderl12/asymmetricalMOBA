using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Game.Combat;
using Game.Core;
using Game.Hero;
using Game.Skill;

namespace Game.Bot
{
    /// BOTの勇者のスキル使用。人間と同じ SkillRuntime / SkillExecutor を通すので、消費MP・クールタイム・演出は人間と変わらない。
    public class BotSkillUser : MonoBehaviour
    {
        private const float EyeHeight = 1.6f;
        private const float BodyCenterHeight = 1.1f;
        private const float EmergencyHpRatio = 0.4f;
        private const float BlessedFightRangeScale = 2f;

        private static readonly List<HealthComponent> AllyBuffer = new List<HealthComponent>();

        private BotHero bot;
        private HeroClassData classData;
        private HealthComponent health;
        private NavMeshAgent agent;
        private SkillRuntime runtime;
        private SkillExecutor executor;
        private ManaComponent mana;
        private Transform aim;
        private Transform focus;
        private bool wasCasting;
        private float nextThink;
        private float nextCast;
        private float nextRegen;

        public bool IsCasting => executor != null && executor.IsBusy;
        /// このBOTのジョブ。HUDの味方一覧で肖像とジョブ名を出すのに使う。
        public HeroClassData ClassData => classData;

        public void Init(BotHero owner, HeroClassData data, SkillData freeSlotSkill)
        {
            bot = owner;
            classData = data;
            health = GetComponent<HealthComponent>();
            agent = GetComponent<NavMeshAgent>();

            mana = gameObject.AddComponent<ManaComponent>();
            mana.SetMaxMP(data.initialMP);
            runtime = gameObject.AddComponent<SkillRuntime>();
            runtime.Equip(data.classSkills, freeSlotSkill);
            executor = gameObject.AddComponent<SkillExecutor>();

            var aimObject = new GameObject("BotAim");
            aim = aimObject.transform;
            aim.SetParent(transform, false);
            aim.localPosition = Vector3.up * EyeHeight;
            executor.SetAim(aim);

            // 開幕で全員が同時に動かないようにずらす。
            nextCast = Time.time + Random.Range(0f, BalanceConfig.Instance.botSkillCastGap);
        }

        private void Update()
        {
            if (executor == null || health == null || health.IsDead) return;
            RegenMp();

            bool casting = IsCasting;
            if (casting != wasCasting) SetCastingMotion(casting);
            wasCasting = casting;
            if (casting)
            {
                PointAt(focus);
                return;
            }

            if (Time.time < nextThink) return;
            nextThink = Time.time + BalanceConfig.Instance.botSkillThinkInterval;
            if (Time.time < nextCast || !StatusEffectController.CanUseSkills(gameObject)) return;
            Think();
        }

        // 人間の HeroRegen の「戦闘中でも回復する分」と同じ割合。
        private void RegenMp()
        {
            if (Time.time < nextRegen) return;
            nextRegen = Time.time + BalanceConfig.Instance.botMpRegenInterval;
            mana.RestoreFraction(mana.MaxMP * classData.mpCombatRegenRate);
        }

        private void SetCastingMotion(bool casting)
        {
            if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;
            agent.isStopped = casting;
            agent.updateRotation = !casting;
        }

        // 回復と強化を先に考え、その後で攻撃を考える。毎回同じ順に撃たないよう、開始位置はランダムにする。
        private void Think()
        {
            if (TryCastFrom(true)) return;
            TryCastFrom(false);
        }

        private bool TryCastFrom(bool support)
        {
            int start = Random.Range(0, SkillRuntime.SLOT_COUNT);
            for (int i = 0; i < SkillRuntime.SLOT_COUNT; i++)
            {
                int slot = (start + i) % SkillRuntime.SLOT_COUNT;
                var skill = runtime.EquippedSkills[slot];
                if (skill == null || !runtime.CanCast(slot)) continue;
                bool isSupport = skill.effectType == SkillEffectType.Heal || skill.effectType == SkillEffectType.Buff;
                if (isSupport != support) continue;
                if (!WantsToCast(skill, out Transform target)) continue;

                focus = target;
                FaceAndAim(target);
                executor.CastSlot(slot);
                nextCast = Time.time + BalanceConfig.Instance.botSkillCastGap;
                return true;
            }
            return false;
        }

        /// そのスキルを今使うべきか。target は向ける相手（null なら自分に使う）。
        private bool WantsToCast(SkillData skill, out Transform target)
        {
            var config = BalanceConfig.Instance;
            Transform enemy = bot.EngagementTarget;
            float enemyDistance = enemy != null ? Vector3.Distance(transform.position, enemy.position) : float.PositiveInfinity;
            target = null;

            switch (skill.skillName)
            {
                case "Slash":
                case "Whirlwind":
                case "Bash":
                case "Smite":
                case "Stomp":
                case "Fireball":
                case "Blizzard":
                    target = enemy;
                    return enemyDistance <= skill.range;
                case "Charge":
                    target = enemy;
                    return enemyDistance <= skill.range && enemyDistance > bot.AttackRange;
                case "Taunt":
                    return CountEnemiesNear(transform.position, skill.range) >= 2;
                case "Guard":
                case "IronWall":
                case "ManaShield":
                    return HpRatio(health) < config.botDefendHpRatio && CountEnemiesNear(transform.position, config.botDefendEnemyRange) > 0;
                case "Recover":
                    return HpRatio(health) < config.botHealHpRatio;
                case "Sprint":
                    return enemyDistance < float.PositiveInfinity && enemyDistance > config.botSprintTargetDistance;
                case "Teleport":
                    return HpRatio(health) < config.botEscapeHpRatio && CountEnemiesNear(transform.position, config.botDefendEnemyRange) > 0;
                case "Heal":
                    return WantsSingleHeal(skill.range, out target);
                case "GroupHeal":
                    return CountHurtAllies(skill.range) >= 2 || HpRatio(health) < EmergencyHpRatio;
                case "Blessing":
                    return WantsBlessing(skill.range, enemyDistance, out target);
                // Detect は印が本人の画面にしか出ないのでBOTには意味がない。WarpPoint は人間の操作を前提にしている。
                default:
                    return false;
            }
        }

        private bool WantsSingleHeal(float range, out Transform target)
        {
            target = null;
            HealthComponent lowest = null;
            float lowestRatio = BalanceConfig.Instance.botHealHpRatio;
            foreach (var ally in AlliesWithin(range))
            {
                float ratio = HpRatio(ally);
                if (ratio >= lowestRatio) continue;
                lowestRatio = ratio;
                lowest = ally;
            }
            if (lowest == null) return false;
            if (lowest != health) target = lowest.transform;
            return true;
        }

        // 敵と戦っている味方を優先し、いなければ戦闘中の自分にかける。
        private bool WantsBlessing(float range, float enemyDistance, out Transform target)
        {
            target = null;
            float fightRange = BalanceConfig.Instance.botDefendEnemyRange * BlessedFightRangeScale;
            foreach (var ally in AlliesWithin(range))
            {
                if (ally == health || CountEnemiesNear(ally.transform.position, fightRange) == 0) continue;
                target = ally.transform;
                return true;
            }
            return enemyDistance <= fightRange;
        }

        private int CountHurtAllies(float range)
        {
            int count = 0;
            foreach (var ally in AlliesWithin(range))
                if (HpRatio(ally) < BalanceConfig.Instance.botHealHpRatio) count++;
            return count;
        }

        private List<HealthComponent> AlliesWithin(float range)
        {
            AllyBuffer.Clear();
            foreach (var go in GameObject.FindGameObjectsWithTag("Player"))
            {
                if (Vector3.Distance(transform.position, go.transform.position) > range) continue;
                var hp = go.GetComponent<HealthComponent>();
                if (hp != null && !hp.IsDead) AllyBuffer.Add(hp);
            }
            return AllyBuffer;
        }

        private static int CountEnemiesNear(Vector3 center, float range)
        {
            int count = 0;
            foreach (var monster in GameObject.FindGameObjectsWithTag("Monster"))
                if (Vector3.Distance(center, monster.transform.position) <= range) count++;
            return count;
        }

        private static float HpRatio(HealthComponent hp)
        {
            return hp.MaxHP > 0 ? (float)hp.CurrentHP / hp.MaxHP : 1f;
        }

        private void FaceAndAim(Transform target)
        {
            if (target != null)
            {
                Vector3 flat = target.position - transform.position;
                flat.y = 0f;
                if (flat.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(flat);
            }
            PointAt(target);
        }

        // 相手がいないスキルは真上を向く（照準の先に味方がいて、その人にかかってしまうのを防ぐ）。
        private void PointAt(Transform target)
        {
            if (aim == null) return;
            if (target == null)
            {
                aim.rotation = Quaternion.LookRotation(Vector3.up, transform.forward);
                return;
            }
            Vector3 to = target.position + Vector3.up * BodyCenterHeight - aim.position;
            if (to.sqrMagnitude > 0.01f) aim.rotation = Quaternion.LookRotation(to);
        }
    }
}
