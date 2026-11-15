using System.Collections.Generic;
using UnityEngine;
using Game.Combat;

namespace Game.Skill
{
    /// 僧侶のスキルの共通部分。杖を回しながら掲げて光を集め、放つ瞬間に前へ振り出す。
    /// 回復と加護の光は、柱や輪ではなく、やわらかい光と立ちのぼる粒で包む（GentleLight）。
    public abstract class PriestCast : MeleeSkillCast
    {
        private const float GlowMaxSize = 1.1f;
        private const float RaiseSharpness = 1f;
        private const float LocalFlashScale = 0.3f;
        private const float LocalGlowScale = 0.25f;
        private const float AimRadius = 0.5f;

        protected static readonly Vector3 WandUpPosition = new Vector3(0.24f, -0.05f, 0.42f);
        protected static readonly Vector3 WandUpEuler = new Vector3(-8f, -12f, -14f);
        protected static readonly Vector3 BookUpPosition = new Vector3(-0.28f, -0.16f, 0.42f);
        protected static readonly Vector3 BookUpEuler = new Vector3(-10f, 30f, 0f);
        // 掲げきったときに杖を後ろへ倒す角度と、掲げながら杖先で描く円の大きさ。
        private const float RaiseTiltBack = 24f;
        private const float StirRadius = 0.035f;
        private const float StirTurns = 1.25f;
        // 放つ瞬間に杖を前へ振り出す構え（放ったあとの前半だけ）。
        private static readonly Vector3 WandCastPosition = new Vector3(0.15f, -0.03f, 0.64f);
        private static readonly Vector3 WandCastEuler = new Vector3(44f, -6f, -8f);
        private static readonly Vector3 BookOpenOffset = new Vector3(-0.07f, 0.04f, 0.07f);
        private const float CastSwingRatio = 0.55f;
        /// 回復と加護の光の色（淡いクリーム色）。
        protected static readonly Color SoftLight = new Color(1f, 0.93f, 0.7f, 1f);

        /// 放ったあとの杖の構え。標準は前へ振り出す。
        protected virtual Vector3 ReleaseWandPosition => WandCastPosition;
        protected virtual Vector3 ReleaseWandEuler => WandCastEuler;

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
            // 杖先で小さな円を描きながら掲げ、掲げきるほど後ろへ倒して力をためる。本は胸の前へ引き寄せる。
            float turn = e * StirTurns * Mathf.PI * 2f;
            Vector3 stir = new Vector3(Mathf.Cos(turn), Mathf.Sin(turn), 0f) * (StirRadius * Mathf.Sin(e * Mathf.PI));
            PoseArms(WandUpPosition + stir + new Vector3(0f, 0.05f * e, -0.04f * e), WandUpEuler + new Vector3(-RaiseTiltBack * e, 0f, 0f),
                BookUpPosition + new Vector3(0.03f * e, 0.02f * e, -0.03f * e), BookUpEuler);
            if (ModelAnim == null) return;
            ModelAnim.SetMeleePose(-0.7f * e, 0f, e, Chest + Vector3.up * 2.2f + transform.forward * 0.25f + transform.right * 0.2f + transform.right * (Mathf.Cos(turn) * 0.25f));
            ModelAnim.SetGuardArm(e, Chest + transform.forward * 0.8f + Vector3.up * 0.2f - transform.right * 0.25f);
        }

        protected override void OnRecover(float seconds, float k)
        {
            RemoveGlow();
            float settle = (1f - k) * (1f - k);
            // 前半は杖を前へ振り出して本を開き、後半は力を抜いて普段の構えへ戻る。
            if (View != null && k < CastSwingRatio) PoseArms(ReleaseWandPosition, ReleaseWandEuler, BookUpPosition + BookOpenOffset, BookUpEuler);
            if (ModelAnim != null)
            {
                ModelAnim.SetMeleePose(0.8f * settle, 0f, settle, Chest + transform.forward * 1.5f + Vector3.up * 0.35f + transform.right * 0.15f);
                ModelAnim.SetGuardArm(settle, Chest + transform.forward * 0.7f + Vector3.up * 0.05f - transform.right * 0.5f);
            }
            SetCamera(new Vector3(0f, 0f, 0.03f * settle), 1.5f * settle, 0f, 2f * settle);
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

        /// やわらかい光とゆっくり昇る粒で、回復された相手を包む。
        protected void BlessEffect(HealthComponent target, float seconds)
        {
            GentleLight.Wrap(target.transform, SizeScale, seconds, SoftLight, PillarLayerFor(target));
        }

        /// 杖の先から相手へ、小さな光の玉を飛ばす。
        protected void SendOrbs(HealthComponent target)
        {
            GentleLight.Orbs(WandTip, target.transform, 0.34f, SoftLight, SizeScale);
        }

        /// 光の本体を置く層。操作している本人を包む光は、視界をふさがないよう本人のカメラに映らない層（本人のモデルと同じ）にする。
        protected static int PillarLayerFor(HealthComponent target)
        {
            if (target.GetComponent<Game.Hero.HumanHeroMarker>() == null) return -1;
            var model = target.GetComponent<Game.Hero.CharacterModelAnimator>();
            return model != null && model.ModelRoot != null ? model.ModelRoot.gameObject.layer : -1;
        }

        // 視線の先で一番近い味方。壁や地面に遮られていれば見つからない扱い。
        protected HealthComponent FindAllyInAim()
        {
            if (CameraTransform == null) return null;
            HealthComponent best = null;
            float bestDistance = float.PositiveInfinity;
            float wall = float.PositiveInfinity;
            foreach (var h in Physics.SphereCastAll(CameraTransform.position, AimRadius, CameraTransform.forward, Skill.range, ~0, QueryTriggerInteraction.Collide))
            {
                if (h.collider.transform.IsChildOf(transform)) continue;
                var hp = h.collider.GetComponentInParent<HealthComponent>();
                if (hp == null)
                {
                    if (!h.collider.isTrigger && h.distance < wall) wall = h.distance;
                    continue;
                }
                if (hp.IsDead || !hp.CompareTag("Player") || h.distance >= bestDistance) continue;
                best = hp;
                bestDistance = h.distance;
            }
            return bestDistance < wall ? best : null;
        }

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
