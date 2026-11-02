using UnityEngine;

namespace Game.Hero
{
    /// Knightの骨格に合わせて関節位置を変えたモデル用。共有クリップは骨の位置もKnightの値で書き込むので、
    /// 骨の位置だけモデル本来の値へ戻し、回転はクリップのまま使う。腰はKnightとの高さの比で上下の動きを拡大する。
    [DefaultExecutionOrder(-10)]
    public class ModelBoneFit : MonoBehaviour
    {
        private const string HipsName = "Hips";
        private const string ReferencePath = "Knight/knight";

        private static bool referenceLoaded;
        private static Vector3 referenceHips;

        private Transform hips;
        private Vector3 hipsRest;
        private float hipsScale;
        private Vector3 hipsWritten;
        private Transform[] bones;
        private Vector3[] restPositions;

        /// Instantiate直後に呼ぶ。Knightと同じ骨位置のモデルには何も付けずnullを返す。
        public static ModelBoneFit Attach(GameObject model)
        {
            var modelHips = FindBone(model.transform, HipsName);
            if (modelHips == null || !LoadReference()) return null;
            if ((modelHips.localPosition - referenceHips).sqrMagnitude < 1e-6f) return null;

            var fit = model.AddComponent<ModelBoneFit>();
            fit.hips = modelHips;
            fit.hipsRest = modelHips.localPosition;
            fit.hipsScale = referenceHips.magnitude > 1e-5f ? modelHips.localPosition.magnitude / referenceHips.magnitude : 1f;
            fit.hipsWritten = modelHips.localPosition;
            var all = modelHips.GetComponentsInChildren<Transform>(true);
            fit.bones = new Transform[all.Length - 1];
            fit.restPositions = new Vector3[all.Length - 1];
            for (int i = 1; i < all.Length; i++)
            {
                fit.bones[i - 1] = all[i];
                fit.restPositions[i - 1] = all[i].localPosition;
            }
            return fit;
        }

        private static bool LoadReference()
        {
            if (referenceLoaded) return true;
            var prefab = Resources.Load<GameObject>(ReferencePath);
            var refHips = prefab != null ? FindBone(prefab.transform, HipsName) : null;
            if (refHips == null) return false;
            referenceHips = refHips.localPosition;
            referenceLoaded = true;
            return true;
        }

        private static Transform FindBone(Transform root, string boneName)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == boneName) return t;
            return null;
        }

        private void LateUpdate()
        {
            Apply();
        }

        /// クリップを当てた後に骨の位置を戻す。アニメーターを通さず姿勢を作るとき（肖像生成）は直接呼ぶ。
        public void Apply()
        {
            // アニメーターが書き込まなかったフレームは、前回の結果へ重ねて掛けないようにする。
            if (hips.localPosition != hipsWritten)
            {
                hipsWritten = hipsRest + (hips.localPosition - referenceHips) * hipsScale;
                hips.localPosition = hipsWritten;
            }
            for (int i = 0; i < bones.Length; i++) bones[i].localPosition = restPositions[i];
        }
    }
}
