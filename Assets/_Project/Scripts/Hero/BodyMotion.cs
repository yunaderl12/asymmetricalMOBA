using UnityEngine;

namespace Game.Hero
{
    /// アニメーションクリップの上に重ねる体の動き（騎士の骨を使う全キャラクター共通: 勇者・BOT・魔物・魔王）。
    /// クリップは待機・走り・斬りなど数本しかないので、足りない動きをここで骨を回して補う。
    ///  - 進む向きへ腰をひねる（横移動・斜め移動でも足が進む向きを向く）。胸と頭は正面のまま。
    ///  - 速さに合わせた前傾、加速・減速での揺り戻し、曲がるときの内側への傾き。
    ///  - 空中の姿勢（跳び上がりと落下）、着地の沈み込み。
    ///  - しゃがみとスライディング（腰を落とし、足は地面に付けたままにする）。
    ///  - 被弾でのけぞる、攻撃の踏み込み、待機中の息づかい。
    /// CharacterModelAnimator が Update で Tick、LateUpdate（Animator のあと）で Apply を呼ぶ。
    public class BodyMotion
    {
        // 傾き（度）
        private const float RunLean = 6f;
        private const float AccelLean = 0.5f;
        private const float AccelLeanMax = 7f;
        private const float TurnBank = 0.035f;
        private const float TurnBankMax = 9f;
        private const float StrafeBank = 4f;
        private const float MoveTwistMax = 62f;
        private const float MoveTwistSharpness = 12f;
        // 空中
        private const float AirLegLift = 38f;
        private const float AirLegTrail = 16f;
        private const float AirArmSpread = 24f;
        // しゃがみ・スライディング・着地（m は等倍の体での長さ）
        private const float CrouchDrop = 0.26f;
        private const float CrouchLean = 16f;
        private const float SlideDrop = 0.42f;
        private const float SlideLeanBack = 8f;
        private const float SlideRecline = 24f;
        private const float SlideFootForward = 0.36f;
        private const float AnkleHeight = 0.19f;
        private const float LandDropPerSpeed = 0.022f;
        private const float LandDropMax = 0.22f;
        private const float LandStiffness = 130f;
        private const float LandDamping = 15f;
        // 被弾
        private const float FlinchSeconds = 0.26f;
        private const float FlinchDegrees = 13f;
        // 攻撃の踏み込み
        private const float LungeDistance = 0.2f;
        private const float LungeLean = 11f;
        private const float LungeWindup = 5f;
        // 上半身だけの攻撃で、腰の前傾を打ち消す上限（度）。
        private const float UpperPitchCancelMax = 35f;

        public bool Airborne;
        public float VerticalSpeed;
        /// しゃがみ（0〜1）とスライディング（0〜1）。操作している勇者が入れる。
        public float Crouch;
        public float Slide;
        /// 普段の移動の速さ（m/s）。0 より大きければ、実際の速さに合わせて足の回転を速く・遅くする。
        public float NominalSpeed;
        /// 上半身だけで攻撃を振っている度合い（0〜1）。走りの腰の前傾が上半身に乗って、おじぎにならないよう起こす。
        public float UpperOverride;

        /// 走りクリップの再生速度に掛ける倍率（実際の速さ ÷ 普段の速さ）。
        public float StrideFactor { get; private set; } = 1f;
        public float PlanarSpeed { get; private set; }
        /// 体の向きから見た速度（x: 右、z: 前）。
        public Vector3 LocalVelocity { get; private set; }

        private readonly Transform root;
        private readonly Transform model;
        private Transform hips, spineBase, spineLower, spineUpper, neck, head;
        private Transform leftUpLeg, leftLeg, leftFoot, rightUpLeg, rightLeg, rightFoot, leftArm, rightArm;
        private bool hasLegs;

        private Vector3 lastPosition;
        private float lastYaw;
        private Vector3 velocitySmooth, velocityRef;
        private float speedSmooth, lastSpeedSmooth, accelSmooth, accelRef;
        private float yawRate, yawRateRef;
        private float moveTwist;
        private float airWeight;
        private float moveWeight;
        private float landOffset, landVelocity;
        private bool wasAirborne;
        private float fallSpeed;
        private float flinchTimer;
        private Vector3 flinchAxis;
        private float flinchStrength;
        private float lungeTimer, lungeSeconds;
        private float breathSeed;

        public BodyMotion(Transform root, Transform model)
        {
            this.root = root;
            this.model = model;
            lastPosition = root.position;
            lastYaw = root.eulerAngles.y;
            breathSeed = Random.value * 20f;
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
            {
                switch (t.name)
                {
                    case "Hips": hips = t; break;
                    case "Spine02": spineBase = t; break;
                    case "Spine01": spineLower = t; break;
                    case "Spine": spineUpper = t; break;
                    case "neck": case "Neck": neck = t; break;
                    case "Head": if (head == null) head = t; break;
                    case "LeftUpLeg": leftUpLeg = t; break;
                    case "LeftLeg": leftLeg = t; break;
                    case "LeftFoot": leftFoot = t; break;
                    case "RightUpLeg": rightUpLeg = t; break;
                    case "RightLeg": rightLeg = t; break;
                    case "RightFoot": rightFoot = t; break;
                    case "LeftArm": leftArm = t; break;
                    case "RightArm": rightArm = t; break;
                }
            }
            hasLegs = hips != null && leftUpLeg != null && leftLeg != null && leftFoot != null && rightUpLeg != null && rightLeg != null && rightFoot != null;
        }

        /// 騎士の骨が見つかったか（独自の骨のモデルでは何もしない）。
        public bool Valid => hips != null && spineLower != null;

        /// 攻撃を受けた。fromDirection は攻撃が来た向き（攻撃した側 → 自分）。
        public void Flinch(Vector3 fromDirection, float strength = 1f)
        {
            fromDirection.y = 0f;
            if (fromDirection.sqrMagnitude < 0.0001f) fromDirection = -root.forward;
            // 上向きの軸を、押された向きへ倒す回転軸。
            flinchAxis = Vector3.Cross(Vector3.up, fromDirection.normalized);
            flinchStrength = Mathf.Clamp(strength, 0.3f, 1.6f);
            flinchTimer = FlinchSeconds;
        }

        /// 攻撃の踏み込み（seconds は攻撃アニメーションの長さ）。
        public void Lunge(float seconds)
        {
            lungeSeconds = Mathf.Max(0.1f, seconds);
            lungeTimer = lungeSeconds;
        }

        /// 着地した（落ちてきた速さ m/s）。
        public void Land(float speed)
        {
            landVelocity -= Mathf.Min(speed * LandDropPerSpeed, LandDropMax) * 14f;
        }

        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            Vector3 velocity = (root.position - lastPosition) / dt;
            lastPosition = root.position;
            if (Airborne) fallSpeed = Mathf.Max(fallSpeed, -velocity.y);
            velocity.y = 0f;
            // 瞬間移動（リスポーンやワープ）は速度に数えない。
            if (velocity.sqrMagnitude > 60f * 60f) velocity = Vector3.zero;
            velocitySmooth = Vector3.SmoothDamp(velocitySmooth, velocity, ref velocityRef, 0.08f, Mathf.Infinity, dt);
            LocalVelocity = root.InverseTransformDirection(velocitySmooth);
            PlanarSpeed = velocitySmooth.magnitude;

            lastSpeedSmooth = speedSmooth;
            speedSmooth = PlanarSpeed;
            accelSmooth = Mathf.SmoothDamp(accelSmooth, (speedSmooth - lastSpeedSmooth) / dt * Mathf.Sign(LocalVelocity.z == 0f ? 1f : LocalVelocity.z), ref accelRef, 0.12f, Mathf.Infinity, dt);

            float yaw = root.eulerAngles.y;
            float rate = Mathf.DeltaAngle(lastYaw, yaw) / dt;
            lastYaw = yaw;
            yawRate = Mathf.SmoothDamp(yawRate, Mathf.Clamp(rate, -400f, 400f), ref yawRateRef, 0.12f, Mathf.Infinity, dt);

            StrideFactor = NominalSpeed > 0.1f ? Mathf.Clamp(PlanarSpeed / NominalSpeed, 0.55f, 1.7f) : 1f;

            // 進む向きへのひねり。前へ進む成分があるか、真横のときだけ（後ろ走りは専用のクリップがある）。
            float twistGoal = 0f;
            bool moving = PlanarSpeed > 0.6f;
            if (moving && LocalVelocity.z > -0.25f * PlanarSpeed)
                twistGoal = Mathf.Clamp(Mathf.Atan2(LocalVelocity.x, Mathf.Max(LocalVelocity.z, 0.001f)) * Mathf.Rad2Deg, -MoveTwistMax, MoveTwistMax);
            if (Slide > 0.5f) twistGoal = 0f;
            moveTwist = Mathf.Lerp(moveTwist, twistGoal, 1f - Mathf.Exp(-MoveTwistSharpness * dt));
            moveWeight = Mathf.MoveTowards(moveWeight, moving ? 1f : 0f, dt * 5f);

            airWeight = Mathf.MoveTowards(airWeight, Airborne ? 1f : 0f, dt / (Airborne ? 0.12f : 0.1f));
            if (wasAirborne && !Airborne)
            {
                if (fallSpeed > 3f) Land(fallSpeed);
                fallSpeed = 0f;
            }
            wasAirborne = Airborne;

            // 着地の沈み込みはばねで戻す。
            landVelocity += (-LandStiffness * landOffset - LandDamping * landVelocity) * dt;
            landOffset += landVelocity * dt;
            if (landOffset > 0.02f) landOffset = 0.02f;

            if (flinchTimer > 0f) flinchTimer -= dt;
            if (lungeTimer > 0f) lungeTimer -= dt;
        }

        /// Animator が骨を決めたあとに呼ぶ。動かすのは骨だけ（Animator が毎フレーム元に戻すので、ずれが残らない）。
        /// モデルの根元の位置や大きさは、浮遊・出現・被弾の演出など他のスクリプトが使うので触らない。
        public void Apply()
        {
            if (!Valid) return;
            Vector3 up = root.up, right = root.right, forward = root.forward;
            float scale = Mathf.Abs(model.lossyScale.y);
            float refSpeed = NominalSpeed > 0.1f ? NominalSpeed : 8f;

            // ---- 攻撃の踏み込み（腰ごと少し前へ出て、上体を振る）
            float lungeLean = 0f, lungeForward = 0f;
            if (lungeTimer > 0f)
            {
                float p = 1f - lungeTimer / lungeSeconds;
                if (p < 0.22f) lungeLean = -LungeWindup * Mathf.Sin(p / 0.22f * Mathf.PI * 0.5f);
                else if (p < 0.5f)
                {
                    float k = (p - 0.22f) / 0.28f;
                    lungeLean = Mathf.Lerp(-LungeWindup, LungeLean, k * k * (3f - 2f * k));
                    lungeForward = k * k * (3f - 2f * k);
                }
                else
                {
                    float k = (p - 0.5f) / 0.5f;
                    lungeLean = LungeLean * (1f - k) * (1f - k);
                    lungeForward = (1f - k) * (1f - k);
                }
            }

            // 足を地面に付けたままにするため、腰を動かす前の足の位置と向きを覚える。
            float crouch = Mathf.Clamp01(Crouch);
            float slide = Mathf.Clamp01(Slide);
            float drop = (crouch * CrouchDrop + slide * SlideDrop - landOffset) * scale * (1f - airWeight);
            bool plant = hasLegs && (drop > 0.002f || slide > 0f);
            Vector3 leftTarget = default, rightTarget = default;
            Quaternion leftRotation = default, rightRotation = default;
            if (plant)
            {
                leftTarget = leftFoot.position;
                rightTarget = rightFoot.position;
                leftRotation = leftFoot.rotation;
                rightRotation = rightFoot.rotation;
            }

            // 走りのクリップは腰を前へ倒し、背骨と首で起こして前を見ている。上半身を攻撃のクリップで上書きすると
            // その起こしが消えるので、腰の前傾のぶんだけ背骨の根元を起こす。
            float upper = Mathf.Clamp01(UpperOverride);
            float hipsPitch = 0f;
            if (upper > 0f && spineBase != null)
            {
                Vector3 trunk = spineBase.position - hips.position;
                hipsPitch = Mathf.Clamp(Mathf.Atan2(Vector3.Dot(trunk, forward), Vector3.Dot(trunk, up)) * Mathf.Rad2Deg, 0f, UpperPitchCancelMax);
            }

            // ---- 腰: 進む向きへのひねりと、曲がる・横へ動くときの傾き
            float bank = Mathf.Clamp(-yawRate * TurnBank, -TurnBankMax, TurnBankMax) * Mathf.Clamp01(PlanarSpeed / refSpeed)
                - StrafeBank * Mathf.Clamp(LocalVelocity.x / refSpeed, -1f, 1f) * (1f - Mathf.Abs(moveTwist) / 90f);
            float twist = moveTwist * moveWeight * (1f - airWeight * 0.5f);
            // スライディングは腰ごと後ろへ倒して、足を前へ投げ出す。
            hips.rotation = Quaternion.AngleAxis(-SlideRecline * slide, right) * Quaternion.AngleAxis(bank, forward) * Quaternion.AngleAxis(twist, up) * hips.rotation;
            if (drop > 0f) hips.position -= up * drop;
            if (lungeForward > 0f) hips.position += forward * (lungeForward * LungeDistance * scale);
            if (slide > 0f) hips.position -= forward * (0.12f * slide * scale);

            // ---- 背骨: ひねりを戻して胸を正面に向け、速さ・加減速・姿勢で前後に傾ける
            float lean = RunLean * Mathf.Clamp(LocalVelocity.z / refSpeed, -0.6f, 1.7f) * (1f - upper)
                + Mathf.Clamp(accelSmooth * AccelLean, -AccelLeanMax, AccelLeanMax)
                + CrouchLean * crouch - SlideLeanBack * slide + lungeLean
                + airWeight * (VerticalSpeed > 0f ? -5f : 9f * Mathf.Clamp01(-VerticalSpeed / 8f))
                + Mathf.Clamp(-landOffset / LandDropMax, 0f, 1f) * 14f / Mathf.Max(scale, 0.5f) * 0.5f;
            float breath = Mathf.Sin((Time.time + breathSeed) * Mathf.PI * 2f * 0.28f) * 1.1f * (1f - moveWeight);
            Quaternion untwist = Quaternion.AngleAxis(-twist * 0.5f, up);
            Quaternion flinch = Quaternion.identity;
            if (flinchTimer > 0f)
            {
                float k = 1f - flinchTimer / FlinchSeconds;
                // すぐにのけぞって、ゆっくり戻る。
                float env = k < 0.2f ? k / 0.2f : 1f - (k - 0.2f) / 0.8f;
                flinch = Quaternion.AngleAxis(FlinchDegrees * flinchStrength * env * 0.5f, flinchAxis);
            }
            Quaternion half = flinch * Quaternion.AngleAxis((lean + breath) * 0.5f, right) * untwist;
            if (spineBase != null && hipsPitch > 0f) spineBase.rotation = Quaternion.AngleAxis(-hipsPitch * upper, right) * spineBase.rotation;
            if (spineBase != null) spineBase.rotation = half * spineBase.rotation;
            spineLower.rotation = half * spineLower.rotation;
            if (spineUpper != null && breath != 0f) spineUpper.rotation = Quaternion.AngleAxis(breath, right) * spineUpper.rotation;
            // 頭は傾きを少し打ち消して、前を見続ける。
            if (head != null) head.rotation = flinch * Quaternion.AngleAxis(-lean * 0.45f, right) * head.rotation;

            // ---- 空中: 片足を引き上げ、もう片方を後ろへ流し、腕を開く
            if (airWeight > 0f && hasLegs)
            {
                float rise = Mathf.Clamp01(VerticalSpeed / 4f + 0.4f);
                leftUpLeg.rotation = Quaternion.AngleAxis(-AirLegLift * airWeight * rise, right) * leftUpLeg.rotation;
                leftLeg.rotation = Quaternion.AngleAxis(AirLegLift * 1.2f * airWeight * rise, right) * leftLeg.rotation;
                rightUpLeg.rotation = Quaternion.AngleAxis(AirLegTrail * airWeight, right) * rightUpLeg.rotation;
                rightLeg.rotation = Quaternion.AngleAxis(AirLegTrail * 1.5f * airWeight, right) * rightLeg.rotation;
                if (leftArm != null) leftArm.rotation = Quaternion.AngleAxis(AirArmSpread * airWeight, forward) * leftArm.rotation;
                if (rightArm != null) rightArm.rotation = Quaternion.AngleAxis(-AirArmSpread * airWeight, forward) * rightArm.rotation;
            }

            // ---- 足を元の位置（スライディング中は体の前）へ戻す
            if (plant)
            {
                if (slide > 0f)
                {
                    Vector3 ground = root.position;
                    // 左足は前へ伸ばし、右足は膝を立てる（足首の高さに置く）。
                    Vector3 slideLeft = ground + forward * (SlideFootForward * scale) - right * (0.1f * scale) + up * (AnkleHeight * scale);
                    Vector3 slideRight = ground + forward * (SlideFootForward * 0.5f * scale) + right * (0.14f * scale) + up * (AnkleHeight * scale);
                    leftTarget = Vector3.Lerp(leftTarget, slideLeft, slide);
                    rightTarget = Vector3.Lerp(rightTarget, slideRight, slide);
                }
                SolveLeg(leftUpLeg, leftLeg, leftFoot, leftTarget, forward + up * 0.3f);
                SolveLeg(rightUpLeg, rightLeg, rightFoot, rightTarget, forward + up * 0.3f);
                leftFoot.rotation = leftRotation;
                rightFoot.rotation = rightRotation;
            }
        }

        // 2本の骨（太もも・すね）で足先を target に届かせる。hint は膝を曲げる向き。
        private static void SolveLeg(Transform upper, Transform lower, Transform end, Vector3 target, Vector3 hint)
        {
            float a = (lower.position - upper.position).magnitude;
            float b = (end.position - lower.position).magnitude;
            Vector3 toTarget = target - upper.position;
            float d = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(a - b) + 0.01f, (a + b) * 0.999f);
            Vector3 dir = toTarget.normalized;
            Vector3 axis = Vector3.Cross(dir, hint);
            if (axis.sqrMagnitude < 0.0001f) axis = Vector3.Cross(dir, Vector3.up);
            axis.Normalize();
            float hipAngle = Mathf.Acos(Mathf.Clamp((a * a + d * d - b * b) / (2f * a * d), -1f, 1f)) * Mathf.Rad2Deg;
            Vector3 kneePosition = upper.position + Quaternion.AngleAxis(hipAngle, axis) * dir * a;
            upper.rotation = Quaternion.FromToRotation(lower.position - upper.position, kneePosition - upper.position) * upper.rotation;
            lower.rotation = Quaternion.FromToRotation(end.position - lower.position, target - lower.position) * lower.rotation;
        }
    }
}
