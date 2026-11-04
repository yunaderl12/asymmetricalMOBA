using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Game.Combat;
using Game.Hero;

namespace Game.Skill
{
    public class SkillExecutor : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private LineRenderer beamLine;
        [SerializeField] private float beamDuration = 0.15f;

        private SkillRuntime runtime;
        private ManaComponent mana;
        private HealthComponent health;
        private HeroFPSController controller;
        private HeroClassInitializer classInit;
        private StatModifierContainer stats;
        private HeroInputActions inputs;
        private Coroutine activeBeamRoutine;
        private Game.Hero.HeroRegen regen;
        private FireballCast fireball;
        private HeroTeleport teleport;
        private BlizzardCast blizzard;
        private MeleeSkillCast melee;

        private void Awake()
        {
            runtime = GetComponent<SkillRuntime>();
            mana = GetComponent<ManaComponent>();
            health = GetComponent<HealthComponent>();
            controller = GetComponent<HeroFPSController>();
            classInit = GetComponent<HeroClassInitializer>();
            stats = GetComponent<StatModifierContainer>();
            regen = GetComponent<Game.Hero.HeroRegen>();
            if (inputs == null) inputs = new HeroInputActions();
        }

        private void OnEnable()
        {
            if (inputs == null) inputs = new HeroInputActions();
            inputs.Hero.Enable();
            inputs.Hero.SkillCast.performed += OnSkillCastPerformed;
        }

        private void OnDisable()
        {
            if (inputs == null) return;
            inputs.Hero.SkillCast.performed -= OnSkillCastPerformed;
            inputs.Hero.Disable();
        }

        private void OnSkillCastPerformed(InputAction.CallbackContext _)
        {
            if (Game.UI.ConsoleUI.IsConsoleActive) return;
            var modelAnim = GetComponent<Game.Hero.CharacterModelAnimator>();
            if (modelAnim != null && modelAnim.IsRespawnAnimPlaying) return;
            TryCast();
        }

        public void TryCast()
        {
            if (runtime == null) return;
            if (fireball != null && fireball.IsCasting) return;
            if (teleport != null && teleport.IsBusy) return;
            if (blizzard != null && blizzard.IsPosing) return;
            if (melee != null && melee.IsBusy) return;
            if (!StatusEffectController.CanUseSkills(gameObject))
            {
                Debug.Log("[Skill] cannot cast while stunned / silenced");
                return;
            }
            int idx = runtime.SelectedIndex;
            var skill = runtime.CurrentSkill;
            if (skill == null)
            {
                Debug.Log("[Skill] No skill in slot " + idx);
                return;
            }
            if (skill.skillName == WarpSkillName) { TryWarp(idx, skill); return; }
            if (skill.skillName == FireballSkillName) { TryFireball(idx, skill); return; }
            if (skill.skillName == TeleportSkillName) { TryTeleport(idx, skill); return; }
            if (skill.skillName == BlizzardSkillName) { TryBlizzard(idx, skill); return; }
            if (skill.skillName == "Slash") { TryMelee<SlashCast>(idx, skill); return; }
            if (skill.skillName == "Charge") { TryMelee<ChargeCast>(idx, skill); return; }
            if (skill.skillName == "Whirlwind") { TryMelee<WhirlwindCast>(idx, skill); return; }
            if (skill.skillName == "Guard") { TryMelee<GuardCast>(idx, skill); return; }
            if (skill.skillName == "Bash") { TryMelee<BashCast>(idx, skill); return; }
            if (skill.skillName == "Stomp") { TryMelee<StompCast>(idx, skill); return; }
            if (skill.skillName == "Taunt") { TryMelee<TauntCast>(idx, skill); return; }
            if (skill.skillName == "IronWall") { TryMelee<IronWallCast>(idx, skill); return; }
            if (skill.skillName == "Heal") { TryMelee<HealCast>(idx, skill); return; }
            if (skill.skillName == "GroupHeal") { TryMelee<GroupHealCast>(idx, skill); return; }
            if (skill.skillName == "Blessing") { TryMelee<BlessingCast>(idx, skill); return; }
            if (skill.skillName == "Smite") { TryMelee<SmiteCast>(idx, skill); return; }
            if (!runtime.CanCast(idx))
            {
                float cd = runtime.GetCooldownRemaining(idx);
                Debug.Log($"[Skill] {skill.skillName} on cooldown or low MP (cd={cd:F1}, mp={(mana != null ? mana.CurrentMP : -1)}/{skill.spCost})");
                return;
            }
            if (mana != null) mana.TryUseMana(skill.spCost);
            runtime.StartCooldown(idx);
            runtime.NotifyCast(idx);
            if (regen != null) regen.NotifyActed();

            Debug.Log($"[Skill] {skill.skillName} cast (effect={skill.effectType},dmg={skill.damageType},target={skill.targetType})");

            float val = runtime.GetCurrentEffectValue(idx);
            Execute(skill, val);
        }

        public const string WarpSkillName = "WarpPoint";

        /// Warp point skill: setting the point is free; moving costs SP and starts the cooldown only when the cast completes.
        private void TryWarp(int idx, SkillData skill)
        {
            var warp = GetComponent<HeroWarpPoint>();
            if (warp == null) warp = gameObject.AddComponent<HeroWarpPoint>();
            if (warp.IsCasting)
            {
                Debug.Log("[Skill] WarpPoint already casting");
                return;
            }
            if (!warp.HasPoint)
            {
                warp.TrySetPoint();
                return;
            }
            if (!runtime.CanCast(idx))
            {
                float cd = runtime.GetCooldownRemaining(idx);
                Debug.Log($"[Skill] WarpPoint on cooldown or low SP (cd={cd:F1}, sp={(mana != null ? mana.CurrentMP : -1)}/{skill.spCost})");
                return;
            }
            warp.BeginCast(() =>
            {
                if (mana != null) mana.TryUseMana(skill.spCost);
                runtime.StartCooldown(idx);
                runtime.NotifyCast(idx);
                if (regen != null) regen.NotifyActed();
            });
        }

        public const string FireballSkillName = "Fireball";

        /// 溜めてから放つスキル。SPとクールタイムは放った瞬間に消費するので、中断されたら消費しない。
        private void TryFireball(int idx, SkillData skill)
        {
            if (!runtime.CanCast(idx))
            {
                float cd = runtime.GetCooldownRemaining(idx);
                Debug.Log($"[Skill] Fireball on cooldown or low SP (cd={cd:F1}, sp={(mana != null ? mana.CurrentMP : -1)}/{skill.spCost})");
                return;
            }
            if (fireball == null) fireball = GetComponent<FireballCast>();
            if (fireball == null) fireball = gameObject.AddComponent<FireballCast>();

            int dmg = 0;
            fireball.BeginCast(skill, cameraTransform, skill.GetSizeAtLevel(runtime.GetSkillLevel(idx)),
                () =>
                {
                    if (!runtime.CanCast(idx)) return false;
                    if (mana != null) mana.TryUseMana(skill.spCost);
                    runtime.StartCooldown(idx);
                    runtime.NotifyCast(idx);
                    if (regen != null) regen.NotifyActed();
                    dmg = Mathf.RoundToInt(runtime.GetCurrentEffectValue(idx));
                    Debug.Log($"[Skill] {skill.skillName} released (dmg={dmg})");
                    return true;
                },
                hp =>
                {
                    hp.TakeDamage(dmg, gameObject, ToDamageType(skill.damageType));
                    ApplySkillStatus(skill, hp);
                });
        }

        public const string BlizzardSkillName = "Blizzard";

        /// 狙った地面へ氷を走らせる。SPとクールタイムは放つ瞬間に消費し、ダメージは氷が届いた瞬間に入る。
        private void TryBlizzard(int idx, SkillData skill)
        {
            if (fireball != null && fireball.IsPosing) return;
            if (!runtime.CanCast(idx))
            {
                float cd = runtime.GetCooldownRemaining(idx);
                Debug.Log($"[Skill] Blizzard on cooldown or low SP (cd={cd:F1}, sp={(mana != null ? mana.CurrentMP : -1)}/{skill.spCost})");
                return;
            }
            if (blizzard == null) blizzard = GetComponent<BlizzardCast>();
            if (blizzard == null) blizzard = gameObject.AddComponent<BlizzardCast>();

            int dmg = 0;
            float size = skill.GetSizeAtLevel(runtime.GetSkillLevel(idx));
            blizzard.BeginCast(skill, cameraTransform, size,
                () =>
                {
                    if (!runtime.CanCast(idx)) return false;
                    if (mana != null) mana.TryUseMana(skill.spCost);
                    runtime.StartCooldown(idx);
                    runtime.NotifyCast(idx);
                    if (regen != null) regen.NotifyActed();
                    dmg = Mathf.RoundToInt(runtime.GetCurrentEffectValue(idx));
                    Debug.Log($"[Skill] Blizzard released (dmg={dmg})");
                    return true;
                },
                () => skill.GetDurationAtLevel(runtime.GetSkillLevel(idx)),
                point =>
                {
                    if (this == null) return;
                    int hitCount = HitArea(skill, point, skill.areaRadius * size, dmg);
                    Debug.Log($"[Skill] Blizzard hit {hitCount} target(s)");
                    if (hitCount > 0 && GetComponent<HumanHeroMarker>() != null) HitStop.Play(skill.hitStopSeconds);
                });
        }

        // 着弾点の範囲にいる敵へ。味方と自分は巻き込まない。
        private int HitArea(SkillData skill, Vector3 point, float radius, int dmg)
        {
            var damaged = new System.Collections.Generic.HashSet<HealthComponent>();
            foreach (var col in Physics.OverlapSphere(point, radius, ~0, QueryTriggerInteraction.Collide))
            {
                var hp = col.GetComponentInParent<HealthComponent>();
                if (hp == null || hp.IsDead || hp.CompareTag("Player") || hp.gameObject == gameObject || !damaged.Add(hp)) continue;
                hp.TakeDamage(dmg, gameObject, ToDamageType(skill.damageType));
                ApplySkillStatus(skill, hp);
                ImpactPunch.Play(hp.gameObject, hp.transform.position - point);
            }
            return damaged.Count;
        }

        public const string TeleportSkillName = "Teleport";

        /// はじまりのまちへの帰還。SPとクールタイムは飛び立つ瞬間に消費する。
        private void TryTeleport(int idx, SkillData skill)
        {
            if (fireball != null && fireball.IsPosing) return;
            if (!runtime.CanCast(idx))
            {
                float cd = runtime.GetCooldownRemaining(idx);
                Debug.Log($"[Skill] Teleport on cooldown or low SP (cd={cd:F1}, sp={(mana != null ? mana.CurrentMP : -1)}/{skill.spCost})");
                return;
            }
            if (teleport == null) teleport = GetComponent<HeroTeleport>();
            if (teleport == null) teleport = gameObject.AddComponent<HeroTeleport>();
            teleport.EffectScale = skill.GetSizeAtLevel(runtime.GetSkillLevel(idx));
            teleport.BeginCast(skill, cameraTransform, () =>
            {
                if (!runtime.CanCast(idx)) return false;
                if (mana != null) mana.TryUseMana(skill.spCost);
                runtime.StartCooldown(idx);
                runtime.NotifyCast(idx);
                if (regen != null) regen.NotifyActed();
                return true;
            });
        }

        /// 溜めてから出す近接スキル。SPとクールタイムは発動の瞬間に消費し、ダメージは当たった敵に入る。
        private void TryMelee<T>(int idx, SkillData skill) where T : MeleeSkillCast
        {
            if (!runtime.CanCast(idx))
            {
                float cd = runtime.GetCooldownRemaining(idx);
                Debug.Log($"[Skill] {skill.skillName} on cooldown or low SP (cd={cd:F1}, sp={(mana != null ? mana.CurrentMP : -1)}/{skill.spCost})");
                return;
            }
            T cast = GetComponent<T>();
            if (cast == null) cast = gameObject.AddComponent<T>();
            melee = cast;

            float value = 0f;
            float size = skill.GetSizeAtLevel(runtime.GetSkillLevel(idx));
            cast.BeginCast(skill, cameraTransform, size,
                () =>
                {
                    if (!runtime.CanCast(idx)) return false;
                    if (mana != null) mana.TryUseMana(skill.spCost);
                    runtime.StartCooldown(idx);
                    runtime.NotifyCast(idx);
                    if (regen != null) regen.NotifyActed();
                    value = runtime.GetCurrentEffectValue(idx);
                    Debug.Log($"[Skill] {skill.skillName} released (value={value})");
                    return true;
                },
                (hp, damageScale) =>
                {
                    if (skill.effectType == SkillEffectType.Heal)
                    {
                        int amount = Mathf.RoundToInt(value * damageScale);
                        hp.Heal(amount);
                        if (hp.gameObject != gameObject) SupportTracker.RecordSupport(gameObject, hp.gameObject, amount);
                        return;
                    }
                    hp.TakeDamage(Mathf.RoundToInt(value * damageScale), gameObject, ToDamageType(skill.damageType));
                    ApplySkillStatus(skill, hp);
                },
                () =>
                {
                    if (skill.skillName == "Taunt") ExecuteTaunt(skill, size);
                    else if (skill.effectType == SkillEffectType.Buff) ExecuteBuff(skill, value);
                });
        }

        private void Execute(SkillData skill, float effectValue)
        {
            // Special-case placeholders first
            if (skill.skillName == "Detect") { ExecuteDetect(skill); return; }

            switch (skill.effectType)
            {
                case SkillEffectType.Damage: ExecuteDamage(skill, effectValue); break;
                case SkillEffectType.Heal: ExecuteHeal(skill, effectValue); break;
                case SkillEffectType.Buff: ExecuteBuff(skill, effectValue); break;
                case SkillEffectType.Debuff: ExecuteDebuff(skill, effectValue); break;
            }
        }

        private void ExecuteDamage(SkillData skill, float effectValue)
        {
            int dmg = Mathf.RoundToInt(effectValue);
            if (skill.targetType == SkillTargetType.Single)
            {
                if (cameraTransform == null) return;
                var origin = cameraTransform.position;
                var dir = cameraTransform.forward;
                Vector3 endPoint = origin + dir * skill.range;
                if (Physics.Raycast(origin, dir, out var hit, skill.range))
                {
                    endPoint = hit.point;
                    var hp = hit.collider.GetComponentInParent<HealthComponent>();
                    if (hp != null && hp.gameObject != gameObject)
                    {
                        hp.TakeDamage(dmg, gameObject, ToDamageType(skill.damageType));
                        ApplySkillStatus(skill, hp);
                    }
                }
                ShowBeam(origin, endPoint);
            }
            else if (skill.targetType == SkillTargetType.Area)
            {
                var hits = Physics.OverlapSphere(transform.position, skill.range);
                int affected = 0;
                foreach (var col in hits)
                {
                    var hp = col.GetComponentInParent<HealthComponent>();
                    if (hp == null || hp.gameObject == gameObject) continue;
                    hp.TakeDamage(dmg, gameObject, ToDamageType(skill.damageType));
                    ApplySkillStatus(skill, hp);
                    affected++;
                }
                Debug.Log($"[Skill] {skill.skillName} hit {affected} target(s) in area");
            }
        }

        private void ApplySkillStatus(SkillData skill, HealthComponent target)
        {
            if (skill.statusEffect == StatusEffectType.None || target == null || target.IsDead) return;
            if (Random.value > skill.statusChance) return;
            StatusEffectController.Apply(target.gameObject, skill.statusEffect, skill.statusDuration, skill.statusPower, gameObject);
        }

        private static DamageType ToDamageType(SkillDamageType t)
        {
            switch (t)
            {
                case SkillDamageType.Physical: return DamageType.Physical;
                case SkillDamageType.Magical: return DamageType.Magical;
                default: return DamageType.True;
            }
        }

        // 対象を選ぶ回復は HealCast / GroupHealCast が行う。
        private void ExecuteHeal(SkillData skill, float effectValue)
        {
            if (skill.targetType == SkillTargetType.Self && health != null) health.Heal(Mathf.RoundToInt(effectValue));
        }

        private void ExecuteBuff(SkillData skill, float effectValue)
        {
            if (stats == null) return;
            float val = effectValue;
            float atkMul = 1f, spdMul = 1f, dfMul = 1f;

            switch (skill.skillName)
            {
                case "Guard": dfMul = val; break;
                case "IronWall": dfMul = val; break;
                case "ManaShield": dfMul = val; break;
                case "Blessing": atkMul = val; break;
                case "Sprint": spdMul = val; break;
                default: spdMul = val; break;
            }
            string srcId = "Skill_" + skill.skillName;
            stats.AddOrReplaceModifier(new StatModifier(srcId, atkMul, spdMul, dfMul));

            ApplyBuffToSpeed();
            if (skill.duration > 0f) StartCoroutine(RemoveAfter(srcId, skill.skillName, skill.duration));
        }

        private void ExecuteDebuff(SkillData skill, float effectValue)
        {
            if (stats == null) return;
            string srcId = "Skill_" + skill.skillName;
            stats.AddOrReplaceModifier(new StatModifier(srcId, 0.7f, 0.7f, 1f));
            ApplyBuffToSpeed();
            if (skill.duration > 0f) StartCoroutine(RemoveAfter(srcId, skill.skillName, skill.duration));
        }

        private void ApplyBuffToSpeed()
        {
            if (controller != null && classInit != null)
            {
                float baseSpd = classInit.CurrentSpd;
                controller.SetMoveSpeed(baseSpd * stats.TotalSpdMultiplier);
            }
        }

        private IEnumerator RemoveAfter(string srcId, string skillName, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (stats != null) stats.RemoveModifier(srcId);
            ApplyBuffToSpeed();
            Debug.Log($"[Skill] {skillName} expired");
        }

        private void ExecuteTaunt(SkillData skill, float sizeScale)
        {
            // Adds hate only; monsters without hate (MonsterData.useHate off) ignore it.
            float amount = Game.Core.BalanceConfig.Instance.hateTauntAmount;
            var hits = Physics.OverlapSphere(transform.position, skill.range * sizeScale);
            var done = new System.Collections.Generic.HashSet<Game.Monster.MonsterHate>();
            foreach (var col in hits)
            {
                var mh = col.GetComponentInParent<Game.Monster.MonsterHate>();
                if (mh == null || !done.Add(mh)) continue;
                mh.Add(gameObject, amount);
            }
            Debug.Log($"[Skill] Taunt: +{amount} hate on {done.Count} monster(s)");
        }

        private void ExecuteDetect(SkillData skill)
        {
            var hits = Physics.OverlapSphere(transform.position, skill.range);
            int enemies = 0;
            foreach (var col in hits)
            {
                if (col.GetComponentInParent<Game.Monster.Monster>() != null) enemies++;
            }
            Debug.Log($"[Skill] Detect: enemies in {skill.range}m = {enemies}");
        }

        private void ShowBeam(Vector3 a, Vector3 b)
        {
            if (beamLine == null) return;
            beamLine.SetPosition(0, a);
            beamLine.SetPosition(1, b);
            beamLine.enabled = true;
            if (activeBeamRoutine != null) StopCoroutine(activeBeamRoutine);
            activeBeamRoutine = StartCoroutine(HideBeamAfter(beamDuration));
        }

        private IEnumerator HideBeamAfter(float t)
        {
            yield return new WaitForSeconds(t);
            if (beamLine != null) beamLine.enabled = false;
            activeBeamRoutine = null;
        }
    }
}
