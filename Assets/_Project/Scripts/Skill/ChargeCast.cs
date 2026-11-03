using UnityEngine;

namespace Game.Skill
{
    /// 剣士の Charge。身を沈めて剣を引き、向いている方向へ突進する。最初にぶつかった敵1体に当たって止まる。
    public class ChargeCast : MeleeSkillCast
    {
        private const float ProbeRadius = 1.6f;
        private const float ProbeCone = 110f;
        private const float StuckRatio = 0.25f;
        private const float GhostInterval = 0.035f;
        private const float GhostSeconds = 0.28f;
        private const float ShakeSeconds = 0.26f;
        private const float ShakeSize = 0.09f;

        private static readonly Vector3 DrawnPosition = new Vector3(0.3f, -0.17f, 0.26f);
        private static readonly Vector3 DrawnEuler = new Vector3(60f, 15f, 0f);
        private static readonly Vector3 ThrustPosition = new Vector3(0.14f, -0.15f, 0.42f);
        private static readonly Vector3 ThrustEuler = new Vector3(72f, -10f, 0f);
        private static readonly Vector3 ShieldTuckPosition = new Vector3(-0.3f, -0.3f, 0.36f);

        private CharacterController characterController;
        private Vector3 direction;
        private float traveled;
        private float nextGhost;
        private ParticleSystem speedLines;

        protected override float RecoverSeconds => 0.32f;

        protected override void Awake()
        {
            base.Awake();
            characterController = GetComponent<CharacterController>();
        }

        protected override void OnWindUp(float k)
        {
            float e = EaseOut(k);
            if (View != null)
            {
                View.PoseRight(DrawnPosition, DrawnEuler);
                View.PoseLeft(ShieldTuckPosition, Vector3.zero);
            }
            if (ModelAnim != null) ModelAnim.SetMeleePose(0.8f * e, 0f, e, Chest - transform.forward * 0.6f + transform.right * 0.5f);
            SetCamera(new Vector3(0f, -0.2f * e, -0.1f * e), 1.5f * e, 0f, -4f * e);
        }

        protected override void OnRelease()
        {
            direction = transform.forward;
            traveled = 0f;
            nextGhost = 0f;
            MeleeFx.DustBurst(transform.position, 0.4f, 14, 3.5f);
            ShockRing.Spawn(transform.position, 0.3f, 2.4f, 0.28f, 0.25f, MeleeFx.SteelBlue);
            if (IsLocalPlayer && CameraTransform != null) speedLines = SpawnSpeedLines();
        }

        protected override bool TickActive(float seconds)
        {
            if (View != null)
            {
                View.PoseRight(ThrustPosition, ThrustEuler);
                View.PoseLeft(ShieldTuckPosition, Vector3.zero);
            }
            if (ModelAnim != null) ModelAnim.SetMeleePose(1f, 0f, 1f, Chest + direction * 2f);
            SetCamera(new Vector3(0f, -0.1f, 0.12f), 1f, 0f, 12f);

            if (seconds >= nextGhost && ModelAnim != null)
            {
                AfterImage.Spawn(ModelAnim.ModelRoot, MeleeFx.SteelBlue, GhostSeconds);
                nextGhost = seconds + GhostInterval;
            }

            var targets = FindEnemies(Chest, ProbeRadius, direction, ProbeCone);
            if (targets.Count > 0)
            {
                // 単体技なので、一番近い1体だけに当てる。
                Slam(Closest(targets));
                return false;
            }

            float step = Mathf.Min(Skill.dashSpeed * Time.deltaTime, Skill.range - traveled);
            Vector3 before = transform.position;
            characterController.Move(direction * step);
            float moved = Vector3.ProjectOnPlane(transform.position - before, Vector3.up).magnitude;
            traveled += moved;
            if (traveled >= Skill.range - 0.01f) { StopDash(); return false; }
            if (step > 0f && moved < step * StuckRatio)
            {
                MeleeFx.Impact(Chest + direction * 0.7f, -direction, 1.2f);
                Shake(ShakeSeconds * 0.6f, ShakeSize * 0.5f);
                StopDash();
                return false;
            }
            return true;
        }

        private Game.Combat.HealthComponent Closest(System.Collections.Generic.List<Game.Combat.HealthComponent> targets)
        {
            Game.Combat.HealthComponent best = null;
            float bestDistance = float.PositiveInfinity;
            foreach (var hp in targets)
            {
                float d = (hp.transform.position - transform.position).sqrMagnitude;
                if (d >= bestDistance) continue;
                bestDistance = d;
                best = hp;
            }
            return best;
        }

        private void Slam(Game.Combat.HealthComponent target)
        {
            StopDash();
            var single = new System.Collections.Generic.List<Game.Combat.HealthComponent> { target };
            Vector3 point = Chest + direction * 0.7f;
            int hits = HitEnemies(single, Chest, 3.2f);
            Debug.Log($"[Skill] Charge hit {hits} target(s)");
            ShockRing.Spawn(target.transform.position, 0.4f, 3.6f, 0.34f, 0.35f, MeleeFx.SteelCore);
            MeleeFx.DustBurst(transform.position + direction, 0.8f, 18, 4.5f);
            FireballVfx.SpawnSparks(point, direction, 22, Color.white, new Color(0.45f, 0.75f, 1f));
            Shake(ShakeSeconds, ShakeSize);
        }

        private void StopDash()
        {
            if (speedLines != null)
            {
                speedLines.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                speedLines = null;
            }
        }

        protected override void OnRecover(float seconds, float k)
        {
            float settle = (1f - k) * (1f - k);
            SetCamera(new Vector3(0f, -0.1f * settle, 0.12f * settle), settle, 0f, 12f * settle);
            if (ModelAnim != null) ModelAnim.SetMeleePose(settle, 0f, settle, Chest + direction * 2f);
        }

        protected override void OnPoseEnd()
        {
            StopDash();
        }

        // 視界の外へ流れていく細い線。カメラの子に置いて、走っている間だけ出す。
        private ParticleSystem SpawnSpeedLines()
        {
            var ps = FireballVfx.NewParticles("SpeedLines", CameraTransform);
            ps.transform.localPosition = Vector3.forward * 7f;
            ps.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            var main = ps.main;
            main.duration = 1f;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.14f, 0.24f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(26f, 38f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.05f);
            main.startColor = MeleeFx.SteelLight;
            main.maxParticles = 90;
            main.stopAction = ParticleSystemStopAction.Destroy;

            var emission = ps.emission;
            emission.rateOverTime = 260f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 4f;
            shape.radiusThickness = 0.35f;

            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.05f;
            r.lengthScale = 2f;

            ps.gameObject.SetActive(true);
            ps.Play();
            return ps;
        }
    }
}
