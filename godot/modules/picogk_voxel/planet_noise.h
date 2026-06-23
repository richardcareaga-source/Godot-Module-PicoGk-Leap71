#ifndef PLANET_NOISE_H
#define PLANET_NOISE_H

// Port of PlanetTest/Noise.cs — keep algorithm identical for visual match.

namespace PlanetNoise {

void set_seed(int p_seed);
void init_permutation();

float perlin(float x, float y, float z);
float fbm(float x, float y, float z, int octaves = 6, float lacunarity = 2.0f, float gain = 0.5f);
float warped_bm(float x, float y, float z, float warp_strength = 0.8f);

} // namespace PlanetNoise

#endif // PLANET_NOISE_H
