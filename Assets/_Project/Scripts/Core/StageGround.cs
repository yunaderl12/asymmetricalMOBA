using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Core
{
    /// フィールドの地面の高さ（Resources/StageGround.bytes、ArtSource/lowpoly/build_lowpoly.py が書き出す）。
    /// 地形モデルの Field_Ground と同じ格子・同じ三角形の分け方なので、HeightAt は見た目の地面とぴったり合う。
    /// 地面を削る仕組み（GroundDig）が、削ったチャンクの当たり判定をこの高さから作る。
    public static class StageGround
    {
        private static float x0, z0, cell;
        private static int n;
        private static float[] heights;
        private static string loadedFor;

        static StageGround()
        {
            SceneManager.activeSceneChanged += (from, to) => loadedFor = null;
        }

        /// 今のワールドに地面の高さのデータがあるか。
        public static bool Available => Load();

        public static float MinX => Load() ? x0 : 0f;
        public static float MinZ => Load() ? z0 : 0f;
        public static float Size => Load() ? n * cell : 0f;
        public static float Cell => Load() ? cell : 1f;

        /// その地点の地面の高さ。データがないワールドや範囲外では 0。
        public static float HeightAt(float x, float z)
        {
            if (!Load()) return 0f;
            float fx = (x - x0) / cell, fz = (z - z0) / cell;
            int i = Mathf.Clamp(Mathf.FloorToInt(fx), 0, n - 1);
            int j = Mathf.Clamp(Mathf.FloorToInt(fz), 0, n - 1);
            float u = Mathf.Clamp01(fx - i), v = Mathf.Clamp01(fz - j);
            float bl = H(i, j), br = H(i + 1, j), tl = H(i, j + 1), tr = H(i + 1, j + 1);
            if ((i + j) % 2 == 0)
            {
                // 左下と右上を結ぶ対角線。
                return u >= v ? bl + (br - bl) * u + (tr - br) * v : bl + (tr - tl) * u + (tl - bl) * v;
            }
            // 右下と左上を結ぶ対角線。
            return u + v <= 1f ? bl + (br - bl) * u + (tl - bl) * v : tr + (tl - tr) * (1f - u) + (br - tr) * (1f - v);
        }

        private static float H(int i, int j)
        {
            return heights[i + j * (n + 1)];
        }

        private static bool Load()
        {
            string resource = WorldCatalog.ForActiveScene().groundResource;
            if (loadedFor == resource) return heights != null;
            loadedFor = resource;
            heights = null;
            if (string.IsNullOrEmpty(resource)) return false;
            var asset = Resources.Load<TextAsset>(resource);
            if (asset == null) return false;
            byte[] bytes = asset.bytes;
            if (bytes.Length < 16) return false;
            x0 = System.BitConverter.ToSingle(bytes, 0);
            z0 = System.BitConverter.ToSingle(bytes, 4);
            cell = System.BitConverter.ToSingle(bytes, 8);
            n = System.BitConverter.ToInt32(bytes, 12);
            int count = (n + 1) * (n + 1);
            if (n <= 0 || cell <= 0f || bytes.Length < 16 + count * 4) return false;
            heights = new float[count];
            System.Buffer.BlockCopy(bytes, 16, heights, 0, count * 4);
            Resources.UnloadAsset(asset);
            return true;
        }
    }
}
