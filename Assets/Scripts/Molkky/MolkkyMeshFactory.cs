using System.Collections.Generic;
using UnityEngine;

namespace RealMolkky.Molkky
{
    /// <summary>
    /// 棒とピンの仮メッシュ（本物の寸法）。見た目の作り込みはフェーズ4。
    /// 凸メッシュの当たり判定にもそのまま使う（三角形255枚以内）。
    /// </summary>
    public static class MolkkyMeshFactory
    {
        public const int DefaultSegments = 32;

        /// <summary>ピン: 原点は底面の中心、軸は +Y。斜めカットの低い側が -Z（投げる人の側）。</summary>
        public static Mesh CreatePin(int segments = DefaultSegments)
        {
            float r = MolkkySpec.PinRadius;
            float hi = MolkkySpec.PinHeight;
            float lo = MolkkySpec.PinCutLowHeight;
            float mid = (hi + lo) * 0.5f;
            float slope = (hi - lo) * 0.5f / r;
            var mesh = CreateCylinder(segments, r, _ => 0f, (x, z) => mid + z * slope);
            mesh.name = "MolkkyPin";
            return mesh;
        }

        /// <summary>棒: 原点は中心、軸は +Y。</summary>
        public static Mesh CreateStick(int segments = DefaultSegments)
        {
            float half = MolkkySpec.StickLength * 0.5f;
            var mesh = CreateCylinder(segments, MolkkySpec.StickDiameter * 0.5f, _ => -half, (_, _) => half);
            mesh.name = "MolkkyStick";
            return mesh;
        }

        static Mesh CreateCylinder(int segments, float radius,
            System.Func<float, float> bottomY, System.Func<float, float, float> topY)
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            // 側面
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                float x = Mathf.Cos(a) * radius;
                float z = Mathf.Sin(a) * radius;
                var n = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                verts.Add(new Vector3(x, bottomY(0f), z)); normals.Add(n); uvs.Add(new Vector2((float)i / segments, 0f));
                verts.Add(new Vector3(x, topY(x, z), z)); normals.Add(n); uvs.Add(new Vector2((float)i / segments, 1f));
            }
            for (int i = 0; i < segments; i++)
            {
                int b0 = i * 2, t0 = b0 + 1, b1 = b0 + 2, t1 = b0 + 3;
                tris.AddRange(new[] { b0, t0, t1, b0, t1, b1 });
            }

            // 底面
            int bc = verts.Count;
            verts.Add(new Vector3(0f, bottomY(0f), 0f)); normals.Add(Vector3.down); uvs.Add(new Vector2(0.5f, 0.5f));
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                verts.Add(new Vector3(Mathf.Cos(a) * radius, bottomY(0f), Mathf.Sin(a) * radius));
                normals.Add(Vector3.down);
                uvs.Add(new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f));
            }
            for (int i = 0; i < segments; i++)
            {
                tris.AddRange(new[] { bc, bc + 1 + i, bc + 1 + (i + 1) % segments });
            }

            // 上面（斜めでもよい）
            int tc = verts.Count;
            var p0 = new Vector3(radius, topY(radius, 0f), 0f);
            var p1 = new Vector3(0f, topY(0f, radius), radius);
            var center = new Vector3(0f, topY(0f, 0f), 0f);
            var topNormal = Vector3.Cross(p1 - center, p0 - center).normalized;
            verts.Add(center); normals.Add(topNormal); uvs.Add(new Vector2(0.5f, 0.5f));
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                float x = Mathf.Cos(a) * radius;
                float z = Mathf.Sin(a) * radius;
                verts.Add(new Vector3(x, topY(x, z), z));
                normals.Add(topNormal);
                uvs.Add(new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f));
            }
            for (int i = 0; i < segments; i++)
            {
                tris.AddRange(new[] { tc, tc + 1 + (i + 1) % segments, tc + 1 + i });
            }

            var mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }
    }
}
