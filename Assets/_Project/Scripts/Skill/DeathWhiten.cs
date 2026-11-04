using UnityEngine;

namespace Game.Skill
{
    /// 魔物の破裂演出で、モデルの全マテリアルを「白く染まって膨らむ」シェーダーへ差し替える。
    public class DeathWhiten
    {
        private readonly Renderer[] renderers;
        private readonly Material[][] originals;
        private readonly MaterialPropertyBlock[][] blocks;

        public DeathWhiten(Renderer[] renderers, Material[][] originals)
        {
            this.renderers = renderers;
            this.originals = originals;
            blocks = new MaterialPropertyBlock[renderers.Length][];
            var shared = FireballVfx.GetMaterial("VFX/DeathWhiten");
            for (int i = 0; i < renderers.Length; i++)
            {
                var slots = originals[i];
                blocks[i] = new MaterialPropertyBlock[slots.Length];
                var swapped = new Material[slots.Length];
                for (int s = 0; s < slots.Length; s++)
                {
                    swapped[s] = shared;
                    var block = new MaterialPropertyBlock();
                    var source = slots[s];
                    if (source != null && source.HasProperty("_BaseMap") && source.GetTexture("_BaseMap") != null) block.SetTexture("_BaseMap", source.GetTexture("_BaseMap"));
                    block.SetColor("_BaseColor", source != null && source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : Color.white);
                    blocks[i][s] = block;
                }
                if (renderers[i] != null) renderers[i].sharedMaterials = swapped;
            }
        }

        /// white: 白さ（0〜1）/ inflate: 法線方向の膨らみ（m）/ seed: 変えるたびに膨らみの凹凸が変わる。
        public void Apply(float white, float inflate, float seed)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                for (int s = 0; s < blocks[i].Length; s++)
                {
                    var block = blocks[i][s];
                    block.SetFloat("_White", white);
                    block.SetFloat("_Inflate", inflate);
                    block.SetFloat("_Seed", seed);
                    renderers[i].SetPropertyBlock(block, s);
                }
            }
        }

        public void Restore()
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                renderers[i].sharedMaterials = originals[i];
                for (int s = 0; s < blocks[i].Length; s++) renderers[i].SetPropertyBlock(null, s);
            }
        }
    }
}
