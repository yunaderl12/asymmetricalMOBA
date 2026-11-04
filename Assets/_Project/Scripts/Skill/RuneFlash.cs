using UnityEngine;

namespace Game.Skill
{
    /// 一瞬だけ開いて薄れていく魔法陣。
    public class RuneFlash : MonoBehaviour
    {
        private const float OpenSeconds = 0.07f;
        private const float StartScale = 0.55f;

        private MeshRenderer disc;
        private MaterialPropertyBlock block;
        private Color color;
        private float radius;
        private float seconds;
        private float timer;

        public static void Spawn(Vector3 position, Quaternion rotation, float radius, float seconds, Color color)
        {
            var go = new GameObject("RuneFlash");
            go.transform.SetPositionAndRotation(position, rotation);
            var flash = go.AddComponent<RuneFlash>();
            flash.disc = FireballVfx.NewMeshObject("Disc", go.transform, FireballVfx.QuadMesh, FireballVfx.GetMaterial("VFX/RuneCircle"));
            flash.block = new MaterialPropertyBlock();
            flash.color = color;
            flash.radius = radius;
            flash.seconds = seconds;
            flash.Apply();
        }

        private void Update()
        {
            timer += Time.deltaTime;
            if (timer >= seconds)
            {
                Destroy(gameObject);
                return;
            }
            Apply();
        }

        private void Apply()
        {
            float open = Mathf.Clamp01(timer / OpenSeconds);
            float size = radius * 2f * Mathf.Lerp(StartScale, 1f, 1f - (1f - open) * (1f - open));
            transform.localScale = new Vector3(size, size, 1f);
            Color c = color;
            c.a = 1f - Mathf.Pow(timer / seconds, 2f);
            block.SetColor("_Color", c);
            block.SetFloat("_Fill", 0.25f);
            disc.SetPropertyBlock(block);
        }
    }
}
