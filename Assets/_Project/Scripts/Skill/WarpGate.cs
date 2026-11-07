using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Game.Bot;
using Game.Combat;
using Game.Core;
using Game.Hero;

namespace Game.Skill
{
    /// 魔法使いのワープの門1つ。泡がくっついた形の穴で、縁がネオンに光る（シェーダー Game/VFX/WarpGate）。
    /// 2つの門がつながると、表からも裏からも反対側の門の向こうが見え、どちらからでも通り抜けられる。
    /// 表から入ると反対側の門の表から、裏から入ると反対側の門の裏から出てくる（門の中を通り抜けた先がそのまま見えている景色）。
    /// 反対側の景色は、反対側の門の後ろに置いたカメラで描いて RenderTexture に入れる。
    /// 置いた向き: 門の表（+Z）は置いた勇者の方を向く。足元が transform の位置。
    public class WarpGate : MonoBehaviour
    {
        private static readonly List<WarpGate> all = new List<WarpGate>();
        private static bool renderHooked;

        public WarpGate Partner { get; private set; }
        public bool IsLinked => Partner != null && !Partner.closing;
        public bool IsClosing => closing;
        public float Remaining => closeAt - Time.time;

        private MeshRenderer meshRenderer;
        private Material material;
        private float width, height;
        private float open;
        private bool closing;
        private float closeAt;
        private float linked;
        private Camera portalCamera;
        private RenderTexture portalTexture;
        private Light glowLight;
        private readonly Dictionary<Transform, float> lastSide = new Dictionary<Transform, float>();
        private readonly Dictionary<Transform, float> passCooldown = new Dictionary<Transform, float>();
        private static readonly List<Transform> heroes = new List<Transform>();
        private static float heroesRefreshAt;

        private static readonly int OpenId = Shader.PropertyToID("_Open");
        private static readonly int LinkedId = Shader.PropertyToID("_Linked");
        private static readonly int PortalTexId = Shader.PropertyToID("_PortalTex");

        /// 足元 position に、表が forward を向く門を作る。lifetime 秒で閉じる。
        public static WarpGate Create(Vector3 position, Vector3 forward, float lifetime)
        {
            var cfg = BalanceConfig.Instance;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            var go = new GameObject("WarpGate");
            go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward.normalized, Vector3.up));
            var gate = go.AddComponent<WarpGate>();
            gate.Build(cfg.warpGateWidth, cfg.warpGateHeight);
            gate.closeAt = Time.time + lifetime;
            return gate;
        }

        /// 2つの門をつなぐ。両方とも linkedSeconds 後に閉じる。
        public static void Link(WarpGate a, WarpGate b, float linkedSeconds)
        {
            if (a == null || b == null || a == b) return;
            a.Partner = b;
            b.Partner = a;
            a.closeAt = b.closeAt = Time.time + linkedSeconds;
        }

        public void Close()
        {
            if (closing) return;
            closing = true;
            if (Partner != null && !Partner.closing) Partner.Close();
        }

        private void Build(float w, float h)
        {
            width = w;
            height = h;
            var mesh = new Mesh { name = "WarpGateQuad" };
            float bottom = -0.1f;
            mesh.vertices = new[]
            {
                new Vector3(-w * 0.5f, bottom, 0f), new Vector3(w * 0.5f, bottom, 0f),
                new Vector3(-w * 0.5f, bottom + h, 0f), new Vector3(w * 0.5f, bottom + h, 0f),
            };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var visual = new GameObject("Visual");
            visual.transform.SetParent(transform, false);
            visual.AddComponent<MeshFilter>().sharedMesh = mesh;
            meshRenderer = visual.AddComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            var shader = Shader.Find("Game/VFX/WarpGate");
            material = new Material(shader != null ? shader : Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetVector("_Size", new Vector4(w, h, 0f, 0f));
            material.SetFloat("_Seed", Random.Range(0f, 10f));
            material.SetFloat(OpenId, 0f);
            material.SetFloat(LinkedId, 0f);
            meshRenderer.sharedMaterial = material;

            var lightGo = new GameObject("Glow");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = new Vector3(0f, h * 0.5f, 0.6f);
            glowLight = lightGo.AddComponent<Light>();
            glowLight.type = LightType.Point;
            glowLight.color = new Color(0.75f, 0.5f, 1f);
            glowLight.range = 6f;
            glowLight.intensity = 0f;
            glowLight.shadows = LightShadows.None;

            all.Add(this);
            HookRendering();
        }

        private void OnDestroy()
        {
            all.Remove(this);
            if (portalCamera != null) Destroy(portalCamera.gameObject);
            if (portalTexture != null) { portalTexture.Release(); Destroy(portalTexture); }
            if (material != null) Destroy(material);
        }

        private void Update()
        {
            var cfg = BalanceConfig.Instance;
            if (!closing && Time.time >= closeAt) Close();
            if (!closing && Partner != null && Partner.closing) Close();

            float speed = 1f / Mathf.Max(0.05f, closing ? cfg.warpGateOpenSeconds * 0.7f : cfg.warpGateOpenSeconds);
            open = Mathf.MoveTowards(open, closing ? 0f : 1f, speed * Time.deltaTime);
            linked = Mathf.MoveTowards(linked, IsLinked ? 1f : 0f, 2f * Time.deltaTime);
            material.SetFloat(OpenId, open);
            material.SetFloat(LinkedId, linked);
            glowLight.intensity = 2.2f * open;
            if (closing && open <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            if (IsLinked && open > 0.9f) CheckPassage(cfg);
        }

        // ---- 通り抜け ----

        private void CheckPassage(BalanceConfig cfg)
        {
            RefreshHeroes();
            foreach (var hero in heroes)
            {
                if (hero == null) continue;
                var hp = hero.GetComponent<HealthComponent>();
                if (hp != null && hp.IsDead) { lastSide.Remove(hero); continue; }
                Vector3 local = transform.InverseTransformPoint(hero.position + Vector3.up * 0.9f);
                bool within = Mathf.Abs(local.x) < width * 0.42f && local.y > -0.6f && local.y < height && Mathf.Abs(local.z) < 2.5f;
                float side = local.z;
                bool had = lastSide.TryGetValue(hero, out float prev);
                lastSide[hero] = side;
                if (!within || !had) continue;
                bool fromFront = prev > 0f && side <= 0f;
                bool fromBack = prev < 0f && side >= 0f;
                if (fromFront || fromBack)
                {
                    if (passCooldown.TryGetValue(hero, out float until) && Time.time < until) continue;
                    Pass(hero, cfg, fromFront);
                }
            }
        }

        private void Pass(Transform hero, BalanceConfig cfg, bool fromFront)
        {
            var exit = Partner;
            Quaternion turn = exit.transform.rotation * Quaternion.Euler(0f, 180f, 0f) * Quaternion.Inverse(transform.rotation);
            // 表から入ったら出口の表へ、裏から入ったら出口の裏へ出る。
            Vector3 target = exit.transform.position + exit.transform.forward * (fromFront ? cfg.warpGateExitDistance : -cfg.warpGateExitDistance);
            if (Physics.Raycast(target + Vector3.up * 1.2f, Vector3.down, out var hit, 5f, ~0, QueryTriggerInteraction.Ignore))
                target = hit.point + Vector3.up * 0.05f;
            Quaternion rot = Quaternion.Euler(0f, (turn * hero.rotation).eulerAngles.y, 0f);

            var cc = hero.GetComponent<CharacterController>();
            var agent = hero.GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.Warp(target);
                hero.rotation = rot;
            }
            else
            {
                bool ccOn = cc != null && cc.enabled;
                if (cc != null) cc.enabled = false;
                hero.SetPositionAndRotation(target, rot);
                if (cc != null) cc.enabled = ccOn;
            }
            float until = Time.time + cfg.warpGatePassCooldown;
            passCooldown[hero] = until;
            exit.passCooldown[hero] = until;
            exit.lastSide.Remove(hero);
            lastSide.Remove(hero);
            Debug.Log($"[WarpGate] {hero.name} passed to {target}");
        }

        private static void RefreshHeroes()
        {
            if (Time.time < heroesRefreshAt) return;
            heroesRefreshAt = Time.time + 1f;
            heroes.Clear();
            if (HumanHeroMarker.Instance != null) heroes.Add(HumanHeroMarker.Instance.transform);
            foreach (var bot in FindObjectsByType<BotHero>(FindObjectsSortMode.None)) heroes.Add(bot.transform);
        }

        // ---- 反対側の景色 ----

        private void LateUpdate()
        {
            var main = Camera.main;
            bool show = IsLinked && linked > 0.01f && open > 0.01f && main != null && meshRenderer.isVisible
                        && (main.transform.position - transform.position).sqrMagnitude < 120f * 120f;
            if (!show)
            {
                if (portalCamera != null) portalCamera.enabled = false;
                return;
            }
            EnsureCamera(main);

            var exit = Partner.transform;
            Matrix4x4 m = exit.localToWorldMatrix * Matrix4x4.Rotate(Quaternion.Euler(0f, 180f, 0f)) * transform.worldToLocalMatrix * main.transform.localToWorldMatrix;
            portalCamera.transform.SetPositionAndRotation(m.GetColumn(3), Quaternion.LookRotation(m.GetColumn(2), m.GetColumn(1)));
            portalCamera.fieldOfView = main.fieldOfView;
            portalCamera.nearClipPlane = main.nearClipPlane;
            portalCamera.farClipPlane = main.farClipPlane;
            portalCamera.ResetProjectionMatrix();

            // 出口の門よりカメラ側にあるものは映さない（表から見ても裏から見ても、門の向こうだけを映す）。
            Vector3 n = exit.forward;
            Vector3 p = exit.position;
            int sign = System.Math.Sign(Vector3.Dot(n, p - portalCamera.transform.position));
            if (sign == 0) sign = 1;
            Matrix4x4 w2c = portalCamera.worldToCameraMatrix;
            Vector3 cp = w2c.MultiplyPoint(p);
            Vector3 cn = w2c.MultiplyVector(n) * sign;
            float cd = -Vector3.Dot(cp, cn) + 0.05f;
            if (Mathf.Abs(cd) > 0.2f) portalCamera.projectionMatrix = main.CalculateObliqueMatrix(new Vector4(cn.x, cn.y, cn.z, cd));
            portalCamera.enabled = true;
        }

        private void EnsureCamera(Camera main)
        {
            int w = Mathf.Max(64, Screen.width / 2), h = Mathf.Max(64, Screen.height / 2);
            if (portalTexture == null || portalTexture.width != w || portalTexture.height != h)
            {
                if (portalTexture != null) { portalTexture.Release(); Destroy(portalTexture); }
                portalTexture = new RenderTexture(w, h, 24, RenderTextureFormat.DefaultHDR) { name = "WarpGateView" };
                material.SetTexture(PortalTexId, portalTexture);
                if (portalCamera != null) portalCamera.targetTexture = portalTexture;
            }
            if (portalCamera != null) return;
            var go = new GameObject("WarpGateCamera");
            portalCamera = go.AddComponent<Camera>();
            portalCamera.CopyFrom(main);
            portalCamera.depth = main.depth - 5f;
            portalCamera.targetTexture = portalTexture;
            int avatar = LayerMask.NameToLayer("LocalAvatar");
            int viewModel = LayerMask.NameToLayer(HeroViewModel.LayerName);
            int mask = main.cullingMask;
            if (avatar >= 0) mask |= 1 << avatar;
            if (viewModel >= 0) mask &= ~(1 << viewModel);
            portalCamera.cullingMask = mask;
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            data.renderType = CameraRenderType.Base;
            portalCamera.enabled = false;
        }

        // 門のカメラで描くあいだは、門そのものを隠す（自分の絵を読みながら書くのを防ぐ）。
        private static void HookRendering()
        {
            if (renderHooked) return;
            renderHooked = true;
            RenderPipelineManager.beginCameraRendering += (ctx, cam) => SetGatesVisible(!IsPortalCamera(cam));
            RenderPipelineManager.endCameraRendering += (ctx, cam) => { if (IsPortalCamera(cam)) SetGatesVisible(true); };
        }

        private static bool IsPortalCamera(Camera cam)
        {
            foreach (var g in all) if (g != null && g.portalCamera == cam) return true;
            return false;
        }

        private static void SetGatesVisible(bool visible)
        {
            foreach (var g in all) if (g != null && g.meshRenderer != null) g.meshRenderer.enabled = visible;
        }
    }
}
