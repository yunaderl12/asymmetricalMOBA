using System.Collections.Generic;
using Game.Core;
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
        // Field_Ground は起伏と窪んだ川があるので、地形の当たり判定はこのメッシュで取る（古い平らな Ground の当たり判定は切る）。
        private static readonly string[] SolidMeshes = { "Field_Ground", "Town_Buildings", "Castle_Buildings", "Field_Mountains", "Field_Bridges", "Field_Barrier", "Field_TreeColliders", "Field_MountainFill" };
        // 当たり判定だけで、見た目に出さないメッシュ。
        private static readonly string[] HiddenMeshes = { "Field_Barrier", "Field_TreeColliders", "Field_MountainFill" };

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

        /// Resources/StageMap.json のとおりにシーンの物を置き直す。地形モデルを書き出したあとに実行する。
        [MenuItem("Tools/LowPoly/Apply Stage Layout And Rebake NavMesh")]
        public static void ApplyStageLayout()
        {
            var root = GameObject.Find(RootName);
            if (root == null)
            {
                Debug.LogWarning("[LowPoly] " + RootName + " not found; run Apply MainStage Visuals first");
                return;
            }
            AddBuildingColliders(root);
            Physics.SyncTransforms();

            var map = StageMap.Map;
            var scene = root.scene;
            var byName = new System.Collections.Generic.Dictionary<string, Transform>();
            foreach (var r in scene.GetRootGameObjects())
                foreach (var t in r.GetComponentsInChildren<Transform>(true))
                    if (!byName.ContainsKey(t.name)) byName[t.name] = t;

            // 門（モデルの +X / -X）が地図の中央を向くように、まちと城を45度回す。
            var yaw = Quaternion.Euler(0f, 45f, 0f);
            var town = new Vector3(map.townX, 0f, map.townZ);
            var castle = new Vector3(map.castleX, map.castleHeight, map.castleZ);
            Set(byName, "StartTown", town + Vector3.up, yaw);
            // 配置済みのモデルは FBX の軸の都合で180度回っている。
            var modelYaw = yaw * Quaternion.Euler(0f, 180f, 0f);
            Set(byName, "LP_StartTown", town, modelYaw);
            Set(byName, "ArcherTower_North", town + yaw * new Vector3(15.6f, 0f, 8.5f), yaw);
            Set(byName, "ArcherTower_South", town + yaw * new Vector3(15.6f, 0f, -8.5f), yaw);
            Set(byName, "Hero", town + Vector3.up + yaw * new Vector3(10f, 0f, 0f), Quaternion.identity);
            Set(byName, "DemonCastle", castle + Vector3.up, yaw);
            Set(byName, "LP_DemonCastle", castle, modelYaw);
            Set(byName, "DemonKing", castle + Vector3.up + yaw * new Vector3(2.4f, 0f, 2.7f), Quaternion.identity);

            foreach (var rock in map.rocks)
            {
                Transform t;
                if (byName.TryGetValue(rock.name, out t)) t.position = new Vector3(rock.x, t.position.y, rock.z);
            }

            foreach (var entry in map.spawners)
            {
                Transform t;
                if (!byName.TryGetValue(entry.name, out t)) continue;
                t.position = new Vector3(entry.x, SurfaceY(entry.x, entry.z), entry.z);
                var spawner = t.GetComponent<Game.Monster.Spawner>();
                var so = new SerializedObject(spawner);
                var list = so.FindProperty("waypointsToStartTown");
                list.arraySize = entry.waypoints.Length / 2;
                for (int i = 0; i < list.arraySize; i++)
                {
                    float x = entry.waypoints[i * 2], z = entry.waypoints[i * 2 + 1];
                    list.GetArrayElementAtIndex(i).vector3Value = new Vector3(x, SurfaceY(x, z), z);
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            AddBiomeSpawner(byName);
            AddBossSpawner(byName);

            Transform ground;
            if (byName.TryGetValue("Ground", out ground))
            {
                ground.localScale = new Vector3(map.size / 10f, 1f, map.size / 10f);
                var flat = ground.GetComponent<Collider>();
                if (flat != null) flat.enabled = !StageGround.Available;
            }
            Transform walls;
            if (byName.TryGetValue("Walls", out walls)) Object.DestroyImmediate(walls.gameObject);
            Transform bird;
            if (byName.TryGetValue("BirdsEyeCamera", out bird))
            {
                bird.position = new Vector3(0f, 150f, 0f);
                bird.GetComponent<Camera>().orthographicSize = map.size * 0.5f;
            }

            foreach (var r in scene.GetRootGameObjects())
            {
                foreach (var c in r.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (c == null) continue;
                    string type = c.GetType().Name;
                    if (type != "BotSpawner" && type != "MatchItemGimmick") continue;
                    var so = new SerializedObject(c);
                    if (type == "BotSpawner")
                    {
                        var heroes = so.FindProperty("heroPositions");
                        var locals = new[] { new Vector3(8f, 0f, 2f), new Vector3(8f, 0f, -2f), new Vector3(12f, 0f, 2f), new Vector3(12f, 0f, -2f) };
                        for (int i = 0; i < heroes.arraySize && i < locals.Length; i++)
                            heroes.GetArrayElementAtIndex(i).vector3Value = town + Vector3.up + yaw * locals[i];
                        so.FindProperty("demonKingPosition").vector3Value = castle + Vector3.up + yaw * new Vector3(0f, 0f, 2.7f);
                    }
                    else
                    {
                        float reach = map.size * 0.5f - 10f;
                        so.FindProperty("mapXRange").vector2Value = new Vector2(-reach, reach);
                        so.FindProperty("mapZRange").vector2Value = new Vector2(-reach, reach);
                    }
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Physics.SyncTransforms();
            RebakeNavMesh();
            Debug.Log("[LowPoly] stage layout applied. Save the scene after the bake finishes.");
        }

        private const string FieldModelPath = ModelDir + "LP_Field.fbx";
        private const string RiverMaterialDir = "Assets/_Project/Art/LowPoly/Materials/";
        private static readonly string[] RiverSourceMaterials = { "LP_WaterShallow", "LP_WaterShallowB" };
        private static readonly Color[] RiverColors = { new Color(0.18f, 0.62f, 0.86f, 0.5f), new Color(0.14f, 0.56f, 0.82f, 0.55f) };

        /// 地形モデルの読み込み設定。浅い川の水を半透明の材質に差し替え、吊り橋のロープを動かせるようにメッシュを読めるようにする。
        [MenuItem("Tools/LowPoly/Setup Field Import (River Water, Readable)")]
        public static void SetupFieldImport()
        {
            var importer = AssetImporter.GetAtPath(FieldModelPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogWarning("[LowPoly] " + FieldModelPath + " not found");
                return;
            }
            if (!AssetDatabase.IsValidFolder(RiverMaterialDir.TrimEnd('/'))) AssetDatabase.CreateFolder("Assets/_Project/Art/LowPoly", "Materials");
            for (int i = 0; i < RiverSourceMaterials.Length; i++)
            {
                string path = RiverMaterialDir + RiverSourceMaterials[i].Replace("LP_WaterShallow", "LP_RiverWater") + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    AssetDatabase.CreateAsset(material, path);
                }
                MakeRiverWater(material, RiverColors[i]);
                EditorUtility.SetDirty(material);
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), RiverSourceMaterials[i]), material);
            }
            importer.isReadable = true;
            importer.SaveAndReimport();
            AssetDatabase.SaveAssets();
            Debug.Log("[LowPoly] field import set up (river water remapped, mesh readable)");
        }

        // 透ける水。水面に空が映るようになめらかにし、影は落とさない（川底が暗くならないように）。
        private static void MakeRiverWater(Material m, Color color)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_AlphaClip", 0f);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", 0.72f);
            m.SetFloat("_Metallic", 0f);
            if (m.HasProperty("_CastShadows")) m.SetFloat("_CastShadows", 0f);
            m.SetShaderPassEnabled("ShadowCaster", false);
            m.SetShaderPassEnabled("DepthOnly", false);
        }

        // 山の広場のドラゴン、砂漠の広場のスコーピオン、森の広場のウェンディゴを出す空のオブジェクト。
        public static void AddBossSpawner(System.Collections.Generic.Dictionary<string, Transform> byName)
        {
            Transform t;
            var go = byName.TryGetValue("BossSpawner", out t) ? t.gameObject : new GameObject("BossSpawner");
            var spawner = go.GetComponent<Game.Monster.BossSpawner>();
            if (spawner == null) spawner = go.AddComponent<Game.Monster.BossSpawner>();
            var so = new SerializedObject(spawner);
            so.FindProperty("dragon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Game.Monster.DragonData>("Assets/_Project/ScriptableObjects/Bosses/Dragon.asset");
            so.FindProperty("scorpion").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Game.Monster.ScorpionData>("Assets/_Project/ScriptableObjects/Bosses/Scorpion.asset");
            so.FindProperty("wendigo").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Game.Monster.WendigoData>("Assets/_Project/ScriptableObjects/Bosses/Wendigo.asset");
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // 森・山・砂漠の自然湧きを受け持つ空のオブジェクト。
        private static void AddBiomeSpawner(System.Collections.Generic.Dictionary<string, Transform> byName)
        {
            Transform t;
            var go = byName.TryGetValue("BiomeSpawner", out t) ? t.gameObject : new GameObject("BiomeSpawner");
            var spawner = go.GetComponent<Game.Monster.BiomeSpawner>();
            if (spawner == null) spawner = go.AddComponent<Game.Monster.BiomeSpawner>();
            var so = new SerializedObject(spawner);
            so.FindProperty("table").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Game.Monster.BiomeSpawnData>("Assets/_Project/ScriptableObjects/Biomes/BiomeSpawnTable.asset");
            so.FindProperty("monsterPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Monster.prefab");
            so.ApplyModifiedPropertiesWithoutUndo();

            var guards = go.GetComponent<Game.Monster.BridgeGuardianSpawner>();
            if (guards == null) guards = go.AddComponent<Game.Monster.BridgeGuardianSpawner>();
            var guardSo = new SerializedObject(guards);
            guardSo.FindProperty("defaultGuardian").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Game.Monster.MonsterData>("Assets/_Project/ScriptableObjects/Monsters/Golem.asset");
            guardSo.FindProperty("monsterPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Monster.prefab");
            guardSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Set(System.Collections.Generic.Dictionary<string, Transform> byName, string name, Vector3 position, Quaternion rotation)
        {
            Transform t;
            if (!byName.TryGetValue(name, out t))
            {
                Debug.LogWarning("[LowPoly] " + name + " not found");
                return;
            }
            t.SetPositionAndRotation(position, rotation);
        }

        // 山の上か平らな地面か。木や岩は数えない。
        private static float SurfaceY(float x, float z)
        {
            float best = 0f;
            foreach (var hit in Physics.RaycastAll(new Vector3(x, 300f, z), Vector3.down, 320f))
                if (hit.collider.name == "Field_Mountains") best = Mathf.Max(best, hit.point.y);
            return best;
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
                if (System.Array.IndexOf(HiddenMeshes, mf.gameObject.name) >= 0)
                {
                    var hidden = mf.GetComponent<MeshRenderer>();
                    if (hidden != null) hidden.enabled = false;
                }
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
