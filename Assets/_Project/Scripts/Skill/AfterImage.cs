using UnityEngine;

namespace Game.Skill
{
    /// 走り抜けた体の形を残して消える残像。元のモデルと同じ Layer に置くので、自分のカメラには映らない。
    public class AfterImage : MonoBehaviour
    {
        private MeshRenderer shell;
        private Mesh baked;
        private MaterialPropertyBlock block;
        private Color color;
        private float seconds;
        private float timer;

        public static void Spawn(Transform modelRoot, Color color, float seconds)
        {
            if (modelRoot == null) return;
            foreach (var skin in modelRoot.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var go = new GameObject("AfterImage");
                go.layer = skin.gameObject.layer;
                go.transform.SetPositionAndRotation(skin.transform.position, skin.transform.rotation);
                go.transform.localScale = skin.transform.lossyScale;
                var image = go.AddComponent<AfterImage>();
                image.baked = new Mesh { name = "AfterImage" };
                skin.BakeMesh(image.baked, true);
                image.shell = FireballVfx.NewMeshObject("Shell", go.transform, image.baked, FireballVfx.GetMaterial("VFX/EnergyFilm"));
                image.shell.gameObject.layer = go.layer;
                image.color = color;
                image.seconds = seconds;
                image.block = new MaterialPropertyBlock();
                image.Apply(0f);
            }
        }

        private void Update()
        {
            timer += Time.deltaTime;
            float t = timer / seconds;
            if (t >= 1f)
            {
                Destroy(baked);
                Destroy(gameObject);
                return;
            }
            Apply(t);
        }

        private void Apply(float t)
        {
            var c = color;
            c.a = (1f - t) * (1f - t);
            block.SetColor("_Color", c);
            block.SetFloat("_Hex", 0f);
            block.SetFloat("_Flash", 0f);
            shell.SetPropertyBlock(block);
        }
    }
}
