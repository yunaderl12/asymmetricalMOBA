using System.Collections.Generic;
using UnityEngine;
using Game.Combat;

namespace Game.Skill
{
    /// 飛んでいく三日月形の斬撃。敵は貫通し、壁や地面に当たると消える。
    /// 弧の面に視線が含まれるので、撃った本人からは剣の軌道と同じ右上から左下への直線に見え、横からは三日月に見える。
    public class SlashWave : MonoBehaviour
    {
        private const float Radius = 1.7f;
        private const float Width = 0.75f;
        private const float ArcDegrees = 125f;
        private const float BladeHalfHeight = 0.16f; // 面の真正面（本人の視点）から線として見える厚み
        private const float SwingAngle = 55f;       // 本人から見た線の傾き（水平からの角度）
        private const float CenterOffset = Radius - Width * 0.5f; // 弧の中心から、弧の中央までの距離
        private const float GrowSeconds = 0.1f;
        private const float StartScale = 0.6f;
        private const float EndFadeRatio = 0.75f;
        private const int Segments = 28;
        private const int SearchMargin = 3;

        private static readonly List<HealthComponent> HitBuffer = new List<HealthComponent>();
        private static Mesh sharedMesh;

        private GameObject owner;
        private MeshRenderer ribbon;
        private MaterialPropertyBlock block;
        private Vector3 direction;
        private Vector3 line;             // 当たり判定の中心（照準線の上）
        private Vector3 toCenter;
        private int damage;
        private float speed;
        private float range;
        private float hitRadius;
        private float traveled;
        private float age;
        private readonly HashSet<HealthComponent> alreadyHit = new HashSet<HealthComponent>();

        /// start は弧の中央が来る位置。up は撃った本人のカメラの上方向。
        public static SlashWave Spawn(GameObject owner, Vector3 start, Vector3 direction, Vector3 up, int damage, float speed, float range, float hitRadius)
        {
            var go = new GameObject("SlashWave");
            var wave = go.AddComponent<SlashWave>();
            wave.owner = owner;
            wave.direction = direction.normalized;
            wave.line = start;
            wave.damage = damage;
            wave.speed = speed;
            wave.range = range;
            wave.hitRadius = hitRadius;
            Vector3 side = Vector3.Cross(up, wave.direction).normalized;
            Vector3 toSwingStart = Quaternion.AngleAxis(SwingAngle, wave.direction) * side; // 右上
            wave.toCenter = -wave.direction * CenterOffset;
            // メッシュの +X（弧の先端側）が左下、+Z（弧のふくらみ）が進行方向を向く。
            go.transform.rotation = Quaternion.LookRotation(wave.direction, Vector3.Cross(wave.direction, -toSwingStart));
            wave.ribbon = FireballVfx.NewMeshObject("Ribbon", go.transform, GetMesh(), FireballVfx.GetMaterial("VFX/SwordArc"));
            wave.block = new MaterialPropertyBlock();
            wave.Apply();
            return wave;
        }

        private void Update()
        {
            float step = Mathf.Min(speed * Time.deltaTime, range - traveled);
            Vector3 next = line + direction * step;
            bool stopped = Physics.Linecast(line, next, out var hit, ~0, QueryTriggerInteraction.Ignore) && hit.collider.GetComponentInParent<HealthComponent>() == null;
            if (stopped) next = hit.point;
            line = next;
            traveled += step;
            age += Time.deltaTime;
            HitEnemies();

            if (stopped)
            {
                MeleeFx.Impact(hit.point, hit.normal, 1.6f, MeleeFx.Steel);
                Destroy(gameObject);
                return;
            }
            if (traveled >= range)
            {
                Destroy(gameObject);
                return;
            }
            Apply();
        }

        private void HitEnemies()
        {
            HitBuffer.Clear();
            foreach (var col in Physics.OverlapSphere(line, hitRadius + SearchMargin, ~0, QueryTriggerInteraction.Collide))
            {
                var hp = col.GetComponentInParent<HealthComponent>();
                if (hp == null || hp.IsDead || hp.CompareTag("Player") || hp.gameObject == owner || alreadyHit.Contains(hp)) continue;
                Bounds body = FireballProjectile.BodyBoundsOf(hp.gameObject, col);
                if ((body.ClosestPoint(line) - line).sqrMagnitude > hitRadius * hitRadius) continue;
                alreadyHit.Add(hp);
                HitBuffer.Add(hp);
            }
            foreach (var hp in HitBuffer)
            {
                Vector3 point = FireballProjectile.BodyBoundsOf(hp.gameObject, null).ClosestPoint(line);
                hp.TakeDamage(damage, owner, DamageType.Physical);
                MeleeFx.Impact(point, -direction, 1.8f, MeleeFx.Steel);
                if (hp == null) continue;
                FireHitEffect.PlayFlash(hp.gameObject);
                ImpactPunch.Play(hp.gameObject, direction);
            }
        }

        private void Apply()
        {
            float grow = Mathf.Clamp01(age / GrowSeconds);
            float scale = Mathf.Lerp(StartScale, 1f, grow);
            transform.localScale = Vector3.one * scale;
            transform.position = line + toCenter * scale;

            // 飛距離の終わりで尾から消えていく。
            float fade = Mathf.InverseLerp(EndFadeRatio, 1f, traveled / range);
            block.SetFloat("_Head", 1f);
            block.SetFloat("_Tail", fade * fade);
            block.SetColor("_Color", MeleeFx.SteelCore);
            block.SetColor("_EdgeColor", MeleeFx.SteelBlue);
            ribbon.SetPropertyBlock(block);
        }

        private static Mesh GetMesh()
        {
            if (sharedMesh == null) sharedMesh = BuildMesh();
            return sharedMesh;
        }

        // XZ 面に、+Z がふくらむ弧を置く。外縁に沿って Y 方向の帯を足し、真正面からも線が見えるようにする。uv は SwordArc と同じ。
        private static Mesh BuildMesh()
        {
            const int RingSize = 4;
            var vertices = new Vector3[(Segments + 1) * RingSize];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[Segments * 18];
            float inner = Radius - Width;
            Vector3 blade = Vector3.up * BladeHalfHeight;
            for (int i = 0; i <= Segments; i++)
            {
                float u = (float)i / Segments;
                float radians = (u - 0.5f) * ArcDegrees * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians));
                int v = i * RingSize;
                vertices[v] = dir * inner;
                vertices[v + 1] = dir * Radius;
                vertices[v + 2] = dir * Radius + blade;
                vertices[v + 3] = dir * Radius - blade;
                uvs[v] = new Vector2(u, 0f);
                uvs[v + 1] = new Vector2(u, 1f);
                uvs[v + 2] = new Vector2(u, 0f);
                uvs[v + 3] = new Vector2(u, 0f);
                if (i == Segments) break;
                int t = i * 18;
                for (int edge = 0; edge < 3; edge++)
                {
                    int from = v + (edge == 0 ? 0 : edge + 1); // 内側、上、下から外縁へ
                    int to = v + 1;
                    triangles[t++] = from;
                    triangles[t++] = to;
                    triangles[t++] = from + RingSize;
                    triangles[t++] = to;
                    triangles[t++] = to + RingSize;
                    triangles[t++] = from + RingSize;
                }
            }
            var mesh = new Mesh { name = "SlashWave" };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
