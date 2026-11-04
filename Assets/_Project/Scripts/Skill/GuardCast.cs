using UnityEngine;
using Game.Combat;

namespace Game.Skill
{
    /// 剣士の Guard。盾を構えて光の膜をまとう。効果中は被弾のたびに膜が光る。効果中も移動と攻撃はできる。
    public class GuardCast : MeleeSkillCast
    {
        private const float FadeSeconds = 0.25f;
        private const float FlashDecay = 6f;
        private const float BaseAlpha = 0.55f;
        private const float FilmGrowth = 0.4f;

        private static readonly Vector3 RaisedPosition = new Vector3(-0.14f, -0.14f, 0.4f);
        private static readonly Vector3 RaisedEuler = new Vector3(0f, -20f, 0f);
        private static readonly Vector3 BodyFilmCenter = new Vector3(0f, 1.25f, 0.6f);
        private static readonly Vector3 BodyFilmScale = new Vector3(2.2f, 2.6f, 1.1f);
        private static readonly Vector3 ViewFilmScale = new Vector3(0.4f, 0.4f, 0.13f);
        private static readonly Vector3 ViewFilmOffset = new Vector3(0f, 0.02f, 0.12f);

        private MeshRenderer bodyFilm;
        private MeshRenderer viewFilm;
        private MaterialPropertyBlock block;
        private HealthComponent health;
        private float guardTimer;
        private float guardSeconds;
        private float flash;

        protected override float RecoverSeconds => 0.05f;

        protected override void Awake()
        {
            base.Awake();
            health = GetComponent<HealthComponent>();
            block = new MaterialPropertyBlock();
            HealthComponent.AnyDamage += OnAnyDamage;
        }

        private void OnDestroy()
        {
            HealthComponent.AnyDamage -= OnAnyDamage;
            RemoveFilms();
        }

        protected override void OnWindUp(float k)
        {
            float e = EaseOut(k);
            if (View != null) View.PoseLeft(RaisedPosition, RaisedEuler);
            if (ModelAnim != null) ModelAnim.SetGuardArm(e, Chest + transform.forward * 1.2f);
            SetCamera(new Vector3(0f, -0.04f * e, 0f), 0f, 0f, -1f * e);
        }

        protected override void OnRelease()
        {
            guardSeconds = guardTimer = Skill.duration;
            flash = 1f;
            CreateFilms();
            FireFlash.Spawn(Chest + transform.forward * 0.6f, 2.4f * SizeScale, 0.16f, 0f, Palette.Core, Palette.Main, Palette.Light, 2f);
            ShockRing.Spawn(transform.position, 0.3f, 2.2f * SizeScale, 0.3f, 0.2f, Palette.Main);
        }

        protected override void OnRecover(float seconds, float k) { }

        // 腕の位置は HeroViewModel の LateUpdate で決まるので、構えの指示はそれより前に出す。
        private void LateUpdate()
        {
            if (guardTimer <= 0f) return;
            if (View != null && guardTimer > FadeSeconds) View.PoseLeft(RaisedPosition, RaisedEuler);
            guardTimer -= Time.deltaTime;
            if (guardTimer <= 0f || (health != null && health.IsDead))
            {
                guardTimer = 0f;
                RemoveFilms();
                if (ModelAnim != null) ModelAnim.SetGuardArm(0f, Vector3.zero);
                return;
            }

            float fade = Mathf.Clamp01(guardTimer / FadeSeconds) * Mathf.Clamp01((guardSeconds - guardTimer) / 0.1f + 0.3f);
            flash = Mathf.MoveTowards(flash, 0f, FlashDecay * Time.deltaTime);
            if (ModelAnim != null) ModelAnim.SetGuardArm(Mathf.Clamp01(guardTimer / FadeSeconds), Chest + transform.forward * 1.2f);

            ApplyFilm(bodyFilm, fade);
            ApplyFilm(viewFilm, fade);
        }

        private void ApplyFilm(MeshRenderer film, float fade)
        {
            if (film == null) return;
            var c = Palette.Main;
            c.a = BaseAlpha * fade;
            block.SetColor("_Color", c);
            block.SetFloat("_Hex", 1f);
            block.SetFloat("_Flash", flash);
            film.SetPropertyBlock(block);
        }

        private void CreateFilms()
        {
            RemoveFilms();
            var material = FireballVfx.GetMaterial("VFX/EnergyFilm");
            // 体の膜は他の人から見える分。自分のカメラには映さず、自分には盾の前の膜を見せる。
            bodyFilm = FireballVfx.NewMeshObject("GuardFilm", transform, FireballVfx.SphereMesh, material);
            bodyFilm.transform.localPosition = BodyFilmCenter;
            bodyFilm.transform.localScale = BodyFilmScale * (1f + (SizeScale - 1f) * FilmGrowth);
            int layer = ModelAnim != null && ModelAnim.ModelRoot != null ? ModelAnim.ModelRoot.gameObject.layer : gameObject.layer;
            bodyFilm.gameObject.layer = layer;

            if (View != null && View.LeftHand != null)
            {
                viewFilm = FireballVfx.NewMeshObject("GuardFilmView", CameraTransform, FireballVfx.SphereMesh, material);
                viewFilm.transform.localScale = ViewFilmScale;
                viewFilm.gameObject.layer = View.LeftHand.gameObject.layer;
                viewFilm.gameObject.AddComponent<FollowHand>().Init(View.LeftHand, CameraTransform, ViewFilmOffset);
            }
        }

        private void RemoveFilms()
        {
            if (bodyFilm != null) Destroy(bodyFilm.gameObject);
            if (viewFilm != null) Destroy(viewFilm.gameObject);
            bodyFilm = viewFilm = null;
        }

        private void OnAnyDamage(HealthComponent target, int amount, GameObject attacker)
        {
            if (guardTimer <= 0f || target != health) return;
            flash = 1f;
            Vector3 point = Chest + transform.forward * 0.8f;
            FireballVfx.SpawnSparks(point, transform.forward, 10, Color.white, Palette.Spark);
            if (IsLocalPlayer) Shake(0.12f, 0.03f);
        }

        // 骨の回転に関係なく、カメラの向きのまま手の前に膜を置く。
        private class FollowHand : MonoBehaviour
        {
            private Transform hand;
            private Transform basis;
            private Vector3 offset;

            public void Init(Transform hand, Transform basis, Vector3 offset)
            {
                this.hand = hand;
                this.basis = basis;
                this.offset = offset;
            }

            private void LateUpdate()
            {
                if (hand == null || basis == null) return;
                transform.SetPositionAndRotation(hand.position + basis.rotation * offset, basis.rotation);
            }
        }
    }
}
