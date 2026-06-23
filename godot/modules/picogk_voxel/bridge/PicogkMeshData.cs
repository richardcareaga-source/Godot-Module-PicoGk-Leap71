using System.Numerics;
using PicoGK;

namespace PicogkGodotBridge;

/// <summary>
/// Mesh triangles from PicoGK, positions in Godot meters (mm / 1000).
/// </summary>
public sealed class PicogkMeshData
{
    public float[] Vertices { get; }
    public float[] Normals { get; }
    public int TriangleCount { get; }

    const float MmToM = 0.001f;

    public PicogkMeshData(Mesh msh, bool reverseWinding = false)
    {
        int n = msh.nTriangleCount();
        TriangleCount = n;
        Vertices = new float[n * 9];
        Normals = new float[n * 9];

        for (int i = 0; i < n; i++)
        {
            msh.GetTriangle(i, out Vector3 a, out Vector3 b, out Vector3 c);
            if (reverseWinding)
                (b, c) = (c, b);

            var va = a * MmToM;
            var vb = b * MmToM;
            var vc = c * MmToM;
            var normal = Vector3.Normalize(Vector3.Cross(vb - va, vc - va));

            int o = i * 9;
            WriteVec(Vertices, o + 0, va);
            WriteVec(Vertices, o + 3, vb);
            WriteVec(Vertices, o + 6, vc);
            WriteVec(Normals, o + 0, normal);
            WriteVec(Normals, o + 3, normal);
            WriteVec(Normals, o + 6, normal);
        }
    }

    static void WriteVec(float[] arr, int offset, Vector3 v)
    {
        arr[offset] = v.X;
        arr[offset + 1] = v.Y;
        arr[offset + 2] = v.Z;
    }
}
