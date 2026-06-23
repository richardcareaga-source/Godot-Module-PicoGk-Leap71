using System.Numerics;
using Leap71.ShapeKernel;
using PicoGK;

namespace PicogkGodotBridge;

/// <summary>
/// ShapeKernel BaseShapes → voxels → mesh (rocks, pipes, lenses, etc.).
/// </summary>
public static class ShapeKernelBridge
{
    public static PicogkMeshData SphereMesh(PicogkHeadlessRuntime rt, float radiusMm, bool reverseWinding = false)
    {
        var sphere = new BaseSphere(new LocalFrame(new Vector3(0, 0, 0)), radiusMm);
        using var vox = sphere.voxConstruct();
        using var msh = rt.MeshFromVoxels(vox);
        return new PicogkMeshData(msh, reverseWinding);
    }

    public static PicogkMeshData BoxMesh(PicogkHeadlessRuntime rt, float sizeMm, bool reverseWinding = false)
    {
        float h = sizeMm * 0.5f;
        var box = new BaseBox(new LocalFrame(new Vector3(0, 0, 0)), 2f * h, 2f * h, 2f * h);
        using var vox = box.voxConstruct();
        using var msh = rt.MeshFromVoxels(vox);
        return new PicogkMeshData(msh, reverseWinding);
    }
}
