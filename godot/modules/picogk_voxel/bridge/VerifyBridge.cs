using Leap71.ShapeKernel;

namespace PicogkGodotBridge;

static class VerifyBridge
{
    public static void Run()
    {
        Console.WriteLine("PicogkGodotBridge verify...");

        Console.WriteLine("Planet (mesh display)...");
        var result = PlanetBridge.RunPlanetTaskLikePlanetTest(80f, 1.2f, Console.WriteLine);
        Console.WriteLine($"  display tris: {result.DisplayMesh.TriangleCount:N0}");
        foreach (var kv in result.BiomeMeshes)
            Console.WriteLine($"  {kv.Key}: {kv.Value.TriangleCount:N0} tris");

        using var rt = PicogkBridge.CreateRuntime(1.2f);
        rt.ActivateGlobalLibrary();

        Console.WriteLine("ShapeKernel sphere...");
        var sphere = ShapeKernelBridge.SphereMesh(rt, 30f);
        Console.WriteLine($"  triangles: {sphere.TriangleCount}");

        Console.WriteLine("OK.");
    }
}
