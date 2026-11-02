using UnityEngine;
using UnityEngine.AI;
using Game.Hero;
using Game.Skill;

namespace Game.Monster
{
    /// 魔物の出現演出（本体側）。見た目のモデルだけを地面の下から這い上がらせる。地面より下は地面に隠れて見えない。
    /// 這い出している間は動かず、攻撃せず、当たり判定もない（無敵）。
    public class MonsterEmerge : MonoBehaviour
    {
        /// 全体のうち、ゆっくり這い上がる時間の割合。ここで一気に飛び出す。
        public const float BurstRatio = 0.5f;

        private const float LeapEndRatio = 0.75f;
        private const float BaseDepth = 2.2f;
        private const float CrawlDepthRatio = 0.5f;
        private const float LeapHeight = 0.3f;
        private const float LeapStretch = 1.18f;
        private const float Tremble = 0.03f;

        private Monster monster;
        private NavMeshAgent agent;
        private CharacterModelAnimator modelAnim;
        private Collider[] colliders;
        private GameObject healthBar;
        private Transform visual;
        private Vector3 basePosition;
        private Vector3 baseScale;
        private float seconds;
        private float depth;
        private float timer;
        private bool roared;
        private bool burning;

        public static void Begin(Monster monster, float seconds, float modelScale)
        {
            var emerge = monster.gameObject.AddComponent<MonsterEmerge>();
            emerge.monster = monster;
            emerge.seconds = seconds;
            emerge.depth = BaseDepth * Mathf.Max(0.1f, modelScale);
            emerge.Lock();
        }

        private void Lock()
        {
            monster.IsEmerging = true;
            agent = GetComponent<NavMeshAgent>();
            if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
            colliders = GetComponentsInChildren<Collider>();
            foreach (var c in colliders) c.enabled = false;
            var bar = transform.Find("HealthBar");
            if (bar != null)
            {
                healthBar = bar.gameObject;
                healthBar.SetActive(false);
            }
        }

        // モデルは CharacterModelAnimator.Start で作られるので、見つかってから動かし始める。
        private bool FindVisual()
        {
            if (visual != null) return true;
            modelAnim = GetComponent<CharacterModelAnimator>();
            if (modelAnim != null && modelAnim.ModelRoot != null) visual = modelAnim.ModelRoot;
            else if (modelAnim == null) visual = transform.Find("Body");
            if (visual == null) return false;
            basePosition = visual.localPosition;
            baseScale = visual.localScale;
            return true;
        }

        private void LateUpdate()
        {
            if (!FindVisual())
            {
                if (timer >= seconds) Finish();
                timer += Time.deltaTime;
                return;
            }

            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / seconds);
            float y, stretch = 1f, shake = 0f;
            if (t < BurstRatio)
            {
                // もがきながら少しずつ這い上がる。
                float k = t / BurstRatio;
                y = Mathf.Lerp(-depth, -depth * CrawlDepthRatio, k);
                shake = Tremble * k;
            }
            else if (t < LeapEndRatio)
            {
                // 一気に飛び出す。
                float k = (t - BurstRatio) / (LeapEndRatio - BurstRatio);
                float eased = 1f - (1f - k) * (1f - k);
                y = Mathf.Lerp(-depth * CrawlDepthRatio, LeapHeight, eased);
                stretch = Mathf.Lerp(LeapStretch, 1.05f, k);
                if (!roared && modelAnim != null)
                {
                    roared = true;
                    modelAnim.PlayAttack();
                }
            }
            else
            {
                float k = (t - LeapEndRatio) / (1f - LeapEndRatio);
                y = LeapHeight * (1f - k * k);
            }

            Vector3 offset = new Vector3(Random.Range(-shake, shake), y, Random.Range(-shake, shake));
            visual.localPosition = basePosition + offset;
            visual.localScale = Vector3.Scale(baseScale, new Vector3(1f / stretch, stretch, 1f / stretch));

            // 炎の膜は今の形を写すので、モデルを地面の下へ動かしてから付ける。
            if (!burning)
            {
                burning = true;
                FireHitEffect.PlayBurn(gameObject, Mathf.Max(0.01f, seconds - timer));
            }

            if (t >= 1f) Finish();
        }

        private void Finish()
        {
            if (visual != null)
            {
                visual.localPosition = basePosition;
                visual.localScale = baseScale;
                // 着地でつぶれる。
                ImpactPunch.Play(gameObject, transform.forward, 0f);
            }
            foreach (var c in colliders) if (c != null) c.enabled = true;
            if (healthBar != null) healthBar.SetActive(true);
            monster.IsEmerging = false;
            if (agent != null && agent.isOnNavMesh) agent.isStopped = false;
            Destroy(this);
        }
    }
}
