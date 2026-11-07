using System.Collections.Generic;
using UnityEngine;
using Game.Combat;
using Game.Hero;
using Game.Skill;

namespace Game.DemonKing
{
    /// 魔王のフィールド魔法を発動したあとの本体。Cast で種類ごとのコンポーネントを置き、溜め（予兆）→ 本番 → 消える までを受け持つ。
    /// 判定はこのオブジェクトがするので、ネットワーク化のときはサーバーだけで Cast する。
    public abstract class FieldSpellEffect : MonoBehaviour
    {
        protected FieldSpellData data;
        protected GameObject caster;
        protected Vector3 point;
        protected float time;

        public FieldSpellData Data => data;

        public static FieldSpellEffect Cast(FieldSpellData data, Vector3 point, GameObject caster)
        {
            if (data == null) return null;
            point = Ground(point);
            var go = new GameObject("FieldSpell_" + data.kind);
            go.transform.position = point;
            FieldSpellEffect effect;
            switch (data.kind)
            {
                case FieldSpellKind.Tornado: effect = go.AddComponent<FieldSpellTornado>(); break;
                case FieldSpellKind.Lightning: effect = go.AddComponent<FieldSpellLightning>(); break;
                case FieldSpellKind.Earthquake: effect = go.AddComponent<FieldSpellEarthquake>(); break;
                case FieldSpellKind.Meteor: effect = go.AddComponent<FieldSpellMeteor>(); break;
                case FieldSpellKind.ManaBurst: effect = go.AddComponent<FieldSpellManaBurst>(); break;
                case FieldSpellKind.Downpour: effect = go.AddComponent<FieldSpellDownpour>(); break;
                default:
                    Destroy(go);
                    return null;
            }
            effect.data = data;
            effect.caster = caster;
            effect.point = point;
            effect.Begin();
            Debug.Log($"[FieldSpell] {data.spellName} at {point}");
            return effect;
        }

        protected virtual void Begin() { }
        protected abstract void Tick(float dt);

        private void Update()
        {
            float dt = Time.deltaTime;
            time += dt;
            Tick(dt);
        }

        // ---- 対象 ----

        private static readonly List<HealthComponent> HeroBuffer = new List<HealthComponent>();

        /// 生きている勇者（人もBOTも）。
        public static List<HealthComponent> Heroes()
        {
            HeroBuffer.Clear();
            foreach (var go in GameObject.FindGameObjectsWithTag("Player"))
            {
                var hp = go.GetComponentInParent<HealthComponent>();
                if (hp != null && !hp.IsDead && !HeroBuffer.Contains(hp)) HeroBuffer.Add(hp);
            }
            return HeroBuffer;
        }

        protected static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        protected static Vector3 FlatDirection(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            d.y = 0f;
            return d.sqrMagnitude > 0.0001f ? d.normalized : Random.insideUnitSphere.WithY0().normalized;
        }

        protected void Hit(HealthComponent hp, int damage, DamageType type)
        {
            if (hp == null || hp.IsDead || damage <= 0) return;
            hp.TakeDamage(damage, caster, type);
        }

        /// 人の勇者なら操作しているキャラ（HeroFPSController）。BOT なら null。
        protected static HeroFPSController HumanOf(HealthComponent hp)
        {
            var fps = hp.GetComponent<HeroFPSController>();
            return fps != null && fps.enabled ? fps : null;
        }

        // ---- 地面 ----

        public static Vector3 Ground(Vector3 p)
        {
            p.y = GroundY(p.x, p.z, p.y);
            return p;
        }

        /// 地形の高さ。キャラ・木の当たり・結界・建物の判定は無視する。
        public static float GroundY(float x, float z, float fallback = 0f)
        {
            float y = float.MinValue;
            foreach (var hit in Physics.RaycastAll(new Vector3(x, 200f, z), Vector3.down, 400f, ~0, QueryTriggerInteraction.Ignore))
            {
                var c = hit.collider;
                if (c.CompareTag("Player") || c.CompareTag("Monster") || c.CompareTag("DemonKing") || c.GetComponentInParent<HealthComponent>() != null) continue;
                if (c.name == "Field_TreeColliders" || c.name == "Field_Barrier") continue;
                y = Mathf.Max(y, hit.point.y);
            }
            return y > float.MinValue ? y : fallback;
        }

        // ---- 見た目の部品 ----

        private static Material rockMaterial;

        /// 岩のかけら（竜巻の岩・隕石）。
        protected static Transform NewRock(Transform parent, float size, Color color)
        {
            if (rockMaterial == null)
            {
                rockMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "FieldSpellRock" };
                rockMaterial.SetFloat("_Smoothness", 0.05f);
            }
            var r = FireballVfx.NewMeshObject("Rock", parent, EarthChips.ShardMesh, rockMaterial);
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", color);
            r.SetPropertyBlock(block);
            r.transform.localScale = Vector3.one * size;
            r.transform.rotation = Random.rotation;
            return r.transform;
        }

        /// トゥーン調の煙のかたまり（雲・竜巻）。色は明るい面・暗い面・線。
        protected static MeshRenderer NewPuff(Transform parent, Color light, Color dark, Color line, MaterialPropertyBlock block)
        {
            var r = FireballVfx.NewMeshObject("Puff", parent, FireballVfx.SphereMesh, FireballVfx.GetMaterial("VFX/ToonSmoke"));
            block.Clear();
            block.SetColor("_Light", light);
            block.SetColor("_Dark", dark);
            block.SetColor("_Line", line);
            block.SetFloat("_Seed", Random.Range(0f, 50f));
            block.SetFloat("_Dissolve", 0f);
            r.SetPropertyBlock(block);
            r.transform.rotation = Random.rotation;
            return r;
        }

        protected static void SetDissolve(MeshRenderer r, float dissolve, MaterialPropertyBlock block)
        {
            r.GetPropertyBlock(block);
            block.SetFloat("_Dissolve", dissolve);
            r.SetPropertyBlock(block);
        }

        /// 光の点（雷や爆発の一瞬の明るさ）。
        protected static Light Flash(Vector3 at, Color color, float intensity, float range)
        {
            var go = new GameObject("FieldSpellFlash");
            go.transform.position = at;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            go.AddComponent<FieldSpellLightFade>();
            return light;
        }
    }

    /// Flash の光を短く消す。
    public class FieldSpellLightFade : MonoBehaviour
    {
        public float seconds = 0.35f;
        private Light lamp;
        private float start;
        private float timer;

        private void Start()
        {
            lamp = GetComponent<Light>();
            start = lamp.intensity;
        }

        private void Update()
        {
            timer += Time.deltaTime;
            float k = timer / seconds;
            if (k >= 1f)
            {
                Destroy(gameObject);
                return;
            }
            lamp.intensity = start * (1f - k) * (1f - k);
        }
    }

    /// 地面の輪（発動までの予兆・狙いの印）。消すまで残る。pulse を上げると明滅する。
    public class FieldSpellRing : MonoBehaviour
    {
        public Color color;
        public float radius;
        public float width = 0.35f;
        public float pulse;
        public float alpha = 1f;

        private MeshRenderer disc;
        private MaterialPropertyBlock block;

        public static FieldSpellRing Create(Vector3 at, float radius, Color color)
        {
            var go = new GameObject("FieldSpellRing");
            var ring = go.AddComponent<FieldSpellRing>();
            ring.color = color;
            ring.block = new MaterialPropertyBlock();
            ring.disc = FireballVfx.NewMeshObject("Ring", go.transform, FireballVfx.QuadMesh, FireballVfx.GetMaterial("VFX/ShockRing"));
            ring.disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            ring.Place(at, radius);
            return ring;
        }

        public void Place(Vector3 at, float r)
        {
            transform.position = at + Vector3.up * 0.15f;
            radius = Mathf.Max(0.2f, r);
        }

        private void LateUpdate()
        {
            float outer = radius + width;
            disc.transform.localScale = Vector3.one * (outer * 2f);
            var c = color;
            c.a *= alpha * (1f - pulse * 0.45f * (1f + Mathf.Sin(Time.time * 9f)));
            block.SetColor("_Color", c);
            block.SetFloat("_Ring", radius / outer);
            block.SetFloat("_Width", width / outer);
            disc.SetPropertyBlock(block);
        }
    }

    internal static class FieldSpellVectorExtensions
    {
        public static Vector3 WithY0(this Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}
