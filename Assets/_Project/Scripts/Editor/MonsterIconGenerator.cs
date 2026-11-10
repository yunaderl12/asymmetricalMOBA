using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Game.Hero;
using Game.Monster;

namespace Game.Editor
{
    /// 魔王ロビーで使う魔物・中ボスの丸いアイコンを、3D モデル（modelPath）から描き出す。
    /// Art/Characters/MonsterIcons/<アセット名>_Icon.png に書き、MonsterData.icon / BossData.icon に入れる。
    /// 立っている魔物（背が高い）は頭と上半身、横に長い魔物（ドラゴン・スコーピオンなど）は体全体を写す。
    /// 描画の手順は ClassPortraitGenerator（勇者の顔アイコン）と同じ。絵を描き直したら icon を差し替えればよい。
    public static class MonsterIconGenerator
    {
        private const string MonsterDir = "Assets/_Project/ScriptableObjects/Monsters";
        private const string BossDir = "Assets/_Project/ScriptableObjects/Bosses";
        private const string OutDir = "Assets/_Project/Art/Characters/MonsterIcons";
        private const int IconSize = 256;
        private const float BustFraction = 0.62f; // 立っている魔物で写す、上からの高さの割合
        private const float UprightRatio = 0.8f; // 高さ ÷ 横の広がり がこれ以上なら「立っている」
        private static readonly Color Backdrop = new Color(0.55f, 0.36f, 0.62f); // 魔王ロビーのフェルトに合う紫
        private static readonly Vector3 ViewDirection = new Vector3(0.45f, 0.2f, 1f);

        private enum Framing
        {
            Bust, // 頭と上半身（横に広がる翼や腕は切れてよい）
            Body, // 体全体の高さに合わせ、横にはみ出す翼や尾は切れてよい
            Whole, // 体全体（横に長い魔物）
        }

        /// アセット名ごとの写し方。ない魔物は高さと横の広がりの比で決める（UprightRatio）。
        private static readonly Dictionary<string, Framing> Framings = new Dictionary<string, Framing>
        {
            { "BasicMob", Framing.Bust }, { "Dryad", Framing.Bust }, { "Harpy", Framing.Bust },
            { "Mummy", Framing.Bust }, { "Ifrit", Framing.Bust },
            { "Cockatrice", Framing.Body }, { "Wendigo", Framing.Body },
            { "Golem", Framing.Whole }, { "Dragon", Framing.Whole }, { "Scorpion", Framing.Whole },
        };

        [MenuItem("Tools/Characters/Generate Monster Icons")]
        public static void GenerateAll()
        {
            Directory.CreateDirectory(OutDir);
            var poseClip = ClassPortraitGenerator.FindPoseClip();
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                ClassPortraitGenerator.AddLight(scene, new Vector3(40f, -30f, 0f), 1.3f);
                ClassPortraitGenerator.AddLight(scene, new Vector3(20f, 150f, 0f), 0.6f);
                ClassPortraitGenerator.AddLight(scene, new Vector3(-30f, 20f, 0f), 0.5f);

                var camGO = new GameObject("MonsterIconCamera");
                SceneManager.MoveGameObjectToScene(camGO, scene);
                var cam = camGO.AddComponent<Camera>();
                cam.enabled = false;
                cam.scene = scene;
                cam.fieldOfView = 30f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.allowHDR = false;
                cam.nearClipPlane = 0.01f;

                int count = 0;
                foreach (var guid in AssetDatabase.FindAssets("t:MonsterData", new[] { MonsterDir }))
                {
                    var data = AssetDatabase.LoadAssetAtPath<MonsterData>(AssetDatabase.GUIDToAssetPath(guid));
                    if (data == null || string.IsNullOrEmpty(data.modelPath)) continue;
                    var sprite = Generate(data.name, data.modelPath, data.modelMaterialPath, 0f, !data.customRig ? poseClip : null, scene, cam);
                    if (sprite == null) continue;
                    data.icon = sprite;
                    EditorUtility.SetDirty(data);
                    count++;
                }
                foreach (var guid in AssetDatabase.FindAssets("t:BossData", new[] { BossDir }))
                {
                    var data = AssetDatabase.LoadAssetAtPath<BossData>(AssetDatabase.GUIDToAssetPath(guid));
                    if (data == null || string.IsNullOrEmpty(data.modelPath)) continue;
                    var sprite = Generate(data.name, data.modelPath, data.modelMaterialPath, data.modelYaw, null, scene, cam);
                    if (sprite == null) continue;
                    data.icon = sprite;
                    EditorUtility.SetDirty(data);
                    count++;
                }
                AssetDatabase.SaveAssets();
                Debug.Log($"[MonsterIcon] generated {count} icon(s) in {OutDir}");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        /// poseClip: 勇者と同じ骨組みの魔物（ゴブリン・ゴーレム）に構えの姿勢をとらせる。独自の骨組みの魔物は元の姿勢のまま。
        private static Sprite Generate(string assetName, string modelPath, string materialPath, float yaw, AnimationClip poseClip, Scene scene, Camera cam)
        {
            var prefab = Resources.Load<GameObject>(modelPath);
            if (prefab == null) { Debug.LogWarning($"[MonsterIcon] Resources/{modelPath} not found ({assetName})"); return null; }

            var model = (GameObject)Object.Instantiate(prefab);
            SceneManager.MoveGameObjectToScene(model, scene);
            try
            {
                model.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                ClassPortraitGenerator.ApplyMaterial(model, materialPath);
                if (poseClip != null)
                {
                    var boneFit = ModelBoneFit.Attach(model);
                    poseClip.SampleAnimation(model, poseClip.length * 0.3f);
                    if (boneFit != null) boneFit.Apply();
                }
                var points = ClassPortraitGenerator.CollectPoints(model);
                if (points.Count == 0) { Debug.LogWarning($"[MonsterIcon] no renderers on {modelPath}"); return null; }

                Vector3 min = points[0], max = points[0];
                foreach (var p in points) { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
                float height = max.y - min.y;
                float spread = Mathf.Max(max.x - min.x, max.z - min.z);
                if (!Framings.TryGetValue(assetName, out var framing))
                    framing = height >= spread * UprightRatio ? Framing.Bust : Framing.Whole;
                if (framing == Framing.Bust)
                {
                    float cutY = max.y - height * BustFraction;
                    ClassPortraitGenerator.Fit(cam, points.FindAll(p => p.y >= cutY), IconSize, IconSize, 1.05f, false, ViewDirection);
                }
                else ClassPortraitGenerator.Fit(cam, points, IconSize, IconSize, framing == Framing.Body ? 1.0f : 1.02f, framing == Framing.Whole, ViewDirection);

                var shot = ClassPortraitGenerator.RenderWithAlpha(cam, IconSize, IconSize);
                var icon = ClassPortraitGenerator.CircleIcon(shot, Backdrop, false);
                string path = $"{OutDir}/{assetName}_Icon.png";
                ClassPortraitGenerator.Save(icon, path);
                return ClassPortraitGenerator.ImportSprite(path);
            }
            finally
            {
                Object.DestroyImmediate(model);
            }
        }
    }
}
