using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Game.Combat;

namespace Game.Hero
{
    /// 1人称カメラに映す自分の腕と武器。クラスのモデルから腕と持ち物の面だけを取り出して使う。
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
        private const float BobPerMeter = 1.7f;
        private const float BobSize = 0.012f;
        private const float SwayPerDegree = 0.0016f;
        private const float SwayLimit = 0.05f;
        private const float SwaySharpness = 10f;
        private const float AttackPullSeconds = 0.06f;
        private const float AttackSwingSeconds = 0.12f;

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
            velocity.y = 0f;
            bobPhase += velocity.magnitude * BobPerMeter * dt;
            Vector3 bob = new Vector3(Mathf.Cos(bobPhase), -Mathf.Abs(Mathf.Sin(bobPhase)), 0f) * (BobSize * Mathf.Clamp01(velocity.magnitude / 3f));

            float yaw = transform.eulerAngles.y;
            float pitch = controller != null ? controller.LookPitch : 0f;
            Vector3 target = new Vector3(-Mathf.DeltaAngle(lastYaw, yaw), pitch - lastPitch, 0f) * SwayPerDegree;
            target = Vector3.ClampMagnitude(target, SwayLimit);
            sway = Vector3.Lerp(sway, target, 1f - Mathf.Exp(-SwaySharpness * dt));
            lastYaw = yaw;
            lastPitch = pitch;
            root.localPosition = bob + sway;
        }

        // 通常攻撃の小さな振り。魔法のクラスは突き出すだけ。
        private void UpdateAttack(float dt)
        {
            if (attackAge < 0f || right.PoseFrame == Time.frameCount) return;
            attackAge += dt;
            float total = AttackPullSeconds + AttackSwingSeconds;
            if (attackAge >= total)
            {
                attackAge = -1f;
                return;
            }
            Vector3 idleEuler = right.IdleRotation.eulerAngles;
            if (thrustAttack)
            {
                PoseRight(right.IdlePosition + new Vector3(-0.05f, 0.04f, 0.22f), idleEuler + new Vector3(35f, 0f, 0f));
            }
            else if (attackAge < AttackPullSeconds)
            {
                PoseRight(right.IdlePosition + new Vector3(0.08f, 0.12f, -0.05f), idleEuler + new Vector3(-25f, 10f, -15f));
            }
            else
            {
                PoseRight(right.IdlePosition + new Vector3(-0.28f, -0.08f, 0.2f), idleEuler + new Vector3(70f, -30f, 15f));
            }
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
