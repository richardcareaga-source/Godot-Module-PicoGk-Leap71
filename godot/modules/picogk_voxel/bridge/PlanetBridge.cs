using System.Numerics;
using Leap71.ShapeKernel;
using PicoGK;
using Biome = Leap71.ShapeKernel.Biome;

namespace PicogkGodotBridge;

/// <summary>
/// PlanetTest pipeline: voxBiome / voxFull → visible meshes for Godot + optional voxel preview data.
/// </summary>
public static class PlanetBridge
{
    public static readonly (Biome Biome, string LogName)[] PlanetTestBiomeOrder =
    {
        (Biome.Ocean, "Ocean"),
        (Biome.Tropical, "Tropical"),
        (Biome.Temperate, "Temperate"),
        (Biome.Tundra, "Tundra"),
        (Biome.Mountain, "Mountain"),
        (Biome.Polar, "Polar"),
    };

    public static readonly Dictionary<string, ColorFloat> PlanetTestBiomeColors = new()
    {
        ["Ocean"] = Cp.clrBlue,
        ["Tropical"] = Cp.clrLemongrass,
        ["Temperate"] = Cp.clrGreen,
        ["Tundra"] = Cp.clrRock,
        ["Mountain"] = Cp.clrGray,
        ["Polar"] = Cp.clrFrozen,
    };

    public sealed class PlanetTaskResult
    {
        /// <summary>Full planet surface — always built (visible in Godot).</summary>
        public PicogkMeshData DisplayMesh { get; set; } = null!;

        /// <summary>Per-biome surfaces (PlanetTest PreviewVoxels layers, meshed for Godot).</summary>
        public Dictionary<string, PicogkMeshData> BiomeMeshes { get; } = new();

        /// <summary>Optional voxel centers per biome (PreviewVoxels-style).</summary>
        public Dictionary<string, VoxelPreviewData> BiomeVoxels { get; } = new();

        /// <summary>Single voxFull mesh with blended biome vertex colors.</summary>
        public ColoredPlanetMeshData? ColoredSolidMesh { get; set; }
    }

    public static ColoredPlanetMeshData RunSolidColoredPlanet(
        float radiusMm,
        float voxelSizeMm,
        int noiseSeed = 42,
        float biomeBleed = 1f,
        Action<string>? log = null)
    {
        log?.Invoke("Exporting full planet (solid mesh, biome color bleed)...");
        var colored = ColoredPlanetMeshData.BuildFromVoxFull(radiusMm, voxelSizeMm, noiseSeed, biomeBleed);
        if (colored.Geometry.TriangleCount == 0)
            throw new InvalidOperationException("PicoGK produced an empty planet mesh — check native DLLs (picogk.26.1.dll).");
        log?.Invoke("=== Done ===");
        return colored;
    }

    public static PlanetTaskResult RunPlanetTaskLikePlanetTest(
        float radiusMm,
        float voxelSizeMm,
        Action<string>? log = null,
        bool includeBiomeMeshes = true,
        bool includeBiomeVoxels = false,
        bool reverseWinding = false,
        int noiseSeed = 42)
    {
        Noise.SetSeed(noiseSeed);

        using var rt = new PicogkHeadlessRuntime(voxelSizeMm);
        rt.ActivateGlobalLibrary();

        PlanetSDF.R = radiusMm;
        var planet = new Planet(new LocalFrame(new Vector3(0, 0, 0)));
        var result = new PlanetTaskResult();

        if (includeBiomeMeshes || includeBiomeVoxels)
        {
            foreach (var (biome, name) in PlanetTestBiomeOrder)
            {
                log?.Invoke($"{name}...");
                using var vox = planet.voxBiome(biome);
                if (includeBiomeMeshes)
                    result.BiomeMeshes[name] = VoxelsToMeshData(vox, reverseWinding);
                if (includeBiomeVoxels)
                    result.BiomeVoxels[name] = VoxelPreviewData.FromVoxels(vox);
            }
        }

        log?.Invoke("Exporting full planet (single mesh)...");
        using var voxFull = planet.voxFull();
        result.DisplayMesh = VoxelsToMeshData(voxFull, reverseWinding);

        if (result.DisplayMesh.TriangleCount == 0)
            throw new InvalidOperationException("PicoGK produced an empty planet mesh — check native DLLs (picogk.26.1.dll).");

        log?.Invoke("=== Done ===");
        return result;
    }

    public static PicogkMeshData VoxelsToMeshData(Voxels vox, bool reverseWinding = false)
    {
        using var msh = new Mesh(vox);
        return new PicogkMeshData(msh, reverseWinding);
    }
}
