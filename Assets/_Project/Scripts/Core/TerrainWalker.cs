using System;
using UnityEngine;

namespace Game.Core
{
    /// 歩いている地形で速さが変わるものに付ける。浅瀬では少し遅くなって波紋が立ち、上り坂では SlopePenalty の分だけ遅くなる。
    /// 砂漠の道から外れた砂地では遅くなって砂が跳ねる（SandSlows が false の魔物は砂が跳ねるだけ）。
    public class TerrainWalker : MonoBehaviour
    {
        private const float CheckInterval = 0.1f;
        private const float FullPenaltySlope = 30f; // この角度の上りで SlopePenalty がそのまま効く
        private const float MinMoveSpeed = 1f;
        private const float ShallowMaxHeight = 0.7f;
        private const float RippleMoveDistance = 0.4f;
        private const float RippleVisibleDistance = 70f;
        private const float SlopeSmoothing = 8f;

        /// 上り30度での速度の低下割合（0.45 = 45%遅くなる）。
        public float SlopePenalty { get; set; }
        public float SpeedFactor { get; private set; } = 1f;
        public bool InShallow { get; private set; }
        public bool OnLooseSand { get; private set; }
        /// false にすると砂地でも遅くならない（砂漠に住む魔物）。
        public bool SandSlows { get; set; } = true;
        /// 跳ねる砂の大きさの倍率（見た目の大きさに合わせる。魔王は大きい）。
        public float SplashScale { get; set; } = 1f;
        public event Action FactorChanged;

        private CharacterController controller;
        private Vector3 lastPosition;
        private float checkTimer;
        private float rippleTimer;
        private float sandTimer;
        private float slope;

        public static TerrainWalker For(GameObject go)
        {
            var walker = go.GetComponent<TerrainWalker>();
            return walker != null ? walker : go.AddComponent<TerrainWalker>();
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            lastPosition = transform.position;
            checkTimer = UnityEngine.Random.value * CheckInterval;
        }

        private void Update()
        {
            rippleTimer -= Time.deltaTime;
            sandTimer -= Time.deltaTime;
            checkTimer -= Time.deltaTime;
            if (checkTimer > 0f) return;
            float elapsed = CheckInterval - checkTimer;
            checkTimer = CheckInterval;

            Vector3 position = transform.position;
            Vector3 delta = position - lastPosition;
            lastPosition = position;
            float rise = delta.y;
            delta.y = 0f;
            float run = delta.magnitude;

            bool grounded = controller == null || controller.isGrounded;
            float target = grounded && run / Mathf.Max(elapsed, 0.001f) > MinMoveSpeed && rise > 0f ? Mathf.Atan2(rise, run) * Mathf.Rad2Deg : 0f;
            slope = Mathf.Lerp(slope, target, Mathf.Clamp01(SlopeSmoothing * elapsed));

            InShallow = position.y < ShallowMaxHeight && StageMap.IsShallow(position);
            if (InShallow && run > RippleMoveDistance * elapsed * 10f && rippleTimer <= 0f) SpawnRipple(position);

            OnLooseSand = !InShallow && grounded && StageMap.IsLooseSand(position);
            if (OnLooseSand && run / Mathf.Max(elapsed, 0.001f) > MinMoveSpeed && sandTimer <= 0f) SpawnSand(position, delta / Mathf.Max(elapsed, 0.001f));

            var config = BalanceConfig.Instance;
            float factor = (InShallow ? config.shallowSpeedMultiplier : 1f) * (OnLooseSand && SandSlows ? config.looseSandSpeedMultiplier : 1f)
                * (1f - SlopePenalty * Mathf.Clamp01(slope / FullPenaltySlope));
            if (Mathf.Abs(factor - SpeedFactor) > 0.01f)
            {
                SpeedFactor = factor;
                FactorChanged?.Invoke();
            }
        }

        private void SpawnSand(Vector3 position, Vector3 velocity)
        {
            sandTimer = BalanceConfig.Instance.sandSplashInterval;
            var cam = Camera.main;
            if (cam != null && (cam.transform.position - position).sqrMagnitude > RippleVisibleDistance * RippleVisibleDistance) return;
            SandSplash.Spawn(position, velocity, BalanceConfig.Instance.sandSplashGrains, SplashScale);
        }

        private void SpawnRipple(Vector3 position)
        {
            rippleTimer = BalanceConfig.Instance.rippleInterval;
            var cam = Camera.main;
            if (cam != null && (cam.transform.position - position).sqrMagnitude > RippleVisibleDistance * RippleVisibleDistance) return;
            WaterRipple.Spawn(position, BalanceConfig.Instance.rippleRadius);
        }
    }
}
