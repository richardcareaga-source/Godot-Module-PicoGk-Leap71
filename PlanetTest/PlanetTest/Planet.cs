// Procedural planet — proper domain-warped fBm terrain
// Biomes determined by BOTH latitude AND elevation (like a real planet)

using PicoGK;
using System.Numerics;

namespace Leap71.ShapeKernel
{
    public enum Biome { Ocean, Tropical, Temperate, Tundra, Polar, Mountain }

    // Planet SDF — evaluates terrain at every point in space
    public class PlanetSDF : IImplicit
    {
        public static float R      = 80f;   // base radius mm (set before generate)
        public const float H_MAX   = 12f;   // max terrain relief mm
        const float NOISE_SCALE    = 0.028f; // spatial frequency of continents

        public float fSignedDistance(in Vector3 pt)
        {
            float fR = pt.Length();
            if (fR < 0.001f) return -R;

            // Unit vector on sphere surface
            float fInvR = 1f / fR;
            float nx = pt.X * fInvR;
            float ny = pt.Y * fInvR;
            float nz = pt.Z * fInvR;

            // Sample warped fBm on sphere surface
            float fNoise = Noise.fWarpedBm(
                nx * R * NOISE_SCALE,
                ny * R * NOISE_SCALE,
                nz * R * NOISE_SCALE);

            // Latitude (0 at equator, 1 at pole)
            float fLat    = MathF.Abs(nz);
            float fLatInf = MathF.Pow(fLat, 2.5f); // stronger at poles

            // Blend terrain with polar flattening
            float fTerrain = fNoise * (1f - 0.4f * fLatInf);

            // Polar ice raises the surface near poles
            float fIce = MathF.Max(0f, fLat - 0.78f) / 0.22f; // 0→1 over last 12°
            fTerrain += fIce * 0.35f;

            float fSurfaceR = R + fTerrain * H_MAX;
            return fR - fSurfaceR;
        }

        // Classify biome at a surface point (used for colour splitting)
        public static Biome eBiome(Vector3 ptUnit, float fNoise)
        {
            float fLat = MathF.Abs(ptUnit.Z);

            if (fLat > 0.78f) return Biome.Polar;
            if (fNoise < -0.18f) return Biome.Ocean;
            if (fLat > 0.52f) return Biome.Tundra;
            if (fNoise > 0.28f) return Biome.Mountain;
            if (fLat < 0.32f) return Biome.Tropical;
            return Biome.Temperate;
        }
    }

    // Per-biome implicit — returns SDF only inside the full planet AND in this biome
    public class BiomeSDF : IImplicit
    {
        readonly PlanetSDF m_oPlanet;
        readonly Biome     m_eBiome;
        const float NOISE_SCALE = 0.028f;

        public BiomeSDF(PlanetSDF oPlanet, Biome eBiome)
        {
            m_oPlanet = oPlanet;
            m_eBiome  = eBiome;
        }

        public float fSignedDistance(in Vector3 pt)
        {
            float fPlanetDist = m_oPlanet.fSignedDistance(pt);
            if (fPlanetDist > 2f) return fPlanetDist; // outside planet — skip

            float fR = pt.Length();
            if (fR < 0.001f) return 1f;

            float fInvR = 1f / fR;
            float nx = pt.X * fInvR, ny = pt.Y * fInvR, nz = pt.Z * fInvR;

            float fNoise = Noise.fWarpedBm(
                nx * PlanetSDF.R * NOISE_SCALE,
                ny * PlanetSDF.R * NOISE_SCALE,
                nz * PlanetSDF.R * NOISE_SCALE);

            Biome eThis = PlanetSDF.eBiome(new Vector3(nx, ny, nz), fNoise);

            // Inside this biome: return planet SDF (negative = solid)
            // Outside this biome: return positive (empty)
            return (eThis == m_eBiome) ? fPlanetDist : 1f;
        }
    }

    public class Planet
    {
        readonly LocalFrame m_oFrame;
        public Planet(LocalFrame oFrame) { m_oFrame = oFrame; }

        static BBox3 oBBox(Vector3 vC)
        {
            float b = PlanetSDF.R + 15f;
            return new BBox3(new Vector3(vC.X-b, vC.Y-b, vC.Z-b),
                             new Vector3(vC.X+b, vC.Y+b, vC.Z+b));
        }

        public Voxels voxFull()
        {
            return new Voxels(new PlanetSDF(), oBBox(m_oFrame.vecGetPosition()));
        }

        public Voxels voxBiome(Biome eBiome)
        {
            var oSDF = new PlanetSDF();
            return new Voxels(new BiomeSDF(oSDF, eBiome),
                              oBBox(m_oFrame.vecGetPosition()));
        }
    }
}
