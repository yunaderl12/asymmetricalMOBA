using UnityEngine;

namespace Game.Skill
{
    /// 地面を走る光の輪。広げれば着地の衝撃、縮めれば力を吸い込む予備動作になる。
    public class ShockRing : MonoBehaviour
    {
        private MeshRenderer disc;
        private MaterialPropertyBlock block;
        private float startRadius;
        private float endRadius;
        private float maxRadius;
        private float seconds;
        private float width;
        private float timer;
        private Color color;

        public static ShockRing Spawn(Vector3 position, float startRadius, float endRadius, float seconds, float width, Color color)
        {
            Vector3 up = Vector3.up;
            if (Physics.Raycast(position + Vector3.up, Vector3.down, out var hit, 3f, ~0, QueryTriggerInteraction.Ignore))
            {
                position = hit.point;
                up = hit.normal;
            }
            var go = new GameObject("ShockRing");
            go.transform.SetPositionAndRotation(position + up * 0.05f, Quaternion.FromToRotation(Vector3.up, up));
            var ring = go.AddComponent<ShockRing>();
            ring.startRadius = startRadius;
            ring.endRadius = endRadius;
            ring.maxRadius = Mathf.Max(startRadius, endRadius) + width;
            ring.seconds = seconds;
            ring.width = width;
            ring.color = color;
            ring.block = new MaterialPropertyBlock();
            ring.disc = FireballVfx.NewMeshObject("Ring", go.transform, FireballVfx.QuadMesh, FireballVfx.GetMaterial("VFX/ShockRing"));
            ring.disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            ring.disc.transform.localScale = Vector3.one * (ring.maxRadius * 2f);
            ring.Apply(0f);
            return ring;
        }

        private void Update()
        {
            timer += Time.deltaTime;
            float t = timer / seconds;
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }
            Apply(t);
        }

        private void Apply(float t)
        {
            float eased = 1f - (1f - t) * (1f - t);
            float radius = Mathf.Lerp(startRadius, endRadius, eased);
            var c = color;
            c.a = 1f - t * t;
            block.SetColor("_Color", c);
            block.SetFloat("_Ring", radius / maxRadius);
            block.SetFloat("_Width", width / maxRadius);
            disc.SetPropertyBlock(block);
        }
    }
}
