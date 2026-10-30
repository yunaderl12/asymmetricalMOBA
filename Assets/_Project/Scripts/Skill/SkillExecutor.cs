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
            var knight = GetComponent<Game.Hero.HeroKnightAnimator>();
            if (knight != null && knight.IsRespawnAnimPlaying) return;
            TryCast();
        }

        public void TryCast()
        {
            if (runtime == null) return;
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

        private void Execute(SkillData skill, float effectValue)
        {
            // Special-case placeholders first
            if (skill.skillName == "Teleport") { ExecuteTeleport(skill); return; }
            if (skill.skillName == "Taunt") { ExecuteTaunt(skill); return; }
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

        private void ExecuteHeal(SkillData skill, float effectValue)
        {
            int amount = Mathf.RoundToInt(effectValue);
            if (skill.targetType == SkillTargetType.Self)
            {
                if (health != null) health.Heal(amount);
            }
            else if (skill.targetType == SkillTargetType.Single)
            {
                if (cameraTransform == null) return;
                if (Physics.Raycast(cameraTransform.position, cameraTransform.forward, out var hit, skill.range))
                {
                    var hp = hit.collider.GetComponentInParent<HealthComponent>();
                    if (hp != null)
                    {
                        hp.Heal(amount);
                        SupportTracker.RecordSupport(gameObject, hp.gameObject);
                        Debug.Log($"[Skill] Healed {hp.name} for {amount}");
                    }
                    else Debug.Log("[Skill] Heal: no valid target");
                }
                else
                {
                    Debug.Log("[Skill] Heal: no target in range; self-heal fallback");
                    if (health != null) health.Heal(amount);
                }
            }
            else if (skill.targetType == SkillTargetType.Area)
            {
                var hits = Physics.OverlapSphere(transform.position, skill.range);
                int healed = 0;
                foreach (var col in hits)
                {
                    var hp = col.GetComponentInParent<HealthComponent>();
                    if (hp == null) continue;
                    hp.Heal(amount);
                    SupportTracker.RecordSupport(gameObject, hp.gameObject);
                    healed++;
                }
                Debug.Log($"[Skill] {skill.skillName} healed {healed} target(s) in area");
            }
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

        private void ExecuteTeleport(SkillData skill)
        {
            if (cameraTransform == null) return;
            Vector3 fwd = cameraTransform.forward;
            fwd.y = 0f;
            fwd.Normalize();
            var cc = GetComponent<CharacterController>();
            Vector3 target = transform.position + fwd * skill.range;
            if (cc != null)
            {
                cc.enabled = false;
                transform.position = target;
                cc.enabled = true;
            }
            else transform.position = target;
            Debug.Log("[Skill] Teleport: moved to " + target);
        }

        private void ExecuteTaunt(SkillData skill)
        {
            var hits = Physics.OverlapSphere(transform.position, skill.range);
            int tagged = 0;
            foreach (var col in hits)
            {
                if (!col.CompareTag("Enemy") && col.GetComponentInParent<Game.Monster.Monster>() == null) continue;
                var sm = col.GetComponentInParent<StatModifierContainer>();
                if (sm != null) { sm.AddOrReplaceModifier(new StatModifier("Taunted", 1f, 1f, 1f)); tagged++; }
            }
            Debug.Log($"[Skill] Taunt: marked {tagged} monsters (effect deferred to Phase 9)");
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
