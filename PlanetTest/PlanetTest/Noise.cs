// 3D Gradient Noise — proper Perlin-style implementation
// Multi-octave fBm (fractional Brownian motion) for realistic terrain

using System.Numerics;

namespace Leap71.ShapeKernel
{
    public static class Noise
    {
        static int[] P = null!;
        static int _seed = 42;

        static Noise() => RebuildPermutation(42);

        /// <summary>Same seed → same planet. Call before voxelize.</summary>
        public static void SetSeed(int seed)
        {
            if (seed == _seed && P != null)
                return;
            RebuildPermutation(seed);
        }

        public static int GetSeed() => _seed;

        static void RebuildPermutation(int seed)
        {
            _seed = seed;
            int[] src = new int[256];
            for (int i = 0; i < 256; i++) src[i] = i;
            var rng = new Random(seed);
            for (int i = 255; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (src[i], src[j]) = (src[j], src[i]);
            }
            P = new int[512];
            for (int i = 0; i < 512; i++) P[i] = src[i & 255];
        }

        static float Fade(float t) => t * t * t * (t * (t * 6 - 15) + 10);
        static float Lerp(float a, float b, float t) => a + t * (b - a);

        static float Grad(int hash, float x, float y, float z)
        {
            int h = hash & 15;
            float u = h < 8 ? x : y;
            float v = h < 4 ? y : (h == 12 || h == 14 ? x : z);
            return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
        }

        public static float fPerlin(float x, float y, float z)
        {
            int X = (int)MathF.Floor(x) & 255;
            int Y = (int)MathF.Floor(y) & 255;
            int Z = (int)MathF.Floor(z) & 255;
            x -= MathF.Floor(x); y -= MathF.Floor(y); z -= MathF.Floor(z);
            float u = Fade(x), v = Fade(y), w = Fade(z);
            int A  = P[X]+Y,   AA = P[A]+Z, AB = P[A+1]+Z;
            int B  = P[X+1]+Y, BA = P[B]+Z, BB = P[B+1]+Z;
            return Lerp(
                Lerp(Lerp(Grad(P[AA],   x,   y,   z), Grad(P[BA],   x-1, y,   z), u),
                     Lerp(Grad(P[AB],   x,   y-1, z), Grad(P[BB],   x-1, y-1, z), u), v),
                Lerp(Lerp(Grad(P[AA+1], x,   y,   z-1), Grad(P[BA+1], x-1, y,   z-1), u),
                     Lerp(Grad(P[AB+1], x,   y-1, z-1), Grad(P[BB+1], x-1, y-1, z-1), u), v), w);
        }

        // Fractal Brownian Motion — stacks octaves for realistic terrain
        // More octaves = more detail but slower
        public static float fBm(float x, float y, float z,
                                 int nOctaves = 6,
                                 float fLacunarity = 2.0f,
                                 float fGain = 0.5f)
        {
            float fVal    = 0f;
            float fAmp    = 0.5f;
            float fFreq   = 1f;
            float fMax    = 0f;
            for (int i = 0; i < nOctaves; i++)
            {
                fVal += fPerlin(x * fFreq, y * fFreq, z * fFreq) * fAmp;
                fMax += fAmp;
                fAmp  *= fGain;
                fFreq *= fLacunarity;
            }
            return fVal / fMax; // normalised to roughly -1..+1
        }

        // Domain-warped fBm — folds space before sampling, creates natural
        // continent/mountain shapes (no visible repetition)
        public static float fWarpedBm(float x, float y, float z, float fWarpStr = 0.8f)
        {
            // First pass — warp the domain
            float wx = fBm(x + 0.0f, y + 0.0f, z + 0.0f, 4);
            float wy = fBm(x + 5.2f, y + 1.3f, z + 2.8f, 4);
            float wz = fBm(x + 1.7f, y + 9.2f, z + 4.1f, 4);
            // Second pass — sample with warped coordinates
            return fBm(x + fWarpStr * wx,
                       y + fWarpStr * wy,
                       z + fWarpStr * wz, 6);
        }
    }
}
