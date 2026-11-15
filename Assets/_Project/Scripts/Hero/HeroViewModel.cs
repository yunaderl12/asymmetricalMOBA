using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Game.Combat;

namespace Game.Hero
{
    /// 1人称カメラに映す自分の腕と武器。ジョブのモデルから腕と持ち物の面だけを取り出して使う。
    /// 骨はコードで動かす。普段は HeroClassData の構えをとり、スキルは PoseRight / PoseLeft で毎フレーム上書きする。
    /// 壁にめり込まないよう、専用 Layer を重ね描き用のカメラで最後に描く。
    [DefaultExecutionOrder(5)] // カメラの揺れを反映した後に骨を置く
    public class HeroViewModel : MonoBehaviour
    {
        public const string LayerName = "ViewModel";

        private const float CameraFov = 50f;
        private const float CameraNear = 0.03f;
        private const float CameraFar = 20f;
        private const float PoseSharpness = 34f;
        private const float IdleSharpness = 14f;
        // 腕の揺れ。カメラと同じ歩幅で、縦にだけ少し揺れる。
        private const float BobPerMeter = 0.98f;
        private const float BobSize = 0.01f;
        private const float SwayPerDegree = 0.0016f;
        private const float SwayLimit = 0.05f;
        private const float SwaySharpness = 10f;
        // 通常攻撃の振り: 引く → 振り抜く → 振り抜いた先で少し残る。
        private const float AttackPullSeconds = 0.07f;
        private const float AttackSwingSeconds = 0.1f;
        private const float AttackFollowSeconds = 0.13f;
        // 続けて振るときに、次の振りを返しの振り（左から右）にする間隔。
        private const float ComboSeconds = 0.9f;

        private const float ArmStretch = 0.94f;

        private static readonly string[] KeptBodyBones = { "LeftForeArm", "LeftHand", "RightForeArm", "RightHand" };

        private class Arm
        {
            public Transform Upper, Fore, Hand;
            public Quaternion UpperRest, ForeRest, HandIdle;
            public float UpperLength, ForeLength;
            public Vector3 Shoulder;
            public float Side;
            public Vector3 IdlePosition;
            public Quaternion IdleRotation;
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 PosePosition;
            public Quaternion PoseRotation;
            public int PoseFrame = -1;
        }

        private Transform cameraTransform;
        private Transform root;
        private GameObject model;
        private Mesh filteredMesh;
        private Arm right, left;
        private HealthComponent health;
        private HeroFPSController controller;
        private CharacterController characterController;
        private CharacterModelAnimator avatar;
        private HeroAttack attack;
        private bool thrustAttack;
        private float attackAge = -1f;
        private float bobPhase;
        private Vector3 sway;
        private float lastYaw;
        private float lastPitch;
        // 体の動きに合わせた腕の揺れ（空中の遅れ、止まる・走り出す揺り戻し、姿勢ごとの構え、横移動の傾き、息づかい）。
        private float airLag, airLagVelocity;
        private float surge, surgeVelocity, lastForwardSpeed;
        private float dashBlend, crouchBlend, slideBlend, strafeRoll;
        private float breathPhase;
        private bool backhand;
        private float lastAttackTime = -10f;

        public bool IsVisible => root != null && root.gameObject.activeSelf;
        public Transform LeftHand => left != null ? left.Hand : null;
        public Transform RightHand => right != null ? right.Hand : null;
        public Vector3 RightIdlePosition => right != null ? right.IdlePosition : Vector3.zero;
        public Vector3 LeftIdlePosition => left != null ? left.IdlePosition : Vector3.zero;

        /// 自分が操作する勇者にだけ付ける。専用 Layer やカメラが無ければ何もしない。
        public static HeroViewModel Attach(GameObject hero, HeroClassData data)
        {
            if (string.IsNullOrEmpty(data.modelPath) || hero.GetComponent<HeroViewModel>() != null) return null;
            int layer = LayerMask.NameToLayer(LayerName);
            var camera = hero.GetComponentInChildren<Camera>(true);
            var prefab = Resources.Load<GameObject>(data.modelPath);
            if (layer < 0 || camera == null || prefab == null)
            {
                Debug.LogWarning("[HeroViewModel] missing layer, camera or model: " + data.modelPath);
                return null;
            }
            var view = hero.AddComponent<HeroViewModel>();
            view.Build(camera, prefab, data, layer);
            return view;
        }

        /// 今フレームの右手の構え。位置はカメラから見た座標、回転は待機姿勢の手を基準にした角度。
        public void PoseRight(Vector3 position, Vector3 euler)
        {
            SetPose(right, position, euler);
        }

        public void PoseLeft(Vector3 position, Vector3 euler)
        {
            SetPose(left, position, euler);
        }

        private static void SetPose(Arm arm, Vector3 position, Vector3 euler)
        {
            if (arm == null) return;
            arm.PosePosition = position;
            arm.PoseRotation = Quaternion.Euler(euler);
            arm.PoseFrame = Time.frameCount;
        }

        private void Build(Camera baseCamera, GameObject prefab, HeroClassData data, int layer)
        {
            cameraTransform = baseCamera.transform;
            health = GetComponent<HealthComponent>();
            controller = GetComponent<HeroFPSController>();
            characterController = GetComponent<CharacterController>();
            thrustAttack = data.basicAttackMpCost > 0;
            attack = GetComponent<HeroAttack>();
            if (attack != null) attack.Attacked += OnAttacked;

            root = new GameObject("ViewModel").transform;
            root.SetParent(cameraTransform, false);
            model = Instantiate(prefab, root);
            model.name = prefab.name + "_View";
            model.transform.localScale = Vector3.one * data.viewScale;

            var bones = new Dictionary<string, Transform>();
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) bones[t.name] = t;
            right = MakeArm(bones, "Right", 1f, data.viewRightShoulder, data.viewRightPosition, data.viewRightEuler);
            left = MakeArm(bones, "Left", -1f, data.viewLeftShoulder, data.viewLeftPosition, data.viewLeftEuler);
            if (right == null || left == null)
            {
                Debug.LogWarning("[HeroViewModel] arm bones not found on " + prefab.name);
                root.gameObject.SetActive(false);
                return;
            }
            CaptureIdlePose(data.modelPath, bones);

            FloatingHeldItem.AttachIfPresent(model);
            foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                FilterBody(skin);
                skin.updateWhenOffscreen = true;
                skin.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
            AddOverlayCamera(baseCamera, layer);

            lastYaw = transform.eulerAngles.y;
            lastPitch = controller != null ? controller.LookPitch : 0f;
            enabled = true;
        }

        private static Arm MakeArm(Dictionary<string, Transform> bones, string side, float sign, Vector3 shoulder, Vector3 position, Vector3 euler)
        {
            if (!bones.TryGetValue(side + "Arm", out var upper) || !bones.TryGetValue(side + "ForeArm", out var fore) || !bones.TryGetValue(side + "Hand", out var hand)) return null;
            var arm = new Arm { Upper = upper, Fore = fore, Hand = hand, Side = sign, Shoulder = shoulder };
            arm.UpperLength = Vector3.Distance(upper.position, fore.position);
            arm.ForeLength = Vector3.Distance(fore.position, hand.position);
            arm.IdlePosition = arm.Position = position;
            arm.IdleRotation = arm.Rotation = Quaternion.Euler(euler);
            return arm;
        }

        // 構えの回転は「待機姿勢の手」を基準にする（武器はこの姿勢で立つように作ってある）。骨の位置は元のモデルのまま使う。
        private void CaptureIdlePose(string modelPath, Dictionary<string, Transform> bones)
        {
            var positions = new Dictionary<Transform, Vector3>();
            foreach (var bone in bones.Values) positions[bone] = bone.localPosition;

            var animator = model.GetComponentInChildren<Animator>(true);
            if (animator == null) animator = model.AddComponent<Animator>();
            var avatars = Resources.LoadAll<Avatar>(modelPath);
            if (avatars != null && avatars.Length > 0) animator.avatar = avatars[0];
            animator.runtimeAnimatorController = Resources.Load<RuntimeAnimatorController>("Knight/KnightAnimator");
            animator.Rebind();
            animator.Update(0f);
            Quaternion toModel = Quaternion.Inverse(model.transform.rotation);
            foreach (var arm in new[] { right, left })
            {
                arm.UpperRest = toModel * arm.Upper.rotation;
                arm.ForeRest = toModel * arm.Fore.rotation;
                arm.HandIdle = toModel * arm.Hand.rotation;
            }
            Destroy(animator);
            foreach (var pair in positions) pair.Key.localPosition = pair.Value;
        }

        // 体のメッシュは前腕と手の面だけ残す。持ち物は別マテリアルなのでそのまま。
        private void FilterBody(SkinnedMeshRenderer skin)
        {
            var source = skin.sharedMesh;
            var materials = skin.sharedMaterials;
            if (source == null || !System.Array.Exists(materials, m => m != null && m.name.Contains("_Body"))) return;
            if (!source.isReadable)
            {
                Debug.LogWarning("[HeroViewModel] enable Read/Write on the model: " + source.name);
                return;
            }

            var kept = new bool[skin.bones.Length];
            for (int i = 0; i < kept.Length; i++) kept[i] = skin.bones[i] != null && System.Array.IndexOf(KeptBodyBones, skin.bones[i].name) >= 0;
            var weights = source.boneWeights;

            filteredMesh = Instantiate(source);
            for (int s = 0; s < source.subMeshCount; s++)
            {
                if (s >= materials.Length || materials[s] == null || !materials[s].name.Contains("_Body")) continue;
                var triangles = source.GetTriangles(s);
                var result = new List<int>(triangles.Length / 8);
                for (int i = 0; i + 2 < triangles.Length; i += 3)
                {
                    if (!kept[weights[triangles[i]].boneIndex0] || !kept[weights[triangles[i + 1]].boneIndex0] || !kept[weights[triangles[i + 2]].boneIndex0]) continue;
                    result.Add(triangles[i]);
                    result.Add(triangles[i + 1]);
                    result.Add(triangles[i + 2]);
                }
                filteredMesh.SetTriangles(result, s);
            }
            skin.sharedMesh = filteredMesh;
        }

        private void AddOverlayCamera(Camera baseCamera, int layer)
        {
            int bit = 1 << layer;
            baseCamera.cullingMask &= ~bit;
            var go = new GameObject("ViewModelCamera");
            go.transform.SetParent(cameraTransform, false);
            var overlay = go.AddComponent<Camera>();
            overlay.cullingMask = bit;
            overlay.fieldOfView = CameraFov;
            overlay.nearClipPlane = CameraNear;
            overlay.farClipPlane = CameraFar;
            var overlayData = overlay.GetUniversalAdditionalCameraData();
            overlayData.renderType = CameraRenderType.Overlay;
            overlayData.renderShadows = false;
            baseCamera.GetUniversalAdditionalCameraData().cameraStack.Add(overlay);
        }

        private void OnDestroy()
        {
            if (attack != null) attack.Attacked -= OnAttacked;
            if (filteredMesh != null) Destroy(filteredMesh);
        }

        private void OnAttacked()
        {
            // 続けて振ると、振り下ろしと返しの振りが交互に出る。
            backhand = Time.time - lastAttackTime < ComboSeconds && !backhand;
            lastAttackTime = Time.time;
            attackAge = 0f;
        }

        private bool ShouldShow()
        {
            if (avatar == null) avatar = GetComponent<CharacterModelAnimator>();
            if (health != null && health.IsDead) return false;
            if (avatar != null && avatar.IsRespawnAnimPlaying) return false;
            return controller == null || !controller.Suspended;
        }

        private void LateUpdate()
        {
            if (root == null || right == null || left == null) return;
            bool show = ShouldShow();
            if (root.gameObject.activeSelf != show) root.gameObject.SetActive(show);
            if (!show) return;

            float dt = Time.deltaTime;
            UpdateSway(dt);
            UpdateAttack(dt);
            UpdateArm(right, dt);
            UpdateArm(left, dt);
        }

        private void UpdateSway(float dt)
        {
            Vector3 velocity = characterController != null && characterController.enabled ? characterController.velocity : Vector3.zero;
            float vertical = velocity.y;
            velocity.y = 0f;
            float speed = velocity.magnitude;
            Vector3 local = transform.InverseTransformDirection(velocity);
            bool grounded = characterController == null || characterController.isGrounded;
            float blend = 1f - Mathf.Exp(-9f * dt);
            dashBlend = Mathf.Lerp(dashBlend, controller != null && controller.IsDashing ? 1f : 0f, blend);
            slideBlend = Mathf.Lerp(slideBlend, controller != null && controller.IsSliding ? 1f : 0f, blend);
            crouchBlend = Mathf.Lerp(crouchBlend, controller != null && controller.CurrentStance == HeroFPSController.Stance.Crouching ? 1f : 0f, blend);

            // 歩きの沈み（縦だけ。走ると少し大きくなる）。スライディング中は足を動かさないので止める。
            bobPhase += speed * BobPerMeter * dt;
            float sink = Mathf.Sin(bobPhase);
            Vector3 bob = new Vector3(0f, -sink * sink, 0f) * (BobSize * Mathf.Clamp01(speed / 3f) * (1f + 0.6f * dashBlend) * (1f - slideBlend) * (grounded ? 1f : 0f));

            // 立ち止まっているときの息づかい。
            breathPhase += dt;
            Vector3 breath = new Vector3(0f, Mathf.Sin(breathPhase * Mathf.PI * 2f * 0.22f) * 0.0035f, 0f) * (1f - Mathf.Clamp01(speed / 1.5f));

            // 空中: 跳び上がるときは腕が遅れて下がり、落ちるときは浮く。着地で沈んでから戻る。
            float lagTarget = grounded ? 0f : Mathf.Clamp(-vertical * 0.007f, -0.06f, 0.05f);
            airLag = Mathf.SmoothDamp(airLag, lagTarget, ref airLagVelocity, 0.09f, Mathf.Infinity, dt);

            // 走り出すと腕が遅れて手前へ、止まると前へ流れてから戻る。
            float accel = dt > 0f ? (local.z - lastForwardSpeed) / dt : 0f;
            lastForwardSpeed = local.z;
            surge = Mathf.SmoothDamp(surge, Mathf.Clamp(-accel * 0.0009f, -0.03f, 0.03f), ref surgeVelocity, 0.1f, Mathf.Infinity, dt);

            // 横へ動くと、動く向きへ武器が少し傾く。
            strafeRoll = Mathf.Lerp(strafeRoll, Mathf.Clamp(-local.x * 0.45f, -3f, 3f), blend);

            float yaw = transform.eulerAngles.y;
            float pitch = controller != null ? controller.LookPitch : 0f;
            Vector3 target = new Vector3(-Mathf.DeltaAngle(lastYaw, yaw), pitch - lastPitch, 0f) * SwayPerDegree;
            target = Vector3.ClampMagnitude(target, SwayLimit);
            sway = Vector3.Lerp(sway, target, 1f - Mathf.Exp(-SwaySharpness * dt));
            lastYaw = yaw;
            lastPitch = pitch;

            // 姿勢ごとの構え: 走りは武器を少し下げて前へ倒す。しゃがみは体に引き寄せる。スライディングは持ち上げて傾ける。
            Vector3 stance = dashBlend * new Vector3(0f, -0.03f, -0.015f)
                + crouchBlend * new Vector3(-0.012f, 0.012f, -0.02f)
                + slideBlend * new Vector3(0.02f, 0.03f, -0.03f);
            float stancePitch = dashBlend * 7f - slideBlend * 6f - airLag * 90f;
            float stanceRoll = strafeRoll - crouchBlend * 4f + slideBlend * 10f;
            root.localPosition = bob + sway + breath + stance + new Vector3(0f, airLag, surge);
            root.localRotation = Quaternion.Euler(stancePitch, 0f, stanceRoll);
        }

        // 通常攻撃の振り。剣や斧は「引く → 振り抜く → 残る」で、続けて振ると返しの振りになる。魔法のジョブは杖を引いてから突き出す。
        private void UpdateAttack(float dt)
        {
            if (attackAge < 0f || right.PoseFrame == Time.frameCount) return;
            attackAge += dt;
            float total = AttackPullSeconds + AttackSwingSeconds + AttackFollowSeconds;
            if (attackAge >= total)
            {
                attackAge = -1f;
                return;
            }
            Vector3 idleEuler = right.IdleRotation.eulerAngles;
            Vector3 pullPos, pullEuler, hitPos, hitEuler, endPos, endEuler;
            if (thrustAttack)
            {
                pullPos = new Vector3(0.02f, -0.02f, -0.09f); pullEuler = new Vector3(-14f, 0f, 0f);
                hitPos = new Vector3(-0.05f, 0.05f, 0.26f); hitEuler = new Vector3(40f, 0f, -6f);
                endPos = new Vector3(-0.04f, 0.03f, 0.2f); endEuler = new Vector3(30f, 0f, -4f);
            }
            else if (backhand)
            {
                // 返しの振り: 左に引いて、右上へ払う。
                pullPos = new Vector3(-0.24f, 0.02f, 0.02f); pullEuler = new Vector3(20f, -45f, 30f);
                hitPos = new Vector3(0.14f, 0.06f, 0.24f); hitEuler = new Vector3(35f, 40f, -45f);
                endPos = new Vector3(0.2f, 0.1f, 0.16f); endEuler = new Vector3(25f, 55f, -55f);
            }
            else
            {
                // 振り下ろし: 右上に引いて、左下へ斬る。
                pullPos = new Vector3(0.1f, 0.14f, -0.06f); pullEuler = new Vector3(-32f, 12f, -18f);
                hitPos = new Vector3(-0.28f, -0.09f, 0.22f); hitEuler = new Vector3(72f, -32f, 16f);
                endPos = new Vector3(-0.34f, -0.16f, 0.15f); endEuler = new Vector3(86f, -40f, 22f);
            }

            Vector3 position, euler;
            if (attackAge < AttackPullSeconds)
            {
                float k = EaseOut(attackAge / AttackPullSeconds);
                position = pullPos * k;
                euler = pullEuler * k;
            }
            else if (attackAge < AttackPullSeconds + AttackSwingSeconds)
            {
                // 振り抜きは出だしが速い。
                float k = EaseOut((attackAge - AttackPullSeconds) / AttackSwingSeconds);
                position = Vector3.Lerp(pullPos, hitPos, k);
                euler = Vector3.Lerp(pullEuler, hitEuler, k);
            }
            else
            {
                float k = (attackAge - AttackPullSeconds - AttackSwingSeconds) / AttackFollowSeconds;
                position = Vector3.Lerp(hitPos, endPos, k);
                euler = Vector3.Lerp(hitEuler, endEuler, k);
            }
            PoseRight(right.IdlePosition + position, idleEuler + euler);
            // 反対の手は、振りと逆へ少し引いて釣り合いをとる（盾や本を持つ手）。
            if (left.PoseFrame != Time.frameCount)
            {
                float swing = Mathf.Sin(Mathf.Clamp01(attackAge / total) * Mathf.PI);
                PoseLeft(left.IdlePosition + new Vector3(-0.03f, -0.02f, -0.05f) * swing, left.IdleRotation.eulerAngles);
            }
        }

        private static float EaseOut(float k)
        {
            k = Mathf.Clamp01(k);
            return 1f - (1f - k) * (1f - k);
        }

        private void UpdateArm(Arm arm, float dt)
        {
            bool posed = arm.PoseFrame == Time.frameCount;
            Vector3 targetPosition = posed ? arm.PosePosition : arm.IdlePosition;
            Quaternion targetRotation = posed ? arm.PoseRotation : arm.IdleRotation;
            float blend = 1f - Mathf.Exp(-(posed ? PoseSharpness : IdleSharpness) * dt);
            arm.Position = Vector3.Lerp(arm.Position, targetPosition, blend);
            arm.Rotation = Quaternion.Slerp(arm.Rotation, targetRotation, blend);
            SolveArm(arm);
        }

        // 手の位置から腕を逆算する。モデルの腕は短いので肩は固定せず、Shoulder の方角へ腕を伸ばす（肘は外側の下へ曲げる）。
        private void SolveArm(Arm arm)
        {
            float a = arm.UpperLength;
            float b = arm.ForeLength;

            Vector3 hand = root.TransformPoint(arm.Position);
            Vector3 toHand = hand - root.TransformPoint(arm.Shoulder);
            Vector3 direction = toHand.sqrMagnitude > 1e-6f ? toHand.normalized : cameraTransform.forward;
            float distance = (a + b) * ArmStretch;
            Vector3 shoulder = hand - direction * distance;
            float along = (a * a - b * b + distance * distance) / (2f * distance);
            float height = Mathf.Sqrt(Mathf.Max(a * a - along * along, 0f));
            Vector3 hint = cameraTransform.TransformDirection(new Vector3(arm.Side * 0.7f, -1f, -0.2f));
            Vector3 bend = Vector3.ProjectOnPlane(hint, direction).normalized;
            Vector3 elbow = shoulder + direction * along + bend * height;

            Quaternion view = root.rotation;
            arm.Upper.position = shoulder;
            arm.Upper.rotation = view * arm.UpperRest;
            Aim(arm.Upper, arm.Fore, elbow);
            arm.Fore.rotation = view * arm.ForeRest;
            Aim(arm.Fore, arm.Hand, hand);
            arm.Hand.rotation = view * arm.Rotation * arm.HandIdle;
        }

        private static void Aim(Transform bone, Transform child, Vector3 target)
        {
            bone.rotation = Quaternion.FromToRotation(child.position - bone.position, target - bone.position) * bone.rotation;
        }
    }
}
