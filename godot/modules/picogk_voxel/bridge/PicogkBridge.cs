using Leap71.ShapeKernel;

namespace PicogkGodotBridge;

/// <summary>
/// Entry point — use <see cref="PlanetBridge.RunPlanetTaskLikePlanetTest"/> (same as PlanetTest).
/// </summary>
public static class PicogkBridge
{
    public static PicogkHeadlessRuntime CreateRuntime(float voxelSizeMm = 1.2f)
        => new PicogkHeadlessRuntime(voxelSizeMm);

    /// <summary>Full PlanetTest pipeline in one call.</summary>
    public static PlanetBridge.PlanetTaskResult GeneratePlanetLikePlanetTest(
        float radiusMm = 80f,
        float voxelSizeMm = 1.2f,
        Action<string>? log = null)
        => PlanetBridge.RunPlanetTaskLikePlanetTest(radiusMm, voxelSizeMm, log);
}
