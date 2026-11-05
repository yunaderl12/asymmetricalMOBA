using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Game.Core;

namespace Game.Editor
{
    /// 木は見た目だけでコライダーがないので、MainStage のメッシュから根元の位置を取り出して GroundDigData に保存する。
    /// フィールドのモデルを作り直したら実行し直す。
    public static class GroundDigBake
    {
        private const string ScenePath = "Assets/_Project/Scenes/MainStage.unity";
        private const string AssetPath = "Assets/_Project/Resources/GroundDigData.asset";
        private const string TreesName = "Field_Trees";
        private const float TrunkBottomHeight = 0.3f;
        private const float SameTreeDistance = 1.2f;

        private static readonly string[] TrunkMaterials = { "LP_Bark", "LP_WoodDark" };

        [MenuItem("Tools/LowPoly/Bake Dig Data")]
        public static void Bake()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                MeshFilter trees = null;
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                        if (filter.name == TreesName) trees = filter;
                if (trees == null)
                {
                    Debug.LogWarning("[GroundDigBake] " + TreesName + " not found");
                    return;
                }

                var positions = FindTrunks(trees);
                var data = AssetDatabase.LoadAssetAtPath<GroundDigData>(AssetPath);
                if (data == null)
                {
                    data = ScriptableObject.CreateInstance<GroundDigData>();
                    AssetDatabase.CreateAsset(data, AssetPath);
                }
                data.treePositions = positions;
                BakeBlocked(data, scene);
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssets();
                Debug.Log($"[GroundDigBake] saved {positions.Count} tree position(s).");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        // 水辺の近くと、山・橋・見えない壁の真下は削らない。
        private static void BakeBlocked(GroundDigData data, UnityEngine.SceneManagement.Scene scene)
        {
            const float waterMargin = 4f;
            Physics.SyncTransforms();
            float half = StageMap.Map.size * 0.5f;
            data.blockedOrigin = new Vector2(-half, -half);
            data.blockedCell = 2.5f;
            data.blockedColumns = data.blockedRows = Mathf.CeilToInt(StageMap.Map.size / data.blockedCell);
            data.blockedBits = new byte[(data.blockedColumns * data.blockedRows + 7) / 8];
            int count = 0;
            var physics = scene.GetPhysicsScene();
            var hits = new RaycastHit[8];
            for (int z = 0; z < data.blockedRows; z++)
            {
                for (int x = 0; x < data.blockedColumns; x++)
                {
                    float wx = data.blockedOrigin.x + (x + 0.5f) * data.blockedCell;
                    float wz = data.blockedOrigin.y + (z + 0.5f) * data.blockedCell;
                    bool blocked = StageMap.DistanceToWater(new Vector3(wx, 0f, wz)) < waterMargin;
                    if (!blocked)
                    {
                        int n = physics.Raycast(new Vector3(wx, 300f, wz), Vector3.down, hits, 310f);
                        for (int i = 0; i < n && !blocked; i++)
                        {
                            string name = hits[i].collider.name;
                            blocked = name == "Field_Mountains" || name == "Field_Bridges" || name == "Field_Barrier";
                        }
                    }
                    if (!blocked) continue;
                    int bit = x + z * data.blockedColumns;
                    data.blockedBits[bit >> 3] |= (byte)(1 << (bit & 7));
                    count++;
                }
            }
            Debug.Log($"[GroundDigBake] blocked {count} / {data.blockedColumns * data.blockedRows} cells.");
        }

        private static List<Vector2> FindTrunks(MeshFilter trees)
        {
            var mesh = trees.sharedMesh;
            var materials = trees.GetComponent<MeshRenderer>().sharedMaterials;
            var vertices = mesh.vertices;
            var result = new List<Vector2>();
            var counts = new List<int>();
            for (int s = 0; s < mesh.subMeshCount && s < materials.Length; s++)
            {
                if (materials[s] == null || System.Array.IndexOf(TrunkMaterials, materials[s].name) < 0) continue;
                foreach (int index in mesh.GetIndices(s))
                {
                    Vector3 world = trees.transform.TransformPoint(vertices[index]);
                    if (world.y > TrunkBottomHeight) continue;
                    AddToCluster(result, counts, new Vector2(world.x, world.z));
                }
            }
            return result;
        }

        private static void AddToCluster(List<Vector2> centers, List<int> counts, Vector2 point)
        {
            for (int i = 0; i < centers.Count; i++)
            {
                if ((centers[i] - point).sqrMagnitude > SameTreeDistance * SameTreeDistance) continue;
                centers[i] = (centers[i] * counts[i] + point) / (counts[i] + 1);
                counts[i]++;
                return;
            }
            centers.Add(point);
            counts.Add(1);
        }
    }
}
