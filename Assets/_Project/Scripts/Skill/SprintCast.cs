using UnityEngine;
using Game.Combat;

namespace Game.Skill
{
    /// 共通の Sprint。足元から風が弾けて駆け出す。効果の間は走った跡に残像と土煙を残す。
    public class SprintCast : MeleeSkillCast
    {
        private const float FovKick = 8f;

        private ParticleSystem speedLines;

        protected override float RecoverSeconds => 0.35f;
        protected override MeleeFx.Palette Palette => MeleeFx.Verdant;

        protected override void OnWindUp(float k) { }

        protected override void OnRelease()
        {
            ShockRing.Spawn(transform.position, 0.3f, 2.6f * SizeScale, 0.3f, 0.3f, Palette.Main);
            MeleeFx.DustBurst(transform.position, 0.5f, 14, 3.5f);
            if (IsLocalPlayer && CameraTransform != null) speedLines = MeleeFx.SpawnSpeedLines(CameraTransform, Palette.Light);
            if (ModelAnim != null) gameObject.AddComponent<SprintTrail>().Init(ModelAnim, Skill.duration, Palette.Main, SizeScale);
        }

        protected override void OnRecover(float seconds, float k)
        {
            float settle = (1f - k) * (1f - k);
            SetCamera(Vector3.zero, 0f, 0f, FovKick * settle);
            if (k > 0.5f) StopLines();
        }

        protected override void OnPoseEnd()
        {
            StopLines();
        }

        private void StopLines()
        {
            if (speedLines == null) return;
            speedLines.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            speedLines = null;
        }

        private class SprintTrail : MonoBehaviour
        {
            private const float GhostInterval = 0.11f;
            private const float GhostSeconds = 0.3f;
            private const float DustInterval = 0.18f;
            private const float MovingSpeed = 1f;

            private Game.Hero.CharacterModelAnimator model;
            private HealthComponent health;
            private Color color;
            private float scale;
            private float timer;
            private float nextGhost;
            private float nextDust;
            private Vector3 lastPosition;

            public void Init(Game.Hero.CharacterModelAnimator model, float seconds, Color color, float scale)
            {
                this.model = model;
                this.color = color;
                this.scale = scale;
                timer = seconds;
                health = GetComponent<HealthComponent>();
                lastPosition = transform.position;
            }

            private void Update()
            {
                timer -= Time.deltaTime;
                if (timer <= 0f || (health != null && health.IsDead))
                {
                    Destroy(this);
                    return;
                }
                float speed = Time.deltaTime > 0f ? (transform.position - lastPosition).magnitude / Time.deltaTime : 0f;
                lastPosition = transform.position;
                if (speed < MovingSpeed) return;

                nextGhost -= Time.deltaTime;
                nextDust -= Time.deltaTime;
                if (nextGhost <= 0f)
                {
                    AfterImage.Spawn(model.ModelRoot, color, GhostSeconds * scale);
                    nextGhost = GhostInterval;
                }
                if (nextDust <= 0f)
                {
                    MeleeFx.DustBurst(transform.position, 0.3f, 4, 2f);
                    nextDust = DustInterval;
                }
            }
        }
    }
}
