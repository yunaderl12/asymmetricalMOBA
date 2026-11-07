using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// 地図の構成（川・海・配置）。Resources/StageMap.json を読む。地形を作る Blender のスクリプトも同じファイルを読む。
    public static class StageMap
    {
        public enum Biome { None, Mountain, Forest, Desert }

        [Serializable]
        public class Area
        {
            public string name;
            public float x, z, rx, rz;
        }

        [Serializable]
        public class Line
        {
            public float width;
            public float[] points;
        }

        [Serializable]
        public class Place
        {
            public string name;
            public float x;
            public float z;
            public float[] waypoints;
        }

        [Serializable]
        public class Data
        {
            public float size;
            public float townX, townZ, townGateX, townGateZ, castleX, castleZ;
            public float castleHeight;
            public float islandRadius;
            public Line[] shallow;
            public Line[] deep;
            public float[] seaShore;
            public float[] seaCorner;
            public float[] bridgeStarts;
            public Area[] biomes;
            public Place[] spawners;
            public Place[] rocks;
        }

        [Serializable]
        private class SandMask
        {
            public float x0, z0, cell;
            public int cols, rows;
            public string[] bits;
        }

        private static Data data;
        private static string loadedResource;
        private static string sandResource;
        private static bool[] sandCells;
        private static SandMask sand;
        private static Vector2[] seaPolygon;

        public static Data Map
        {
            get
            {
                string wanted = WorldCatalog.ForActiveScene().mapResource;
                if (data == null || loadedResource != wanted) Load(wanted);
                return data;
            }
        }

        private static void Load(string resource)
        {
            loadedResource = resource;
            var text = Resources.Load<TextAsset>(resource);
            if (text == null)
            {
                Debug.LogWarning("[StageMap] Resources/" + resource + ".json not found");
                data = new Data { shallow = new Line[0], deep = new Line[0], seaShore = new float[0], seaCorner = new float[2] };
            }
            else data = JsonUtility.FromJson<Data>(text.text);

            var polygon = new List<Vector2>();
            for (int i = 0; i + 1 < data.seaShore.Length; i += 2) polygon.Add(new Vector2(data.seaShore[i], data.seaShore[i + 1]));
            polygon.Add(new Vector2(data.seaCorner[0], data.seaCorner[1]));
            seaPolygon = polygon.ToArray();
        }

        /// 砂漠の道から外れた砂地か。Resources/<地図>_Sand.json（Blender の build_lowpoly.py が地面の色分けと同じ判定で書き出す）を読む。
        /// ファイルがないワールドでは常に false。
        public static bool IsLooseSand(Vector3 position)
        {
            string wanted = WorldCatalog.ForActiveScene().mapResource + "_Sand";
            if (sandResource != wanted) LoadSand(wanted);
            if (sandCells == null) return false;
            int i = Mathf.FloorToInt((position.x - sand.x0) / sand.cell);
            int j = Mathf.FloorToInt((position.z - sand.z0) / sand.cell);
            if (i < 0 || j < 0 || i >= sand.cols || j >= sand.rows) return false;
            return sandCells[j * sand.cols + i];
        }

        private static void LoadSand(string resource)
        {
            sandResource = resource;
            sandCells = null;
            var text = Resources.Load<TextAsset>(resource);
            if (text == null) return;
            sand = JsonUtility.FromJson<SandMask>(text.text);
            if (sand == null || sand.bits == null || sand.cell <= 0f) return;
            sandCells = new bool[sand.cols * sand.rows];
            for (int j = 0; j < sand.rows && j < sand.bits.Length; j++)
            {
                // 1行は右端が i=0 の16進数。
                string row = sand.bits[j];
                for (int k = 0; k < row.Length; k++)
                {
                    int nibble = Convert.ToInt32(row[row.Length - 1 - k].ToString(), 16);
                    for (int b = 0; b < 4; b++)
                    {
                        int i = k * 4 + b;
                        if (i < sand.cols && (nibble & (1 << b)) != 0) sandCells[j * sand.cols + i] = true;
                    }
                }
            }
        }

        /// 歩いて渡れる浅瀬の中か。
        public static bool IsShallow(Vector3 position)
        {
            return Gap(position, Map.shallow) <= 0f;
        }

        /// 深い川か海の中か。
        public static bool IsDeepWater(Vector3 position)
        {
            return IsSea(position) || Gap(position, Map.deep) <= 0f;
        }

        public static bool IsSea(Vector3 position)
        {
            if (Map == null) return false;
            var polygon = seaPolygon;
            float x = position.x, z = position.z;
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                Vector2 a = polygon[i], b = polygon[j];
                if ((a.y > z) != (b.y > z) && x < (b.x - a.x) * (z - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside;
        }

        /// その場所のバイオーム。水の中、まちの周り、魔王城の島は None。
        public static Biome BiomeAt(Vector3 position)
        {
            if (Map.biomes == null || IsDeepWater(position) || IsShallow(position)) return Biome.None;
            foreach (var area in Map.biomes)
            {
                float dx = (position.x - area.x) / area.rx, dz = (position.z - area.z) / area.rz;
                if (dx * dx + dz * dz >= 1f) continue;
                if (area.name == "desert") return Biome.Desert;
                if (area.name == "forest") return Biome.Forest;
                if (area.name == "mountain" && NorthOfMainRiver(position)) return Biome.Mountain;
            }
            return Biome.None;
        }

        /// バイオームの楕円の中の適当な点。
        public static Vector3 RandomPointIn(Biome biome)
        {
            string key = biome.ToString().ToLowerInvariant();
            foreach (var area in Map.biomes)
            {
                if (area.name != key) continue;
                var offset = UnityEngine.Random.insideUnitCircle;
                return new Vector3(area.x + offset.x * area.rx, 0f, area.z + offset.y * area.rz);
            }
            return Vector3.zero;
        }

        // 山は1本目の深い川の北東側だけ。川の進行方向の左側。
        private static bool NorthOfMainRiver(Vector3 position)
        {
            var river = Map.deep[0].points;
            float best = float.MaxValue, side = 0f;
            for (int i = 0; i + 3 < river.Length; i += 2)
            {
                float d = SegmentDistance(position.x, position.z, river[i], river[i + 1], river[i + 2], river[i + 3]);
                if (d >= best) continue;
                best = d;
                side = (river[i + 2] - river[i]) * (position.z - river[i + 1]) - (river[i + 3] - river[i + 1]) * (position.x - river[i]);
            }
            return side > 0f;
        }

        /// いちばん近い水辺までの距離（水の中は0以下）。
        public static float DistanceToWater(Vector3 position)
        {
            if (IsSea(position)) return 0f;
            return Mathf.Min(Gap(position, Map.shallow), Gap(position, Map.deep), ShoreDistance(position));
        }

        private static float ShoreDistance(Vector3 position)
        {
            float best = float.MaxValue;
            var flat = Map.seaShore;
            for (int i = 0; i + 3 < flat.Length; i += 2)
                best = Mathf.Min(best, SegmentDistance(position.x, position.z, flat[i], flat[i + 1], flat[i + 2], flat[i + 3]));
            return best;
        }

        private static float Gap(Vector3 position, Line[] lines)
        {
            float best = float.MaxValue;
            foreach (var line in lines)
            {
                var flat = line.points;
                for (int i = 0; i + 3 < flat.Length; i += 2)
                {
                    float d = SegmentDistance(position.x, position.z, flat[i], flat[i + 1], flat[i + 2], flat[i + 3]) - line.width * 0.5f;
                    if (d < best) best = d;
                }
            }
            return best;
        }

        private static float SegmentDistance(float px, float pz, float ax, float az, float bx, float bz)
        {
            float dx = bx - ax, dz = bz - az;
            float t = Mathf.Clamp01(((px - ax) * dx + (pz - az) * dz) / (dx * dx + dz * dz));
            float ex = ax + dx * t - px, ez = az + dz * t - pz;
            return Mathf.Sqrt(ex * ex + ez * ez);
        }
    }
}
