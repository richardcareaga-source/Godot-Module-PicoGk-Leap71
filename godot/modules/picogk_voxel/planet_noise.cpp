#include "planet_noise.h"

#include <cmath>
#include <cstdlib>

namespace PlanetNoise {

static int P[512];
static int g_seed = 42;
static bool g_ready = false;

static float fade(float t) {
	return t * t * t * (t * (t * 6.0f - 15.0f) + 10.0f);
}

static float lerp(float a, float b, float t) {
	return a + t * (b - a);
}

static float grad(int hash, float x, float y, float z) {
	int h = hash & 15;
	float u = h < 8 ? x : y;
	float v = h < 4 ? y : (h == 12 || h == 14 ? x : z);
	return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
}

void set_seed(int p_seed) {
	g_seed = p_seed;
	g_ready = false;
}

void init_permutation() {
	if (g_ready) {
		return;
	}
	int src[256];
	for (int i = 0; i < 256; i++) {
		src[i] = i;
	}
	// Deterministic shuffle — matches Noise.cs (Random(42) by default).
	std::srand((unsigned)g_seed);
	for (int i = 255; i > 0; i--) {
		int j = std::rand() % (i + 1);
		int tmp = src[i];
		src[i] = src[j];
		src[j] = tmp;
	}
	for (int i = 0; i < 512; i++) {
		P[i] = src[i & 255];
	}
	g_ready = true;
}

float perlin(float x, float y, float z) {
	init_permutation();
	int X = ((int)std::floor(x)) & 255;
	int Y = ((int)std::floor(y)) & 255;
	int Z = ((int)std::floor(z)) & 255;
	x -= std::floor(x);
	y -= std::floor(y);
	z -= std::floor(z);
	float u = fade(x);
	float v = fade(y);
	float w = fade(z);
	int A = P[X] + Y, AA = P[A] + Z, AB = P[A + 1] + Z;
	int B = P[X + 1] + Y, BA = P[B] + Z, BB = P[B + 1] + Z;
	return lerp(
			lerp(lerp(grad(P[AA], x, y, z), grad(P[BA], x - 1, y, z), u),
					lerp(grad(P[AB], x, y - 1, z), grad(P[BB], x - 1, y - 1, z), u), v),
			lerp(lerp(grad(P[AA + 1], x, y, z - 1), grad(P[BA + 1], x - 1, y, z - 1), u),
					lerp(grad(P[AB + 1], x, y - 1, z - 1), grad(P[BB + 1], x - 1, y - 1, z - 1), u), v),
			w);
}

float fbm(float x, float y, float z, int octaves, float lacunarity, float gain) {
	float val = 0.0f;
	float amp = 0.5f;
	float freq = 1.0f;
	float max_val = 0.0f;
	for (int i = 0; i < octaves; i++) {
		val += perlin(x * freq, y * freq, z * freq) * amp;
		max_val += amp;
		amp *= gain;
		freq *= lacunarity;
	}
	return val / max_val;
}

float warped_bm(float x, float y, float z, float warp_strength) {
	float wx = fbm(x + 0.0f, y + 0.0f, z + 0.0f, 4);
	float wy = fbm(x + 5.2f, y + 1.3f, z + 2.8f, 4);
	float wz = fbm(x + 1.7f, y + 9.2f, z + 4.1f, 4);
	return fbm(x + warp_strength * wx, y + warp_strength * wy, z + warp_strength * wz, 6);
}

} // namespace PlanetNoise
