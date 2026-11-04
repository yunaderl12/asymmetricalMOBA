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
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssets();
                Debug.Log($"[GroundDigBake] saved {positions.Count} tree position(s).");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
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
