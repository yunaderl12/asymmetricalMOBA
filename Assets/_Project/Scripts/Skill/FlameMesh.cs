using UnityEngine;

namespace Game.Skill
{
    /// 涙滴形の炎のメッシュ。+Z が丸い根元で、-Z へ尾が細くのびる。全長1。uv.y は根元0・尾1。
    public static class FlameMesh
    {
        private const int Rings = 28;
        private const int Sides = 20;
        private const float HeadRadius = 0.16f;
        private const float HeadLength = 0.16f;
        private const float TailPower = 1.25f;

        private static Mesh mesh;

        public static Mesh Get()
        {
            if (mesh == null) mesh = Build();
            return mesh;
        }

        private static float RadiusAt(float t)
        {
            if (t < HeadLength)
            {
                float d = (HeadLength - t) / HeadLength;
                return HeadRadius * Mathf.Sqrt(Mathf.Max(0f, 1f - d * d));
            }
            return HeadRadius * Mathf.Pow(1f - (t - HeadLength) / (1f - HeadLength), TailPower);
        }

        private static Mesh Build()
        {
            int stride = Sides + 1;
            var vertices = new Vector3[(Rings + 1) * stride];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[Rings * Sides * 6];
            for (int r = 0; r <= Rings; r++)
            {
                float t = (float)r / Rings;
                float radius = RadiusAt(t);
                for (int s = 0; s <= Sides; s++)
                {
                    float angle = Mathf.PI * 2f * s / Sides;
                    vertices[r * stride + s] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, HeadRadius - t);
                    uvs[r * stride + s] = new Vector2((float)s / Sides, t);
                }
            }
            int index = 0;
            for (int r = 0; r < Rings; r++)
            {
                for (int s = 0; s < Sides; s++)
                {
                    int a = r * stride + s;
                    int b = a + stride;
                    triangles[index++] = a;
                    triangles[index++] = b;
                    triangles[index++] = a + 1;
                    triangles[index++] = b;
                    triangles[index++] = b + 1;
                    triangles[index++] = a + 1;
                }
            }
            var built = new Mesh { name = "FlameMesh", vertices = vertices, uv = uvs, triangles = triangles };
            built.RecalculateNormals();
            // 継ぎ目で法線が割れないよう、両端をそろえる。
            var normals = built.normals;
            for (int r = 0; r <= Rings; r++)
            {
                Vector3 n = (normals[r * stride] + normals[r * stride + Sides]).normalized;
                normals[r * stride] = n;
                normals[r * stride + Sides] = n;
            }
            built.normals = normals;
            built.bounds = new Bounds(new Vector3(0f, 0f, HeadRadius - 0.5f), Vector3.one * 2f);
            return built;
        }
    }
}
