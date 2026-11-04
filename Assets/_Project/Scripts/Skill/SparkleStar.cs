using UnityEngine;

namespace Game.Skill
{
    /// 四方にとがった星のきらめき。ふくらんでしぼみながら、ゆっくり上へ流れる。
    public class SparkleStar : MonoBehaviour
    {
        private float size;
        private float seconds;
        private float timer;
        private Vector3 velocity;

        public static void Spawn(Vector3 position, float size, float seconds, Vector3 velocity)
        {
            var renderer = FireballVfx.NewMeshObject("SparkleStar", null, FireballVfx.QuadMesh, FireballVfx.GetMaterial("VFX/SparkleStar"));
            renderer.transform.position = position;
            renderer.transform.localScale = Vector3.zero;
            var star = renderer.gameObject.AddComponent<SparkleStar>();
            star.size = size;
            star.seconds = seconds;
            star.velocity = velocity;
        }

        private void LateUpdate()
        {
            timer += Time.deltaTime;
            float k = timer / seconds;
            if (k >= 1f)
            {
                Destroy(gameObject);
                return;
            }
            transform.position += velocity * Time.deltaTime;
            float pop = k < 0.25f ? k / 0.25f : 1f - (k - 0.25f) / 0.75f;
            transform.localScale = Vector3.one * (size * pop);
            var camera = Camera.main;
            if (camera != null) transform.rotation = camera.transform.rotation;
        }
    }
}
