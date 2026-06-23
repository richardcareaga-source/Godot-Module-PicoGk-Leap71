using Leap71.ShapeKernel;

namespace PicogkGodotBridge;

/// <summary>
/// Single voxFull mesh with per-vertex RGB (biome colors blended at borders).
/// </summary>
public sealed class ColoredPlanetMeshData
{
    public PicogkMeshData Geometry { get; }
    /// <summary>Per-vertex color components: [r,g,b, r,g,b, ...] length = TriangleCount * 9.</summary>
    public float[] VertexColorRgb { get; }

    public ColoredPlanetMeshData(PicogkMeshData geometry, float[] vertexColorRgb)
    {
        Geometry = geometry;
        VertexColorRgb = vertexColorRgb;
    }

    public static ColoredPlanetMeshData BuildFromVoxFull(
        float radiusMm,
        float voxelSizeMm,
        int noiseSeed,
        float biomeBleed = 1f)
    {
        Noise.SetSeed(noiseSeed);
        using var rt = new PicogkHeadlessRuntime(voxelSizeMm);
        rt.ActivateGlobalLibrary();

        PlanetSDF.R = radiusMm;
        var planet = new Planet(new LocalFrame(new System.Numerics.Vector3(0, 0, 0)));
        using var voxFull = planet.voxFull();
        var geom = PlanetBridge.VoxelsToMeshData(voxFull);

        var colors = new float[geom.TriangleCount * 9];
        for (int i = 0; i < geom.TriangleCount; i++)
        {
            int o = i * 9;
            for (int v = 0; v < 3; v++)
            {
                int vi = o + v * 3;
                var posM = new System.Numerics.Vector3(
                    geom.Vertices[vi], geom.Vertices[vi + 1], geom.Vertices[vi + 2]);
                var clr = BiomeColorSampler.ColorAtWorldM(posM, radiusMm, biomeBleed);
                colors[vi] = clr.R;
                colors[vi + 1] = clr.G;
                colors[vi + 2] = clr.B;
            }
        }

        return new ColoredPlanetMeshData(geom, colors);
    }
}
