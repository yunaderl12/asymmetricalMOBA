using System.Collections.Generic;
using UnityEngine;
using Game.Combat;

namespace Game.Skill
{
    /// 僧侶のスキルの共通部分。杖の先に光を集めて、放つ瞬間に解き放つ。
    public abstract class PriestCast : MeleeSkillCast
    {
        private const float GlowMaxSize = 1.1f;
        private const float RaiseSharpness = 1f;
        private const float LocalFlashScale = 0.3f;
        private const float LocalGlowScale = 0.25f;

        protected static readonly Vector3 WandUpPosition = new Vector3(0.24f, -0.05f, 0.42f);
        protected static readonly Vector3 WandUpEuler = new Vector3(-8f, -12f, -14f);
        protected static readonly Vector3 BookUpPosition = new Vector3(-0.28f, -0.16f, 0.42f);
        protected static readonly Vector3 BookUpEuler = new Vector3(-10f, 30f, 0f);

        private static readonly List<HealthComponent> AllyBuffer = new List<HealthComponent>();
        private static readonly HashSet<HealthComponent> AllySeen = new HashSet<HealthComponent>();

        private MeshRenderer glow;
        private MaterialPropertyBlock glowBlock;

        protected override MeleeFx.Palette Palette => MeleeFx.Holy;
        protected override float RecoverSeconds => 0.3f;

        protected Vector3 WandTip
        {
            get
            {
                if (IsLocalPlayer && CameraTransform != null)
                    return CameraTransform.position + CameraTransform.forward * 0.7f + CameraTransform.right * 0.28f + CameraTransform.up * 0.08f;
                return Chest + transform.forward * 0.6f + transform.right * 0.3f + Vector3.up * 0.6f;
            }
        }

        protected override void OnWindUp(float k)
        {
            float e = EaseOut(Mathf.Clamp01(k * RaiseSharpness));
            PoseForWindUp(e);
            UpdateGlow(e);
            SetCamera(new Vector3(0f, 0.02f * e, -0.05f * e), -1f * e, 0f, -2f * e);
        }

        /// 溜めの姿勢。標準は杖を高く掲げて身を反らす。
        protected virtual void PoseForWindUp(float e)
        {
            PoseArms(WandUpPosition, WandUpEuler, BookUpPosition, BookUpEuler);
            if (ModelAnim == null) return;
            ModelAnim.SetMeleePose(-0.5f * e, 0f, e, Chest + Vector3.up * 2f + transform.forward * 0.4f + transform.right * 0.2f);
            ModelAnim.SetGuardArm(e, Chest + transform.forward * 1f + Vector3.up * 0.1f - transform.right * 0.3f);
        }

        protected override void OnRecover(float seconds, float k)
        {
            RemoveGlow();
            float settle = (1f - k) * (1f - k);
            if (View != null && settle > 0.3f) PoseArms(WandUpPosition, WandUpEuler, BookUpPosition, BookUpEuler);
            if (ModelAnim != null)
            {
                ModelAnim.SetMeleePose(0.3f * settle, 0f, settle, Chest + Vector3.up * 2f + transform.forward * 0.4f + transform.right * 0.2f);
                ModelAnim.SetGuardArm(settle, Chest + transform.forward * 1f + Vector3.up * 0.1f - transform.right * 0.3f);
            }
            SetCamera(Vector3.zero, 1f * settle, 0f, 2f * settle);
        }

        protected override void OnPoseEnd()
        {
            RemoveGlow();
            if (ModelAnim != null) ModelAnim.SetGuardArm(0f, Vector3.zero);
        }

        protected void PoseArms(Vector3 wandPosition, Vector3 wandEuler, Vector3 bookPosition, Vector3 bookEuler)
        {
            if (View == null) return;
            View.PoseRight(wandPosition, wandEuler);
            View.PoseLeft(bookPosition, bookEuler);
        }

        /// 放つ瞬間の杖先の閃光。
        protected void ReleaseFlash(float size)
        {
            RemoveGlow();
            // 自分の杖先は目のすぐ前にあるので、視界をふさがない大きさにする。
            float flash = size * SizeScale * (IsLocalPlayer ? LocalFlashScale : 1f);
            FireFlash.Spawn(WandTip, flash, 0.16f, 0f, Palette.Core, Palette.Main, Palette.Light, 2.5f);
        }

        /// 光の柱と立ちのぼる粒で、回復された相手を示す。
        protected void BlessEffect(Vector3 feet, float radius, float height, float seconds, bool onSelf)
        {
            LightPillar.Spawn(feet, radius, height, seconds, Palette.Main * 1.6f, true, onSelf ? SelfOnlyHiddenLayer : -1);
            FireballVfx.SpawnSparks(feet + Vector3.up * 0.2f, Vector3.up, Mathf.RoundToInt(18 * SizeScale), Color.white, Palette.Spark);
        }

        /// 自分のカメラに映らず、他の人からは見える層（自分のモデルと同じ）。
        protected int SelfOnlyHiddenLayer => ModelAnim != null && ModelAnim.ModelRoot != null ? ModelAnim.ModelRoot.gameObject.layer : -1;

        /// 半径内の生きている味方（自分を含む）。
        protected List<HealthComponent> FindAllies(Vector3 origin, float radius)
        {
            AllyBuffer.Clear();
            AllySeen.Clear();
            foreach (var col in Physics.OverlapSphere(origin, radius, ~0, QueryTriggerInteraction.Collide))
            {
                var hp = col.GetComponentInParent<HealthComponent>();
                if (hp == null || hp.IsDead || !hp.CompareTag("Player") || !AllySeen.Add(hp)) continue;
                AllyBuffer.Add(hp);
            }
            return AllyBuffer;
        }

        private void UpdateGlow(float k)
        {
            if (glow == null)
            {
                glow = FireballVfx.NewMeshObject("WandGlow", null, FireballVfx.QuadMesh, FireballVfx.Glow);
                glowBlock = new MaterialPropertyBlock();
                glowBlock.SetColor("_Color", Palette.Main);
                glowBlock.SetFloat("_Pull", 0.3f);
                glow.SetPropertyBlock(glowBlock);
            }
            glow.transform.position = WandTip;
            glow.transform.localScale = Vector3.one * (GlowMaxSize * SizeScale * k * (IsLocalPlayer ? LocalGlowScale : 1f));
        }

        private void RemoveGlow()
        {
            if (glow == null) return;
            Destroy(glow.gameObject);
            glow = null;
        }
    }
}
