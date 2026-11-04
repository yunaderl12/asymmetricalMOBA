using UnityEngine;
using UnityEngine.AI;
using Game.Combat;
using Game.Hero;

namespace Game.Skill
{
    /// スキルで敵を大きく押し飛ばす。動けないもの（コア・スポナー）には効かず、壁や NavMesh の端で止まる。
    public class SkillKnockback : MonoBehaviour
    {
        private const float Seconds = 0.4f;

        private NavMeshAgent agent;
        private CharacterController characterController;
        private HealthComponent health;
        private CharacterModelAnimator modelAnim;
        private Vector3 velocity;
        private float timer;

        public static void Play(GameObject target, Vector3 direction, float distance)
        {
            var monster = target.GetComponent<Game.Monster.Monster>();
            if (monster != null)
            {
                if (monster.IsEmerging) return;
                if (monster.Data != null) distance *= monster.Data.hitReactionScale;
            }
            if (distance <= 0f) return;
            if (target.GetComponent<NavMeshAgent>() == null && target.GetComponent<CharacterController>() == null) return;

            var knock = target.GetComponent<SkillKnockback>();
            if (knock == null) knock = target.AddComponent<SkillKnockback>();
            direction.y = 0f;
            // 速さは直線的に落ちていくので、初速は平均の2倍。
            knock.velocity = direction.normalized * (2f * distance / Seconds);
            knock.timer = Seconds;
        }

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            characterController = GetComponent<CharacterController>();
            health = GetComponent<HealthComponent>();
            modelAnim = GetComponent<CharacterModelAnimator>();
        }

        private void Update()
        {
            if (timer <= 0f) return;
            timer -= Time.deltaTime;
            if ((health != null && health.IsDead) || (modelAnim != null && modelAnim.IsBlasting))
            {
                timer = 0f;
                return;
            }
            Vector3 step = velocity * (Mathf.Clamp01(timer / Seconds) * Time.deltaTime);
            if (agent != null && agent.enabled && agent.isOnNavMesh) agent.Move(step);
            else if (characterController != null && characterController.enabled) characterController.Move(step);
        }
    }
}
