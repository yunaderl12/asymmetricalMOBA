using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Combat;

namespace Game.Skill
{
    public struct FireballHit
    {
        public HealthComponent Health;
        public Vector3 Point;
        public Vector3 Normal;
        public float Distance;
    }

    /// 放たれた後の火球。判定は直線で取り、見た目だけ少し弧を描く。
    public class FireballProjectile : MonoBehaviour
    {
        private const float HitRadius = 0.3f;
        private const float ArcHeightRatio = 0.03f;
        private const float ArcHeightMax = 0.3f;
        private const float FizzleSeconds = 0.25f;

        private static readonly RaycastHit[] CastBuffer = new RaycastHit[32];
        private static readonly List<Renderer> RendererBuffer = new List<Renderer>();

        private FireballVisual visual;
        private GameObject owner;
        private Action<HealthComponent, Vector3> onHit;
        private Vector3 linePosition;
        private Vector3 direction;
        private float speed;
        private float maxDistance;
        private float aimDistance;
        private float startRadius;
        private float flightRadius;
        private float radius;
        private float traveled;
        private float age;
        private float fizzleTimer = -1f;

        public static void Launch(FireballVisual visual, Vector3 direction, float speed, float maxDistance, float aimDistance, float startRadius, float flightRadius, GameObject owner, Action<HealthComponent, Vector3> onHit)
        {
            var p = visual.gameObject.AddComponent<FireballProjectile>();
            p.visual = visual;
            p.linePosition = visual.transform.position;
            p.direction = direction;
            p.speed = speed;
            p.maxDistance = maxDistance;
            p.aimDistance = Mathf.Max(0.01f, aimDistance);
            p.startRadius = startRadius;
            p.flightRadius = flightRadius;
            p.radius = startRadius;
            p.owner = owner;
            p.onHit = onHit;
            visual.transform.rotation = Quaternion.LookRotation(direction);
            visual.BeginFlight();
            p.UpdateShape();
            // 至近の敵は発射時点で重なっていることがあるので、ここで一度判定する。
            if (Probe(p.linePosition, direction, HitRadius, 0.01f, owner, out var hit)) p.Impact(hit);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (fizzleTimer >= 0f)
            {
                UpdateFizzle(dt);
                return;
            }

            age += dt;
            UpdateShape();

            float step = speed * dt;
            if (step > 0f && Probe(linePosition, direction, HitRadius, step, owner, out var hit))
            {
                Impact(hit);
                return;
            }

            linePosition += direction * step;
            traveled += step;
            PlaceOnArc();

            if (traveled >= maxDistance)
            {
                fizzleTimer = 0f;
                visual.ReleaseTail();
            }
        }

        // 発射の瞬間につぶれてから、ギュンと伸びて少し戻る。
        private void UpdateShape()
        {
            radius = Mathf.Lerp(startRadius, flightRadius, Mathf.Clamp01(age / 0.12f));
            float stretch, width, taper;
            if (age < 0.035f) { stretch = 0.6f; width = 1.25f; taper = 0f; }
            else if (age < 0.11f)
            {
                float t = (age - 0.035f) / 0.075f;
                stretch = Mathf.Lerp(0.6f, 3.4f, t);
                width = Mathf.Lerp(1.25f, 0.68f, t);
                taper = Mathf.Lerp(0f, 0.9f, t);
            }
            else
            {
                float t = Mathf.Clamp01((age - 0.11f) / 0.12f);
                stretch = Mathf.Lerp(3.4f, 2.7f, t);
                width = Mathf.Lerp(0.68f, 0.76f, t);
                taper = 0.9f;
            }
            visual.SetShape(radius, stretch, width, taper);
        }

        private void PlaceOnArc()
        {
            float t = Mathf.Clamp01(traveled / aimDistance);
            float height = Mathf.Min(ArcHeightMax, aimDistance * ArcHeightRatio);
            Vector3 offset = Vector3.up * (height * 4f * t * (1f - t));
            Vector3 tangent = direction * aimDistance + Vector3.up * (height * 4f * (1f - 2f * t));
            transform.SetPositionAndRotation(linePosition + offset, Quaternion.LookRotation(tangent));
        }

        // 外れたとき: 失速しながら小さくなって消える。
        private void UpdateFizzle(float dt)
        {
            fizzleTimer += dt;
            float t = Mathf.Clamp01(fizzleTimer / FizzleSeconds);
            linePosition += direction * (speed * (1f - t) * (1f - t) * 0.5f * dt);
            transform.position = linePosition;
            float shrink = (1f - t) * (1f - t);
            visual.SetShape(radius * Mathf.Max(0.01f, shrink), Mathf.Lerp(2.7f, 1f, t), Mathf.Lerp(0.76f, 1f, t), Mathf.Lerp(0.9f, 0f, t));
            if (t >= 1f) Destroy(gameObject);
        }

        private void Impact(FireballHit hit)
        {
            enabled = false;
            visual.ReleaseTail();

            Vector3 sparkDir = hit.Health != null ? direction : Vector3.Reflect(direction, hit.Normal);
            FireballVfx.SpawnSparks(hit.Point, sparkDir, hit.Health != null ? 30 : 16);
            FireFlash.Spawn(hit.Point, hit.Health != null ? radius * 9f : radius * 5f, 0.16f, 1f);

            if (hit.Health != null)
            {
                // 倒した敵は即座に消えるので、演出はダメージより先に用意する。
                FireHitEffect.Play(hit.Health.gameObject);
                ImpactPunch.Play(hit.Health.gameObject, direction);
                onHit?.Invoke(hit.Health, hit.Point);
            }
            Destroy(gameObject);
        }

        /// 火球の当たり判定。魔物の当たり判定は半径1.5mの接触ダメージ用トリガーしかないので、
        /// トリガーに触れた場合は見た目の範囲に当たったときだけヒットとする。味方はすり抜ける。
        public static bool Probe(Vector3 origin, Vector3 dir, float castRadius, float distance, GameObject owner, out FireballHit best)
        {
            best = default;
            best.Distance = float.PositiveInfinity;
            int count = castRadius > 0f
                ? Physics.SphereCastNonAlloc(origin, castRadius, dir, CastBuffer, distance, ~0, QueryTriggerInteraction.Collide)
                : Physics.RaycastNonAlloc(origin, dir, CastBuffer, distance, ~0, QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                var h = CastBuffer[i];
                var col = h.collider;
                if (owner != null && col.transform.IsChildOf(owner.transform)) continue;
                if (col.CompareTag("Player")) continue;
                var hp = col.GetComponentInParent<HealthComponent>();
                if (hp != null && (hp.IsDead || hp.CompareTag("Player") || hp.gameObject == owner)) continue;

                float d = h.distance;
                Vector3 point = d > 0f ? h.point : origin;
                if (col.isTrigger)
                {
                    if (hp == null) continue;
                    if (TryIntersectVisual(hp.gameObject, origin, dir, castRadius, distance, out float visualDistance, out Vector3 surface))
                    {
                        d = visualDistance;
                        point = surface;
                    }
                    else if (HasVisual(hp.gameObject)) continue;
                }
                if (d < best.Distance)
                {
                    best.Distance = d;
                    best.Point = point;
                    best.Normal = d > 0f ? h.normal : -dir;
                    best.Health = hp;
                }
            }
            return !float.IsPositiveInfinity(best.Distance);
        }

        /// 敵の体の範囲。見た目が無ければ当たり判定の範囲を使う。
        public static Bounds BodyBoundsOf(GameObject target, Collider fallback)
        {
            FireHitEffect.CollectVisualRenderers(target, RendererBuffer);
            if (RendererBuffer.Count > 0) return GetBodyBounds(target, RendererBuffer);
            return fallback != null ? fallback.bounds : new Bounds(target.transform.position, Vector3.one);
        }

        // SkinnedMeshRenderer.bounds は実際の体よりかなり大きいので、
        // スキンメッシュはメッシュの高さと奥行きから足元基準の直立した箱を作る。
        private static Bounds GetBodyBounds(GameObject target, List<Renderer> renderers)
        {
            bool has = false;
            Bounds result = default;
            foreach (var r in renderers)
            {
                Bounds b = r.bounds;
                if (r is SkinnedMeshRenderer skin && skin.sharedMesh != null)
                {
                    Vector3 size = WorldSizeOf(skin.sharedMesh.bounds, skin.transform);
                    float width = Mathf.Min(size.x, size.z);
                    Vector3 feet = target.transform.position;
                    b = new Bounds(feet + Vector3.up * (size.y * 0.5f), new Vector3(width, size.y, width));
                }
                if (has) result.Encapsulate(b);
                else { result = b; has = true; }
            }
            return result;
        }

        private static Vector3 WorldSizeOf(Bounds local, Transform t)
        {
            Vector3 min = Vector3.positiveInfinity;
            Vector3 max = Vector3.negativeInfinity;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = local.center + Vector3.Scale(local.extents,
                    new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                Vector3 world = t.TransformVector(corner);
                min = Vector3.Min(min, world);
                max = Vector3.Max(max, world);
            }
            return max - min;
        }

        private static bool HasVisual(GameObject target)
        {
            FireHitEffect.CollectVisualRenderers(target, RendererBuffer);
            return RendererBuffer.Count > 0;
        }

        private static bool TryIntersectVisual(GameObject target, Vector3 origin, Vector3 dir, float castRadius, float distance, out float hitDistance, out Vector3 surface)
        {
            hitDistance = 0f;
            surface = origin;
            FireHitEffect.CollectVisualRenderers(target, RendererBuffer);
            if (RendererBuffer.Count == 0) return false;
            Bounds body = GetBodyBounds(target, RendererBuffer);
            Bounds bounds = body;
            bounds.Expand(castRadius * 2f);
            if (!bounds.IntersectRay(new Ray(origin, dir), out float d)) return false;
            d = Mathf.Max(0f, d);
            if (d > distance) return false;
            hitDistance = d;
            // 閃光と火花は火球の中心ではなく、敵の体の表面から出す。
            surface = body.ClosestPoint(origin + dir * d);
            return true;
        }
    }
}
