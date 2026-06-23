using PicoGK;

namespace PicogkGodotBridge;

/// <summary>
/// Headless PicoGK — no Viewer, no Library.Go. Uses public Library + Voxels + Mesh API.
/// </summary>
public sealed class PicogkHeadlessRuntime : IDisposable
{
    public Library Lib { get; }
    public float VoxelSizeMm { get; }

    public PicogkHeadlessRuntime(float voxelSizeMm)
    {
        if (voxelSizeMm <= 0f)
            throw new ArgumentOutOfRangeException(nameof(voxelSizeMm));
        VoxelSizeMm = voxelSizeMm;
        Lib = new Library(voxelSizeMm);
    }

    public Voxels VoxelsFromImplicit(IImplicit sdf, in BBox3 bounds)
        => new Voxels(Lib, sdf, bounds);

    public Mesh MeshFromVoxels(Voxels vox)
        => new Mesh(vox);

    public void ActivateGlobalLibrary()
    {
        try { Library.UnregisterGlobalLibrary(); } catch { /* none registered */ }
        Library.RegisterGlobalLibrary(Lib);
    }

    public void Dispose()
    {
        try { Library.UnregisterGlobalLibrary(); } catch { }
        Lib.Dispose();
    }
}
