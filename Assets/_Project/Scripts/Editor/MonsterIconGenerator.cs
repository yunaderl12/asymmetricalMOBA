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
    /// 顔のアップ: 頭の骨（FaceBones）に付いた頂点に寄せる（頭の骨がなければ体全体）。魔物の形のまわりに白いふちを付ける。
    /// 描画の手順は ClassPortraitGenerator（勇者の顔アイコン）と同じ。絵を描き直したら icon を差し替えればよい。
    public static class MonsterIconGenerator
    {
        private const string MonsterDir = "Assets/_Project/ScriptableObjects/Monsters";
        private const string BossDir = "Assets/_Project/ScriptableObjects/Bosses";
        private const string OutDir = "Assets/_Project/Art/Characters/MonsterIcons";
        private const int IconSize = 256;
        private const float FacePadding = 1.3f; // 顔のまわりの余白（大きいほど引く）
        private const float FacePercentile = 0.85f; // 顔の頂点のうち、絵に収める割合（はみ出した角や毛、重みのにじみは切る）
        private const float OutlinePixels = 9f; // 白いふちの太さ（256px の絵で）
        private static readonly Color Backdrop = new Color(0.55f, 0.36f, 0.62f); // 魔王ロビーのフェルトに合う紫
        private static readonly Vector3 ViewDirection = new Vector3(0.4f, 0.15f, 1f);

        /// 顔として写す骨。上から順に探し、最初に見つかったものを使う。
        /// 頭の骨がないモデル（スコーピオン）は体全体を写す。
        private static readonly string[][] FaceBones =
        {
            new[] { "Head" },
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

                var face = FacePoints(model);
                if (face.Count > 0) FrameFace(cam, face);
                else
                {
                    var points = ClassPortraitGenerator.CollectPoints(model);
                    if (points.Count == 0) { Debug.LogWarning($"[MonsterIcon] no renderers on {modelPath}"); return null; }
                    ClassPortraitGenerator.Fit(cam, points, IconSize, IconSize, 1.05f, true, ViewDirection);
                }

                var shot = ClassPortraitGenerator.RenderWithAlpha(cam, IconSize, IconSize);
                var outlined = Outline(shot, OutlinePixels, Color.white);
                var icon = ClassPortraitGenerator.CircleIcon(outlined, Backdrop, false);
                string path = $"{OutDir}/{assetName}_Icon.png";
                ClassPortraitGenerator.Save(icon, path);
                return ClassPortraitGenerator.ImportSprite(path);
            }
            finally
            {
                Object.DestroyImmediate(model);
            }
        }

        /// 顔の骨（FaceBones）に主に付いている頂点の位置（姿勢をとった後）。CollectPoints と同じ座標の求め方。
        private static List<Vector3> FacePoints(GameObject model)
        {
            var points = new List<Vector3>();
            var renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var names in FaceBones)
            {
                foreach (var smr in renderers)
                {
                    if (smr.sharedMesh == null) continue;
                    var bones = smr.bones;
                    var face = new bool[bones.Length];
                    bool any = false;
                    for (int i = 0; i < bones.Length; i++)
                        if (bones[i] != null && System.Array.IndexOf(names, bones[i].name) >= 0) face[i] = any = true;
                    if (!any) continue;

                    var weights = smr.sharedMesh.boneWeights;
                    var mesh = new Mesh();
                    smr.BakeMesh(mesh);
                    var m = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
                    var vertices = mesh.vertices;
                    for (int v = 0; v < vertices.Length && v < weights.Length; v++)
                    {
                        var w = weights[v];
                        float share = (face[w.boneIndex0] ? w.weight0 : 0f) + (face[w.boneIndex1] ? w.weight1 : 0f)
                                      + (face[w.boneIndex2] ? w.weight2 : 0f) + (face[w.boneIndex3] ? w.weight3 : 0f);
                        if (share >= 0.5f) points.Add(m.MultiplyPoint3x4(vertices[v]));
                    }
                    Object.DestroyImmediate(mesh);
                }
                if (points.Count > 0) return points;
            }
            return points;
        }

        /// 顔の頂点の真ん中（軸ごとの中央値）を中心に、FacePercentile の頂点が入る半径で寄せる。
        /// 端の頂点で決めないのは、翼などに頭の骨の重みが少しにじんでいるモデル（ドラゴン）や、長い毛・角があるモデルのため。
        private static void FrameFace(Camera cam, List<Vector3> points)
        {
            var xs = new List<float>(points.Count);
            var ys = new List<float>(points.Count);
            var zs = new List<float>(points.Count);
            foreach (var p in points) { xs.Add(p.x); ys.Add(p.y); zs.Add(p.z); }
            xs.Sort(); ys.Sort(); zs.Sort();
            int mid = points.Count / 2;
            var center = new Vector3(xs[mid], ys[mid], zs[mid]);

            var dists = new List<float>(points.Count);
            foreach (var p in points) dists.Add(Vector3.Distance(p, center));
            dists.Sort();
            float radius = Mathf.Max(0.01f, dists[Mathf.Min(dists.Count - 1, Mathf.FloorToInt(dists.Count * FacePercentile))]);

            var dir = ViewDirection.normalized;
            float distance = radius * FacePadding / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            cam.transform.SetPositionAndRotation(center + dir * distance, Quaternion.LookRotation(-dir, Vector3.up));
        }

        /// 絵の形（アルファ）のまわりに色のふちを付ける。ふちは形の後ろに敷く。
        private static Texture2D Outline(Texture2D src, float width, Color color)
        {
            int w = src.width, h = src.height;
            var px = src.GetPixels();
            var alpha = new float[px.Length];
            for (int i = 0; i < px.Length; i++) alpha[i] = px[i].a;

            int r = Mathf.CeilToInt(width);
            var offsets = new List<Vector3Int>(); // x, y と、ふちの強さを 1000 倍した値
            for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                float k = Mathf.Clamp01(width + 0.5f - Mathf.Sqrt(dx * dx + dy * dy));
                if (k > 0f) offsets.Add(new Vector3Int(dx, dy, Mathf.RoundToInt(k * 1000f)));
            }

            var outPx = new Color[px.Length];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                float rim = 0f;
                foreach (var o in offsets)
                {
                    int sx = x + o.x, sy = y + o.y;
                    if (sx < 0 || sy < 0 || sx >= w || sy >= h) continue;
                    rim = Mathf.Max(rim, alpha[sy * w + sx] * o.z * 0.001f);
                    if (rim >= 1f) break;
                }
                var c = px[i];
                var back = new Color(color.r, color.g, color.b, rim);
                // c を back の上に重ねる（どちらもまっすぐなアルファ）。
                float a = c.a + back.a * (1f - c.a);
                outPx[i] = a > 0.0001f
                    ? new Color((c.r * c.a + back.r * back.a * (1f - c.a)) / a, (c.g * c.a + back.g * back.a * (1f - c.a)) / a,
                        (c.b * c.a + back.b * back.a * (1f - c.a)) / a, a)
                    : Color.clear;
            }
            Object.DestroyImmediate(src);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels(outPx);
            tex.Apply();
            return tex;
        }
    }
}
