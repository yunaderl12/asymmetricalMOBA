using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.AI.Navigation;
using Unity.AI.Navigation.Editor;

namespace Game.Editor
{
    /// Places the Blender low-poly placeholder models (Art/LowPoly/*.fbx, built by ArtSource/lowpoly/build_lowpoly.py)
    /// on MainStage and hides the old box renderers. Old colliders, cores and triggers are left untouched. The town and
    /// castle buildings get MeshColliders and the NavMesh is rebaked, so heroes and monsters go around them and enter
    /// through the gates (ground and glow meshes stay walk-through).
    public static class LowPolyStageVisuals
    {
        private const string RootName = "LowPolyVisuals";
        private const string ModelDir = "Assets/_Project/Art/LowPoly/";

        // Mesh objects inside the models that block movement.
        private static readonly string[] SolidMeshes = { "Town_Buildings", "Castle_Buildings" };

        // Old placeholder objects whose MeshRenderer is replaced by the models.
        private static readonly string[] HiddenRoots = { "Ground", "Rocks", "Walls" };
        private static readonly string[] HiddenUnderLandmarks = { "StartTown_Core", "DemonCastle_Core", "Pillar_NE", "Pillar_NW", "Pillar_SE", "Pillar_SW" };

        [MenuItem("Tools/LowPoly/Apply MainStage Visuals")]
        public static void Apply()
        {
            Remove(false);

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Apply LowPoly Visuals");

            float groundY = 0f;
            var ground = GameObject.Find("Ground");
            if (ground != null) groundY = ground.transform.position.y;

            Place("LP_Field", root.transform, new Vector3(0f, groundY, 0f), 100f, 60f);

            var town = GameObject.FindWithTag("StartTown");
            if (town != null) Place("LP_StartTown", root.transform, new Vector3(town.transform.position.x, groundY, town.transform.position.z), 10f, 10f);
            else Debug.LogWarning("[LowPoly] StartTown tag not found");

            var castle = GameObject.FindWithTag("DemonCastle");
            if (castle != null) Place("LP_DemonCastle", root.transform, new Vector3(castle.transform.position.x, groundY, castle.transform.position.z), 10f, 10f);
            else Debug.LogWarning("[LowPoly] DemonCastle tag not found");

            int hidden = 0;
            foreach (var r in OldRenderers())
            {
                if (!r.enabled) continue;
                Undo.RecordObject(r, "Apply LowPoly Visuals");
                r.enabled = false;
                hidden++;
            }

            AddBuildingColliders(root);
            EditorSceneManager.MarkSceneDirty(root.scene);
            Selection.activeGameObject = root;
            Debug.Log($"[LowPoly] placed models, hid {hidden} placeholder renderer(s). Save the scene to keep it.");
            RebakeNavMesh();
        }

        [MenuItem("Tools/LowPoly/Add Building Colliders And Rebake NavMesh")]
        public static void AddBuildingCollidersMenu()
        {
            var root = GameObject.Find(RootName);
            if (root == null)
            {
                Debug.LogWarning("[LowPoly] " + RootName + " not found; run Apply MainStage Visuals first");
                return;
            }
            AddBuildingColliders(root);
            EditorSceneManager.MarkSceneDirty(root.scene);
            RebakeNavMesh();
        }

        private static void AddBuildingColliders(GameObject root)
        {
            int added = 0;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (System.Array.IndexOf(SolidMeshes, mf.gameObject.name) < 0) continue;
                var mc = mf.GetComponent<MeshCollider>();
                if (mc == null)
                {
                    mc = Undo.AddComponent<MeshCollider>(mf.gameObject);
                    added++;
                }
                mc.sharedMesh = mf.sharedMesh;
            }
            Debug.Log($"[LowPoly] building colliders: added {added}");
        }

        /// The surfaces collect physics colliders, so the new building colliders cut the walkable area.
        /// Baking runs in the background and writes the scene's NavMesh asset when it finishes.
        private static void RebakeNavMesh()
        {
            var surfaces = Object.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None);
            if (surfaces.Length == 0)
            {
                Debug.LogWarning("[LowPoly] no NavMeshSurface in the scene; NavMesh not rebaked");
                return;
            }
            NavMeshAssetManager.instance.StartBakingSurfaces(surfaces);
            Debug.Log($"[LowPoly] rebaking {surfaces.Length} NavMesh surface(s)");
        }

        [MenuItem("Tools/LowPoly/Remove MainStage Visuals")]
        public static void RemoveMenu()
        {
            Remove(true);
        }

        private static void Remove(bool restoreRenderers)
        {
            var existing = GameObject.Find(RootName);
            if (existing != null) Undo.DestroyObjectImmediate(existing);
            if (!restoreRenderers) return;

            int restored = 0;
            foreach (var r in OldRenderers())
            {
                if (r.enabled) continue;
                Undo.RecordObject(r, "Remove LowPoly Visuals");
                r.enabled = true;
                restored++;
            }
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[LowPoly] removed models, restored {restored} placeholder renderer(s).");
        }

        private static IEnumerable<MeshRenderer> OldRenderers()
        {
            foreach (var name in HiddenRoots)
            {
                var go = GameObject.Find(name);
                if (go == null) continue;
                foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true)) yield return r;
            }
            foreach (var tag in new[] { "StartTown", "DemonCastle" })
            {
                var landmark = GameObject.FindWithTag(tag);
                if (landmark == null) continue;
                foreach (var r in landmark.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (System.Array.IndexOf(HiddenUnderLandmarks, r.gameObject.name) >= 0) yield return r;
                }
            }
        }

        /// Instantiates the model and fixes rotation / scale / mirroring from its Marker_PlusX / Marker_PlusZ
        /// empties, so the FBX axis conversion does not matter.
        private static void Place(string model, Transform parent, Vector3 position, float markerX, float markerZ)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelDir + model + ".fbx");
            if (asset == null)
            {
                Debug.LogWarning($"[LowPoly] {ModelDir}{model}.fbx not found");
                return;
            }
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            var t = inst.transform;
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;

            var mx = FindDeep(t, "Marker_PlusX");
            var mz = FindDeep(t, "Marker_PlusZ");
            if (mx != null && mz != null)
            {
                Vector3 vx = MarkerPos(mx) - t.position;
                float scale = vx.magnitude > 0.001f ? markerX / vx.magnitude : 1f;
                float yaw = Mathf.Atan2(vx.z, vx.x) * Mathf.Rad2Deg;
                t.localRotation = Quaternion.Euler(0f, yaw, 0f);
                t.localScale = Vector3.one * scale;
                Vector3 vz = MarkerPos(mz) - t.position;
                if (vz.z < 0f) t.localScale = new Vector3(scale, scale, -scale);
                if (Mathf.Abs(vz.y) > markerZ * 0.5f) Debug.LogWarning($"[LowPoly] {model}: unexpected up axis (Marker_PlusZ at {vz})");
                mx.gameObject.SetActive(false);
                mz.gameObject.SetActive(false);
            }
            else Debug.LogWarning($"[LowPoly] {model}: markers missing, placed as imported");

            t.position = position;
        }

        /// Markers are small meshes whose geometry (not pivot) sits at the marker point.
        private static Vector3 MarkerPos(Transform marker)
        {
            var r = marker.GetComponent<Renderer>();
            return r != null ? r.bounds.center : marker.position;
        }

        /// Prefix match: Blender suffixes duplicate names (Marker_PlusX.001) when several models are built in one session.
        private static Transform FindDeep(Transform t, string name)
        {
            if (t.name.StartsWith(name)) return t;
            foreach (Transform c in t)
            {
                var r = FindDeep(c, name);
                if (r != null) return r;
            }
            return null;
        }
    }
}
