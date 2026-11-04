using UnityEngine;
using UnityEngine.AI;
using Game.Combat;

namespace Game.Skill
{
    /// 魔法使いの Explosion。長く溜める間、狙った地面に魔法陣が開き、杖から細くなっていく光の線が伸びる。
    /// 溜めの終わりに魔法陣から光の柱と白い炎が立ちのぼり、爆発する。
    public class ExplosionCast : MeleeSkillCast
    {
        private const float FinaleSeconds = 0.9f;
        private const float RuneOpenSeconds = 0.8f;
        private const float RuneSpinPerSecond = 12f;
        private const float RuneLift = 0.12f;
        private const float BeamStartWidth = 0.28f;
        private const float BeamEndWidth = 0.025f;
        private const float WallBackOff = 0.4f;
        private const float WalkableSearch = 6f;
        private const float ShakeSeconds = 0.7f;
        private const float ShakeSize = 0.32f;
        private const float StarInterval = 0.11f;
        private const float HandGlowSize = 0.6f;
        private const float LocalHandGlowScale = 0.3f;
        private const float TeardropLength = 2.6f;
        private const float TeardropGrowth = 3.2f;
        private const int RaycastBuffer = 16;

        private static readonly Color RuneColor = new Color(0.55f, 1.5f, 1.7f, 1f);
        private static readonly Color BeamColor = new Color(0.85f, 1f, 1f, 1f);
        private static readonly RaycastHit[] HitBuffer = new RaycastHit[RaycastBuffer];

        private MeshRenderer rune;
        private MeshRenderer handGlow;
        private MeshRenderer teardrop;
        private LineRenderer beam;
        private MaterialPropertyBlock block;
        private Vector3 targetPoint;
        private bool finaleStarted;
        private float nextStar;
        private float windUpSeconds;

        protected override float RecoverSeconds => 0.45f;
        protected override MeleeFx.Palette Palette => MeleeFx.Arcane;

        private float BlastRadius => Skill.areaRadius * SizeScale;

        private Vector3 WandTip
        {
            get
            {
                if (IsLocalPlayer && CameraTransform != null)
                    return CameraTransform.position + CameraTransform.forward * 0.7f + CameraTransform.right * 0.34f - CameraTransform.up * 0.04f;
                return Chest + transform.forward * 0.6f + transform.right * 0.3f + Vector3.up * 0.6f;
            }
        }

        protected override void OnWindUp(float k)
        {
            if (rune == null) BuildCharge();
            windUpSeconds = Skill.castTime * k;
            UpdateTarget();
            UpdateRune(k);
            UpdateBeam(k);
            Pose(EaseOut(Mathf.Clamp01(k * 4f)));
            if (Skill.castTime - windUpSeconds <= FinaleSeconds) UpdateFinale();
        }

        private void BuildCharge()
        {
            block = new MaterialPropertyBlock();
            finaleStarted = false;
            nextStar = 0f;

            rune = FireballVfx.NewMeshObject("ExplosionRune", null, FireballVfx.QuadMesh, FireballVfx.GetMaterial("VFX/RuneCircle"));
            rune.transform.localScale = Vector3.zero;

            var beamObject = new GameObject("ExplosionBeam");
            beam = beamObject.AddComponent<LineRenderer>();
            beam.sharedMaterial = FireballVfx.Trail;
            beam.positionCount = 2;
            beam.startColor = beam.endColor = BeamColor;
            beam.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.2f);
            beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            beam.receiveShadows = false;

            handGlow = FireballVfx.NewMeshObject("ExplosionHandGlow", null, FireballVfx.QuadMesh, FireballVfx.Glow);
            var glowBlock = new MaterialPropertyBlock();
            glowBlock.SetColor("_Color", new Color(0.5f, 1.6f, 2.2f));
            glowBlock.SetFloat("_Pull", 0f);
            handGlow.SetPropertyBlock(glowBlock);
        }

        // 視線が最初に当たる場所の真下を、歩ける地面へ寄せる。
        private void UpdateTarget()
        {
            Vector3 origin = CameraTransform != null ? CameraTransform.position : Chest + Vector3.up * 0.5f;
            Vector3 aim = Controller != null ? Controller.AimForward : (CameraTransform != null ? CameraTransform.forward : transform.forward);
            float distance = ScaledRange;
            int count = Physics.RaycastNonAlloc(origin, aim, HitBuffer, ScaledRange, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (HitBuffer[i].collider.transform.IsChildOf(transform)) continue;
                distance = Mathf.Min(distance, HitBuffer[i].distance);
            }
            Vector3 point = IceField.SnapToGround(origin + aim * Mathf.Max(0f, distance - WallBackOff));
            if (NavMesh.SamplePosition(point, out var walkable, WalkableSearch, NavMesh.AllAreas)) point = walkable.position;
            targetPoint = point;
        }

        private void UpdateRune(float k)
        {
            float open = EaseOut(Mathf.Clamp01(windUpSeconds / RuneOpenSeconds));
            rune.transform.SetPositionAndRotation(targetPoint + Vector3.up * RuneLift, Quaternion.Euler(90f, windUpSeconds * RuneSpinPerSecond, 0f));
            rune.transform.localScale = Vector3.one * (BlastRadius * 2f * open);
            block.SetColor("_Color", RuneColor);
            block.SetFloat("_Fill", Mathf.Lerp(0.05f, 0.5f, k * k));
            rune.SetPropertyBlock(block);
        }

        private void UpdateBeam(float k)
        {
            Vector3 tip = WandTip;
            beam.SetPosition(0, tip);
            beam.SetPosition(1, targetPoint + Vector3.up * RuneLift);
            beam.widthMultiplier = Mathf.Lerp(BeamStartWidth, BeamEndWidth, k) * (IsLocalPlayer ? 0.5f : 1f);
            handGlow.transform.position = tip;
            handGlow.transform.localScale = Vector3.one * (HandGlowSize * Mathf.Lerp(0.3f, 1f, k) * (IsLocalPlayer ? LocalHandGlowScale : 1f));
            if (Camera.main != null) handGlow.transform.rotation = Camera.main.transform.rotation;
        }

        private void Pose(float e)
        {
            // 自分の画面の杖は構えたままにして、光の線の根元を宝石に合わせる。
            if (ModelAnim != null) ModelAnim.SetCastPose(-e, e, Chest + Vector3.up * 1.2f + transform.forward * 0.5f);
            SetCamera(new Vector3(0f, 0.02f * e, -0.08f * e), -1f * e, 0f, -3f * e);
        }

        // 溜めの最後: 光の柱と白い炎が立ち、星がきらめく。
        private void UpdateFinale()
        {
            if (!finaleStarted)
            {
                finaleStarted = true;
                LightPillar.Spawn(targetPoint, BlastRadius * 0.14f, BlastRadius * 4f, FinaleSeconds + 0.1f, new Color(0.5f, 1.4f, 2f), true);
                ShockRing.Spawn(targetPoint, BlastRadius, BlastRadius * 0.3f, FinaleSeconds, BlastRadius * 0.1f, Color.white);
                teardrop = FireballVfx.NewMeshObject("ExplosionTeardrop", null, FlameMesh.Get(), FireballVfx.GetMaterial("VFX/MagicFlame"));
                teardrop.transform.rotation = Quaternion.LookRotation(Vector3.down);
                var flame = new MaterialPropertyBlock();
                flame.SetColor("_Outer", new Color(0.5f, 1.4f, 2f));
                flame.SetColor("_Body", new Color(1.5f, 2.2f, 2.6f));
                flame.SetColor("_Hot", new Color(2.6f, 2.9f, 3f));
                flame.SetColor("_Pink", new Color(3f, 3f, 3f));
                flame.SetColor("_Core", new Color(3f, 3f, 3f));
                flame.SetFloat("_Bend", 0.15f);
                flame.SetFloat("_Seed", Random.Range(0f, 50f));
                teardrop.SetPropertyBlock(flame);
            }
            float k = Mathf.Clamp01(1f - (Skill.castTime - windUpSeconds) / FinaleSeconds);
            float length = TeardropLength * SizeScale * EaseOut(k) * TeardropGrowth;
            teardrop.transform.position = targetPoint + Vector3.up * (length * 0.16f + 0.1f);
            teardrop.transform.localScale = Vector3.one * length;

            if (windUpSeconds >= nextStar)
            {
                nextStar = windUpSeconds + StarInterval;
                Vector2 flat = Random.insideUnitCircle * (BlastRadius * 0.5f);
                SparkleStar.Spawn(targetPoint + new Vector3(flat.x, BlastRadius * Random.Range(0.5f, 1.3f), flat.y), Random.Range(0.8f, 2f), Random.Range(0.35f, 0.6f), Vector3.up * 2f);
            }
        }

        protected override void OnRelease()
        {
            DestroyCharge();
            ExplosionBurst.Spawn(targetPoint, BlastRadius);

            Vector3 from = targetPoint + Vector3.up * 1.5f;
            var targets = FindEnemies(from, BlastRadius, Vector3.forward, -1f);
            foreach (var target in targets)
                SkillKnockback.Play(target.gameObject, target.transform.position - targetPoint, Skill.knockbackDistance);
            int hits = HitEnemies(targets, from, 2.4f);
            Debug.Log($"[Skill] Explosion hit {hits} target(s) at {targetPoint}");
            Shake(ShakeSeconds, ShakeSize * SizeScale);
        }

        protected override void OnRecover(float seconds, float k)
        {
            float settle = (1f - k) * (1f - k);
            if (ModelAnim != null) ModelAnim.SetCastPose(0.6f * settle, settle, Chest + Vector3.up * 1.2f + transform.forward * 0.5f);
            SetCamera(new Vector3(0f, 0f, 0.1f * settle), settle, 0f, 2f * settle);
        }

        protected override void OnPoseEnd()
        {
            DestroyCharge();
            if (ModelAnim != null) ModelAnim.SetCastPose(0f, 0f, Vector3.zero);
        }

        private void DestroyCharge()
        {
            if (rune != null) Destroy(rune.gameObject);
            if (handGlow != null) Destroy(handGlow.gameObject);
            if (teardrop != null) Destroy(teardrop.gameObject);
            if (beam != null) Destroy(beam.gameObject);
            rune = handGlow = teardrop = null;
            beam = null;
        }
    }
}
