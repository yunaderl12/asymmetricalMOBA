using System.Collections.Generic;
using UnityEngine;

namespace Game.Skill
{
    /// 被弾した敵の白フラッシュと炎の膜。敵のメッシュを複製して上に重ねる。
    /// 元の敵が倒れて消えても、最後の形のまま膜が残る。
    [DefaultExecutionOrder(100)] // ImpactPunch と構えの反映後の形を写す
    public class FireHitEffect : MonoBehaviour
    {
        private const int HitFlashFrames = 2;
        private const float HitFilmSeconds = 0.5f;
        private const float FlashExpand = 0f;
        private const float FilmExpandStart = 0.015f;
        private const float FilmExpandEnd = 0.07f;

        private class Ghost
        {
            public Renderer Source;
            public SkinnedMeshRenderer Skin;
            public Mesh Baked;
            public Transform Transform;
            public MeshRenderer Renderer;
            public int[] WeldGroups;
            public Vector3[] GroupNormals;
        }

        private static readonly List<Renderer> SearchBuffer = new List<Renderer>();
        private static readonly List<Vector3> NormalBuffer = new List<Vector3>();

        private readonly List<Ghost> ghosts = new List<Ghost>();
        private MaterialPropertyBlock block;
        private int flashFrames;
        private float filmSeconds;
        private int startFrame;
        private float filmStart = -1f;

        /// 演出の対象になる見た目のレンダラー（体力バーやリング、パーティクルは除く）。
        public static void CollectVisualRenderers(GameObject target, List<Renderer> results)
        {
            results.Clear();
            target.GetComponentsInChildren(false, SearchBuffer);
            foreach (var r in SearchBuffer)
            {
                if (!r.enabled) continue;
                if (r is SkinnedMeshRenderer) results.Add(r);
                else if (r is MeshRenderer && r.GetComponent<MeshFilter>() != null) results.Add(r);
            }
        }

        public static void Play(GameObject target)
        {
            Begin(target, HitFlashFrames, HitFilmSeconds);
        }

        /// 白フラッシュなしで、炎の膜だけを指定秒数まとわせる（魔物の出現演出用）。
        public static void PlayBurn(GameObject target, float seconds)
        {
            Begin(target, 0, seconds);
        }

        private static void Begin(GameObject target, int flashFrames, float filmSeconds)
        {
            var renderers = new List<Renderer>();
            CollectVisualRenderers(target, renderers);
            if (renderers.Count == 0) return;

            var go = new GameObject("FireHitEffect");
            var effect = go.AddComponent<FireHitEffect>();
            effect.block = new MaterialPropertyBlock();
            effect.flashFrames = flashFrames;
            effect.filmSeconds = filmSeconds;
            effect.startFrame = Time.frameCount;
            foreach (var r in renderers) effect.AddGhost(r);
            effect.RefreshGhosts();
            effect.ApplyPhase();
        }

        private void AddGhost(Renderer source)
        {
            var ghost = new Ghost { Source = source, Skin = source as SkinnedMeshRenderer };
            Mesh mesh;
            int subMeshes;
            if (ghost.Skin != null)
            {
                ghost.Baked = new Mesh { name = "FireHitBaked" };
                mesh = ghost.Baked;
                subMeshes = ghost.Skin.sharedMesh != null ? ghost.Skin.sharedMesh.subMeshCount : 1;
            }
            else
            {
                mesh = source.GetComponent<MeshFilter>().sharedMesh;
                subMeshes = mesh != null ? mesh.subMeshCount : 1;
            }
            ghost.Renderer = FireballVfx.NewMeshObject("Ghost", transform, mesh, FireballVfx.Shell);
            var materials = new Material[Mathf.Max(1, subMeshes)];
            for (int i = 0; i < materials.Length; i++) materials[i] = FireballVfx.Shell;
            ghost.Renderer.sharedMaterials = materials;
            ghost.Transform = ghost.Renderer.transform;
            ghosts.Add(ghost);
        }

        private void RefreshGhosts()
        {
            foreach (var g in ghosts)
            {
                if (g.Source == null) continue;
                var t = g.Source.transform;
                if (g.Skin != null)
                {
                    // ImpactPunch の伸縮にも追従するよう、スケール込みで焼いて同じスケールで置く。
                    g.Skin.BakeMesh(g.Baked, true);
                    SmoothNormals(g);
                    g.Transform.SetPositionAndRotation(t.position, t.rotation);
                    g.Transform.localScale = t.lossyScale;
                }
                else
                {
                    g.Transform.SetPositionAndRotation(t.position, t.rotation);
                    g.Transform.localScale = t.lossyScale;
                }
            }
        }

        // ローポリは面ごとに頂点が分かれているので、そのまま膨らませると面が割れる。
        // 同じ位置の頂点の法線を平均して、継ぎ目なく膨らむようにする。
        private static void SmoothNormals(Ghost g)
        {
            if (g.WeldGroups == null) BuildWeldGroups(g);
            g.Baked.GetNormals(NormalBuffer);
            System.Array.Clear(g.GroupNormals, 0, g.GroupNormals.Length);
            for (int i = 0; i < NormalBuffer.Count; i++) g.GroupNormals[g.WeldGroups[i]] += NormalBuffer[i];
            for (int i = 0; i < NormalBuffer.Count; i++) NormalBuffer[i] = g.GroupNormals[g.WeldGroups[i]].normalized;
            g.Baked.SetNormals(NormalBuffer);
        }

        private static void BuildWeldGroups(Ghost g)
        {
            var vertices = g.Baked.vertices;
            float cell = Mathf.Max(g.Baked.bounds.size.magnitude / 2000f, 0.000001f);
            var map = new Dictionary<Vector3Int, int>();
            g.WeldGroups = new int[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                var v = vertices[i];
                var key = new Vector3Int(Mathf.RoundToInt(v.x / cell), Mathf.RoundToInt(v.y / cell), Mathf.RoundToInt(v.z / cell));
                if (!map.TryGetValue(key, out int group))
                {
                    group = map.Count;
                    map[key] = group;
                }
                g.WeldGroups[i] = group;
            }
            g.GroupNormals = new Vector3[map.Count];
        }

        private void LateUpdate()
        {
            RefreshGhosts();
            ApplyPhase();
        }

        // フラッシュはコマ数で、膜は実時間で進める（ヒットストップ中も動く）。
        private void ApplyPhase()
        {
            bool flashing = Time.frameCount - startFrame < flashFrames;
            if (flashing)
            {
                block.SetFloat("_Flash", 1f);
                block.SetFloat("_Expand", FlashExpand);
            }
            else
            {
                if (filmStart < 0f) filmStart = Time.unscaledTime;
                float t = (Time.unscaledTime - filmStart) / filmSeconds;
                if (t >= 1f)
                {
                    Destroy(gameObject);
                    return;
                }
                block.SetFloat("_Flash", 0f);
                block.SetFloat("_Expand", Mathf.Lerp(FilmExpandStart, FilmExpandEnd, t));
                block.SetFloat("_Intensity", Mathf.Pow(1f - t, 1.5f));
                block.SetFloat("_Dissolve", Mathf.Lerp(0.05f, 0.55f, t));
            }
            foreach (var g in ghosts)
            {
                // 倒して本体が消えたら、残った形を炎で埋めて燃え尽きるように見せる。
                block.SetFloat("_Fill", g.Source == null ? 1f : 0f);
                g.Renderer.SetPropertyBlock(block);
            }
        }

        private void OnDestroy()
        {
            foreach (var g in ghosts)
            {
                if (g.Baked != null) Destroy(g.Baked);
            }
        }
    }
}
