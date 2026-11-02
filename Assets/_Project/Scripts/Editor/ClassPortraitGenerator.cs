using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Game.Hero;

namespace Game.Editor
{
    /// Renders placeholder lobby images for every HeroClassData from its 3D model (HeroClassData.modelPath):
    /// a full-body portrait (big image in the lobby settings panel) and a round bust icon (class list).
    /// Writes Art/Characters/Portraits/<Class>_Portrait.png / <Class>_Icon.png and assigns them to the class asset.
    /// Swap the sprites on the class asset for real illustrations later.
    public static class ClassPortraitGenerator
    {
        private const string ClassDir = "Assets/_Project/ScriptableObjects/Classes";
        private const string OutDir = "Assets/_Project/Art/Characters/Portraits";
        private const string PoseClipPath = "Knight/knight_alert";
        private const int PortraitWidth = 512, PortraitHeight = 768, IconSize = 256;
        private const float FieldOfView = 30f;
        private const float BustFraction = 0.68f; // top share of the body height used for the icon (chibi heads are ~half the body)
        private static readonly Vector3 ViewDirection = new Vector3(0.35f, 0.15f, 1f);

        [MenuItem("Tools/Characters/Generate Class Portraits")]
        public static void GenerateAll()
        {
            Directory.CreateDirectory(OutDir);
            var poseClip = FindPoseClip();
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                AddLight(scene, new Vector3(40f, -30f, 0f), 1.3f);
                AddLight(scene, new Vector3(20f, 150f, 0f), 0.6f);
                AddLight(scene, new Vector3(-30f, 20f, 0f), 0.5f);

                var camGO = new GameObject("PortraitCamera");
                SceneManager.MoveGameObjectToScene(camGO, scene);
                var cam = camGO.AddComponent<Camera>();
                cam.enabled = false;
                cam.scene = scene;
                cam.fieldOfView = FieldOfView;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.allowHDR = false;
                cam.nearClipPlane = 0.01f;

                int count = 0;
                foreach (var guid in AssetDatabase.FindAssets("t:HeroClassData", new[] { ClassDir }))
                {
                    var cls = AssetDatabase.LoadAssetAtPath<HeroClassData>(AssetDatabase.GUIDToAssetPath(guid));
                    if (cls == null || string.IsNullOrEmpty(cls.modelPath)) continue;
                    if (Generate(cls, scene, cam, poseClip)) count++;
                }
                AssetDatabase.SaveAssets();
                Debug.Log($"[ClassPortrait] generated images for {count} class(es) in {OutDir}");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static bool Generate(HeroClassData cls, Scene scene, Camera cam, AnimationClip poseClip)
        {
            var prefab = Resources.Load<GameObject>(cls.modelPath);
            if (prefab == null) { Debug.LogWarning($"[ClassPortrait] Resources/{cls.modelPath} not found ({cls.className})"); return false; }

            var model = (GameObject)Object.Instantiate(prefab);
            SceneManager.MoveGameObjectToScene(model, scene);
            try
            {
                ApplyMaterial(model, cls.modelMaterialPath);
                var boneFit = ModelBoneFit.Attach(model);
                var floatingItem = FloatingHeldItem.AttachIfPresent(model);
                if (poseClip != null) poseClip.SampleAnimation(model, poseClip.length * 0.3f);
                if (boneFit != null) boneFit.Apply();
                if (floatingItem != null) floatingItem.Place();
                var points = CollectPoints(model);
                if (points.Count == 0) { Debug.LogWarning($"[ClassPortrait] no renderers on {cls.modelPath}"); return false; }

                // Full body, transparent background.
                Fit(cam, points, PortraitWidth, PortraitHeight, 1.06f);
                var portrait = RenderWithAlpha(cam, PortraitWidth, PortraitHeight);
                string portraitPath = $"{OutDir}/{cls.className}_Portrait.png";
                Save(portrait, portraitPath);

                // Head and shoulders (top part of the body) inside a class-coloured circle.
                float minY = float.PositiveInfinity, maxY = float.NegativeInfinity;
                foreach (var p in points) { minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y); }
                float cutY = maxY - (maxY - minY) * BustFraction;
                var bustPoints = points.FindAll(p => p.y >= cutY);
                // Height-only fit centred on the body, so a staff sticking out sideways is simply cropped.
                Fit(cam, bustPoints, IconSize, IconSize, 1.05f, false);
                var bust = RenderWithAlpha(cam, IconSize, IconSize);
                var icon = CircleIcon(bust, cls.avatarColor);
                string iconPath = $"{OutDir}/{cls.className}_Icon.png";
                Save(icon, iconPath);

                cls.portrait = ImportSprite(portraitPath);
                cls.icon = ImportSprite(iconPath);
                EditorUtility.SetDirty(cls);
                return true;
            }
            finally
            {
                Object.DestroyImmediate(model);
            }
        }

        private static void AddLight(Scene scene, Vector3 euler, float intensity)
        {
            var go = new GameObject("PortraitLight");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.eulerAngles = euler;
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = intensity;
        }

        private static AnimationClip FindPoseClip()
        {
            AnimationClip best = null;
            foreach (var c in Resources.LoadAll<AnimationClip>(PoseClipPath))
            {
                if (c == null || c.name.StartsWith("__preview__")) continue;
                if (best == null || c.length > best.length) best = c;
            }
            return best;
        }

        private static void ApplyMaterial(GameObject model, string materialPath)
        {
            if (string.IsNullOrEmpty(materialPath)) return;
            var mat = Resources.Load<Material>(materialPath);
            if (mat == null) return;
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
            }
        }

        // World-space vertices of the posed model. SkinnedMeshRenderer.bounds is not updated
        // in edit mode, so the skinned meshes are baked.
        private static List<Vector3> CollectPoints(GameObject model)
        {
            var points = new List<Vector3>();
            foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = new Mesh();
                smr.BakeMesh(mesh);
                var m = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
                foreach (var v in mesh.vertices) points.Add(m.MultiplyPoint3x4(v));
                Object.DestroyImmediate(mesh);
            }
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var m = mf.transform.localToWorldMatrix;
                foreach (var v in mf.sharedMesh.vertices) points.Add(m.MultiplyPoint3x4(v));
            }
            return points;
        }

        // Places the camera on ViewDirection so every point is inside the frame (perspective-exact).
        // fitWidth = false: only the height has to fit, and the frame is centred on the median x
        // (the body) instead of the extents, so props sticking out sideways are cropped.
        private static void Fit(Camera cam, List<Vector3> points, int pxW, int pxH, float padding, bool fitWidth = true)
        {
            var dir = ViewDirection.normalized;
            var rot = Quaternion.LookRotation(-dir, Vector3.up);
            var right = rot * Vector3.right;
            var up = rot * Vector3.up;

            var center = Vector3.zero;
            foreach (var p in points) center += p;
            center /= points.Count;
            var xs = new List<float>(points.Count);
            float xMin = float.PositiveInfinity, xMax = float.NegativeInfinity, yMin = float.PositiveInfinity, yMax = float.NegativeInfinity;
            foreach (var p in points)
            {
                var q = p - center;
                float x = Vector3.Dot(q, right), y = Vector3.Dot(q, up);
                xs.Add(x);
                xMin = Mathf.Min(xMin, x); xMax = Mathf.Max(xMax, x);
                yMin = Mathf.Min(yMin, y); yMax = Mathf.Max(yMax, y);
            }
            xs.Sort();
            float xCenter = fitWidth ? (xMin + xMax) * 0.5f : xs[xs.Count / 2];
            center += right * xCenter + up * ((yMin + yMax) * 0.5f);

            float tanV = Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad) / padding;
            float tanH = tanV * pxW / pxH;
            float distance = 0f;
            foreach (var p in points)
            {
                var q = p - center;
                float toward = Vector3.Dot(q, dir); // how far the point sits toward the camera
                distance = Mathf.Max(distance, toward + Mathf.Abs(Vector3.Dot(q, up)) / tanV);
                if (fitWidth) distance = Mathf.Max(distance, toward + Mathf.Abs(Vector3.Dot(q, right)) / tanH);
            }
            cam.transform.SetPositionAndRotation(center + dir * distance, rot);
        }

        // Renders on black and on white and recovers alpha from the difference,
        // so it works whether or not the pipeline keeps the alpha channel.
        private static Texture2D RenderWithAlpha(Camera cam, int w, int h)
        {
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            cam.targetTexture = rt;
            cam.backgroundColor = Color.black;
            cam.Render();
            var onBlack = ReadPixels(rt, w, h);
            cam.backgroundColor = Color.white;
            cam.Render();
            var onWhite = ReadPixels(rt, w, h);
            cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);

            var outPx = new Color[onBlack.Length];
            for (int i = 0; i < outPx.Length; i++)
            {
                var b = onBlack[i];
                var wt = onWhite[i];
                float a = Mathf.Clamp01(1f - ((wt.r - b.r) + (wt.g - b.g) + (wt.b - b.b)) / 3f);
                outPx[i] = a > 0.001f ? new Color(Mathf.Clamp01(b.r / a), Mathf.Clamp01(b.g / a), Mathf.Clamp01(b.b / a), a) : Color.clear;
            }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels(outPx);
            tex.Apply();
            return tex;
        }

        private static Color[] ReadPixels(RenderTexture rt, int w, int h)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            var px = tex.GetPixels();
            Object.DestroyImmediate(tex);
            return px;
        }

        private static Texture2D CircleIcon(Texture2D bust, Color classColor)
        {
            int size = bust.width;
            var src = bust.GetPixels();
            var outPx = new Color[src.Length];
            var fill = Color.Lerp(classColor, Color.black, 0.45f);
            var ring = Color.Lerp(classColor, Color.white, 0.25f);
            float r = size * 0.5f;
            float ringWidth = size * 0.045f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int i = y * size + x;
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                float edge = Mathf.Clamp01(r - d); // 1px anti-aliased edge
                if (edge <= 0f) { outPx[i] = Color.clear; continue; }
                var c = Color.Lerp(fill, src[i], src[i].a);
                if (d > r - ringWidth) c = ring;
                c.a = edge;
                outPx[i] = c;
            }
            Object.DestroyImmediate(bust);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.SetPixels(outPx);
            tex.Apply();
            return tex;
        }

        private static void Save(Texture2D tex, string path)
        {
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        private static Sprite ImportSprite(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
