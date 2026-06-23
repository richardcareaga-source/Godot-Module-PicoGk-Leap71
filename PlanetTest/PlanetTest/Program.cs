using Leap71.ShapeKernel;
using PicoGK;
using System.Numerics;

string strOut = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "PlanetTest_Output");
Directory.CreateDirectory(strOut);

try
{
    File.WriteAllText(Path.Combine(strOut, "log.txt"), "Started " + DateTime.Now + "\n");
    PicoGK.Library.Go(1.2f, PlanetTask.Run,
        Path.Combine(strOut, "picogk.log"), bEndAppWithTask: true);
    File.AppendAllText(Path.Combine(strOut, "log.txt"), "Done " + DateTime.Now + "\n");
}
catch (Exception e) { Console.WriteLine("Failed: " + e); }

public static class PlanetTask
{
    static readonly string strOut = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "PlanetTest_Output");

    public static void Run()
    {
        try
        {
            var oPlanet = new Planet(new LocalFrame(new Vector3(0, 0, 0)));

            // Each biome voxelised separately — proper colour per zone
            // All sampled from the same noise field so boundaries are continuous

            Library.Log("Ocean...");
            var voxOcean = oPlanet.voxBiome(Biome.Ocean);
            Sh.PreviewVoxels(voxOcean, Cp.clrBlue, 0.95f);

            Library.Log("Tropical...");
            var voxTrop = oPlanet.voxBiome(Biome.Tropical);
            Sh.PreviewVoxels(voxTrop, Cp.clrLemongrass, 0.95f);

            Library.Log("Temperate...");
            var voxTemp = oPlanet.voxBiome(Biome.Temperate);
            Sh.PreviewVoxels(voxTemp, Cp.clrGreen, 0.95f);

            Library.Log("Tundra...");
            var voxTundra = oPlanet.voxBiome(Biome.Tundra);
            Sh.PreviewVoxels(voxTundra, Cp.clrRock, 0.95f);

            Library.Log("Mountain...");
            var voxMtn = oPlanet.voxBiome(Biome.Mountain);
            Sh.PreviewVoxels(voxMtn, Cp.clrGray, 0.95f);

            Library.Log("Polar...");
            var voxPolar = oPlanet.voxBiome(Biome.Polar);
            Sh.PreviewVoxels(voxPolar, Cp.clrFrozen, 0.95f);

            Library.Log("Exporting full planet STL...");
            var voxFull = oPlanet.voxFull();
            Sh.ExportVoxelsToSTLFile(voxFull, Path.Combine(strOut, "Planet.STL"));

            Library.Log("=== Done ===");
        }
        catch (Exception e) { Library.Log("Error: " + e.Message); }
    }
}
