using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Game.Core;

namespace Game.Skill
{
    /// Blizzard が地面に残す氷。上を歩くと、敵も味方も自分も滑る。
    public class IceField : MonoBehaviour
    {
        private const float RevealSeconds = 0.25f;
        private const float FadeSeconds = 1.2f;
        private const float FlashSeconds = 0.5f;
        private const float FadedReveal = 0.65f;
        private const float MaxHeightGap = 2.5f;
        private const float MaxDrape = 1.5f;
        private const float Lift = 0.04f;
        private const float AgentCheckInterval = 0.1f;
        private const int Rings = 10;
        private const int Sectors = 36;

        private static readonly List<IceField> Active = new List<IceField>();
        private static readonly Dictionary<NavMeshAgent, float> SlippingAgents = new Dictionary<NavMeshAgent, float>();
        private static readonly List<NavMeshAgent> AgentScratch = new List<NavMeshAgent>();
        private static readonly RaycastHit[] HitBuffer = new RaycastHit[16];

        private Vector3 center;
        private float radius;
        private float seconds;
        private float timer;
        private float reveal;
        private float agentTimer;
        private MeshRenderer surface;
        private MaterialPropertyBlock block;
        private Mesh mesh;

        public static void Spawn(Vector3 center, float radius, float seconds)
        {
            var go = new GameObject("IceField");
            go.transform.position = center;
            var field = go.AddComponent<IceField>();
            field.center = center;
            field.radius = radius;
            field.seconds = seconds;
            field.block = new MaterialPropertyBlock();
            field.BuildSurface();
            field.BuildAir();
            field.Apply();
            Debug.Log($"[Skill] IceField spawned (radius={radius}, seconds={seconds})");
        }

        public static bool IsOnIce(Vector3 position)
        {
            foreach (var field in Active)
            {
                if (field.Contains(position)) return true;
            }
            return false;
        }

        /// 氷の上では速度がすぐ入力に追いつかず、今の勢いのまま滑る。氷の外では入力どおり。
        public static Vector3 ApplySlip(Vector3 position, Vector3 desired, ref Vector3 velocity)
        {
            if (Active.Count == 0 || !IsOnIce(position))
            {
                velocity = desired;
                return desired;
            }
            velocity = Vector3.MoveTowards(velocity, desired, BalanceConfig.Instance.iceAcceleration * Time.deltaTime);
            return velocity;
        }

        /// 真下の地面の位置。キャラクターと判定用のトリガーは地面として扱わない。
        public static Vector3 SnapToGround(Vector3 position, float above = 4f, float below = 30f)
        {
            int count = Physics.RaycastNonAlloc(position + Vector3.up * above, Vector3.down, HitBuffer, above + below, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            Vector3 ground = position;
            for (int i = 0; i < count; i++)
            {
                if (HitBuffer[i].collider is CharacterController || HitBuffer[i].distance >= best) continue;
                best = HitBuffer[i].distance;
                ground = HitBuffer[i].point;
            }
            return ground;
        }

        private bool Contains(Vector3 position)
        {
            if (Mathf.Abs(position.y - center.y) > MaxHeightGap) return false;
            float dx = position.x - center.x;
            float dz = position.z - center.z;
            float r = radius * reveal;
            return dx * dx + dz * dz <= r * r;
        }

        private void OnEnable()
        {
            Active.Add(this);
        }

        private void OnDisable()
        {
            Active.Remove(this);
            if (Active.Count == 0) ReleaseAgents();
        }

        private void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
        }

        private void Update()
        {
            timer += Time.deltaTime;
            if (timer >= seconds)
            {
                Destroy(gameObject);
                return;
            }
            Apply();

            agentTimer -= Time.deltaTime;
            if (Active[0] == this && agentTimer <= 0f)
            {
                agentTimer = AgentCheckInterval;
                UpdateAgents();
            }
        }

        private void Apply()
        {
            float fade = Mathf.Clamp01((seconds - timer) / Mathf.Min(FadeSeconds, seconds));
            reveal = Mathf.Min(1f - Mathf.Pow(1f - Mathf.Clamp01(timer / RevealSeconds), 2f), Mathf.Lerp(FadedReveal, 1f, fade));
            block.SetFloat("_Reveal", reveal);
            block.SetFloat("_Alpha", fade);
            block.SetFloat("_Flash", 1f - Mathf.Clamp01(timer / FlashSeconds));
            surface.SetPropertyBlock(block);
        }

        // 魔物とBOTは NavMeshAgent で動くので、加速度を下げると勢いのまま滑る。
        private static void UpdateAgents()
        {
            float ice = BalanceConfig.Instance.iceAcceleration;
            foreach (var agent in FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None))
            {
                bool on = IsOnIce(agent.transform.position);
                bool slipping = SlippingAgents.TryGetValue(agent, out float baseAcceleration);
                if (on && !slipping)
                {
                    SlippingAgents[agent] = agent.acceleration;
                    agent.acceleration = ice;
                }
                else if (!on && slipping)
                {
                    agent.acceleration = baseAcceleration;
                    SlippingAgents.Remove(agent);
                }
            }
        }

        private static void ReleaseAgents()
        {
            AgentScratch.Clear();
            AgentScratch.AddRange(SlippingAgents.Keys);
            foreach (var agent in AgentScratch)
            {
                if (agent != null) agent.acceleration = SlippingAgents[agent];
            }
            SlippingAgents.Clear();
        }

        // 起伏に沿うよう、同心円状の頂点を地面へ落として面を作る。
        private void BuildSurface()
        {
            var vertices = new Vector3[Rings * Sectors + 1];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[(Sectors + (Rings - 1) * Sectors * 2) * 3];
            vertices[0] = DrapedPoint(Vector2.zero);
            uvs[0] = new Vector2(0.5f, 0.5f);
            int v = 1;
            for (int ring = 1; ring <= Rings; ring++)
            {
                float ringRadius = radius * 1.08f * ring / Rings;
                for (int s = 0; s < Sectors; s++)
                {
                    float angle = s * Mathf.PI * 2f / Sectors;
                    var flat = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ringRadius;
                    vertices[v] = DrapedPoint(flat);
                    uvs[v] = flat / (radius * 2f) + new Vector2(0.5f, 0.5f);
                    v++;
                }
            }
            int t = 0;
            for (int s = 0; s < Sectors; s++)
            {
                triangles[t++] = 0;
                triangles[t++] = 1 + (s + 1) % Sectors;
                triangles[t++] = 1 + s;
            }
            for (int ring = 1; ring < Rings; ring++)
            {
                int inner = 1 + (ring - 1) * Sectors;
                int outer = 1 + ring * Sectors;
                for (int s = 0; s < Sectors; s++)
                {
                    int next = (s + 1) % Sectors;
                    triangles[t++] = inner + s;
                    triangles[t++] = inner + next;
                    triangles[t++] = outer + next;
                    triangles[t++] = inner + s;
                    triangles[t++] = outer + next;
                    triangles[t++] = outer + s;
                }
            }
            mesh = new Mesh { name = "IceField" };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            surface = FireballVfx.NewMeshObject("Surface", transform, mesh, FireballVfx.GetMaterial("VFX/IceField"));
        }

        private Vector3 DrapedPoint(Vector2 flat)
        {
            Vector3 world = center + new Vector3(flat.x, 0f, flat.y);
            Vector3 ground = SnapToGround(world, MaxDrape, MaxDrape);
            float y = Mathf.Abs(ground.y - center.y) <= MaxDrape ? ground.y - center.y : 0f;
            return new Vector3(flat.x, y + Lift, flat.y);
        }

        // 地を這う冷気ときらめき。
        private void BuildAir()
        {
            var mist = FireballVfx.NewParticles("IceMist", transform);
            var mistMain = mist.main;
            mistMain.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.8f);
            mistMain.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
            mistMain.startSize = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
            mistMain.startColor = new Color(0.75f, 0.92f, 1f, 0.16f);
            mistMain.maxParticles = 40;
            ConfigureAir(mist, radius * 6f / 5f);

            var glints = FireballVfx.NewParticles("IceGlints", transform);
            var glintsMain = glints.main;
            glintsMain.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            glintsMain.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.5f);
            glintsMain.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
            glintsMain.startColor = new Color(0.9f, 1f, 1f, 1f);
            glintsMain.maxParticles = 60;
            ConfigureAir(glints, radius * 14f / 5f);
        }

        private void ConfigureAir(ParticleSystem ps, float rate)
        {
            var main = ps.main;
            main.duration = Mathf.Max(0.1f, seconds - FadeSeconds);
            main.loop = false;
            var emission = ps.emission;
            emission.rateOverTime = rate;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius * 0.9f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            ps.transform.localPosition = Vector3.up * 0.3f;
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;
            ps.gameObject.SetActive(true);
            ps.Play();
        }
    }
}
