using UnityEngine;
using Game.Combat;

namespace Game.Skill
{
    /// 探知した敵の頭上に出す印。壁越しでも見え、離れていても読める大きさを保つ。Detect を使った本人にだけ見せる。
    public class RevealMark : MonoBehaviour
    {
        private const float HeadClearance = 0.5f;
        private const float MinSize = 0.45f;
        private const float SizePerDistance = 0.035f;
        private const float BlinkSeconds = 1.5f;
        private const float BlinkSpeed = 14f;

        private Transform target;
        private HealthComponent health;
        private Transform viewer;
        private MeshRenderer meshRenderer;
        private MaterialPropertyBlock block;
        private float timer;

        public static void Attach(HealthComponent target, Transform viewer, float seconds)
        {
            var renderer = FireballVfx.NewMeshObject("RevealMark", null, FireballVfx.QuadMesh, FireballVfx.GetMaterial("VFX/RevealMark"));
            var mark = renderer.gameObject.AddComponent<RevealMark>();
            mark.target = target.transform;
            mark.health = target;
            mark.viewer = viewer;
            mark.meshRenderer = renderer;
            mark.block = new MaterialPropertyBlock();
            mark.timer = seconds;
            mark.Follow();
        }

        private void LateUpdate()
        {
            timer -= Time.deltaTime;
            if (timer <= 0f || target == null || health == null || health.IsDead || viewer == null)
            {
                Destroy(gameObject);
                return;
            }
            Follow();
        }

        private void Follow()
        {
            Bounds body = FireballProjectile.BodyBoundsOf(target.gameObject, null);
            transform.position = new Vector3(target.position.x, body.max.y + HeadClearance, target.position.z);
            if (viewer != null)
            {
                transform.rotation = viewer.rotation;
                float distance = Vector3.Distance(viewer.position, transform.position);
                transform.localScale = Vector3.one * Mathf.Max(MinSize, distance * SizePerDistance);
            }
            float fade = timer < BlinkSeconds ? 0.55f + 0.45f * Mathf.Sin(timer * BlinkSpeed) : 1f;
            block.SetFloat("_Fade", fade);
            meshRenderer.SetPropertyBlock(block);
        }
    }
}
