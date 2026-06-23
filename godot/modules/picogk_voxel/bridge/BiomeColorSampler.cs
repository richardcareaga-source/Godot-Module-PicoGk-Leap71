using System.Numerics;
using Leap71.ShapeKernel;
using PicoGK;

namespace PicogkGodotBridge;

/// <summary>
/// Soft biome colors on the planet surface — same rules as PlanetSDF.eBiome, with blend at borders.
/// </summary>
static class BiomeColorSampler
{
    const float NoiseScale = 0.028f;
    const float MmPerM = 1000f;

    public static ColorFloat ColorAtWorldM(Vector3 posM, float radiusMm, float bleedStrength)
    {
        float bleedN = 0.14f * bleedStrength;
        float bleedL = 0.09f * bleedStrength;

        var posMm = posM * MmPerM;
        float fR = posMm.Length();
        if (fR < 0.001f)
            return PlanetBridge.PlanetTestBiomeColors["Ocean"];

        float inv = 1f / fR;
        var unit = new Vector3(posMm.X * inv, posMm.Y * inv, posMm.Z * inv);
        float noise = Noise.fWarpedBm(
            unit.X * radiusMm * NoiseScale,
            unit.Y * radiusMm * NoiseScale,
            unit.Z * radiusMm * NoiseScale);
        float lat = MathF.Abs(unit.Z);

        float wPolar = Smooth(lat, 0.78f - bleedL, 0.78f + bleedL);
        float wOcean = 1f - Smooth(noise, -0.18f - bleedN, -0.18f + bleedN);
        float wTundra = Smooth(lat, 0.52f - bleedL, 0.52f + bleedL) * (1f - wPolar);
        float wMountain = Smooth(noise, 0.28f - bleedN, 0.28f + bleedN);
        float wTropical = (1f - Smooth(lat, 0.32f - bleedL, 0.32f + bleedL)) * (1f - wOcean);
        float wTemperate = MathF.Max(0f, 1f - wPolar - wOcean - wTundra - wMountain - wTropical);

        float sum = wPolar + wOcean + wTundra + wMountain + wTropical + wTemperate + 1e-6f;
        var c = PlanetBridge.PlanetTestBiomeColors;
        return new ColorFloat(
            (c["Polar"].R * wPolar + c["Ocean"].R * wOcean + c["Tundra"].R * wTundra +
             c["Mountain"].R * wMountain + c["Tropical"].R * wTropical + c["Temperate"].R * wTemperate) / sum,
            (c["Polar"].G * wPolar + c["Ocean"].G * wOcean + c["Tundra"].G * wTundra +
             c["Mountain"].G * wMountain + c["Tropical"].G * wTropical + c["Temperate"].G * wTemperate) / sum,
            (c["Polar"].B * wPolar + c["Ocean"].B * wOcean + c["Tundra"].B * wTundra +
             c["Mountain"].B * wMountain + c["Tropical"].B * wTropical + c["Temperate"].B * wTemperate) / sum,
            1f);
    }

    static float Smooth(float x, float edge0, float edge1)
    {
        if (edge0 >= edge1)
            return x >= edge0 ? 1f : 0f;
        float t = Math.Clamp((x - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - 2f * t);
    }
}
