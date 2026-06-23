#include "planet_sdf.h"

#include "picogk_runtime.h"
#include "planet_noise.h"

#include <cmath>

static PlanetSettings g_settings;

void planet_settings_set(const PlanetSettings &p_settings) {
	g_settings = p_settings;
	PlanetNoise::set_seed(p_settings.seed);
	PlanetNoise::init_permutation();
}

const PlanetSettings &planet_settings_get() {
	return g_settings;
}

static float sample_noise_on_sphere(float nx, float ny, float nz) {
	const float R = g_settings.radius_mm;
	const float scale = g_settings.noise_scale;
	return PlanetNoise::warped_bm(nx * R * scale, ny * R * scale, nz * R * scale, g_settings.warp_strength);
}

static PlanetBiome classify_biome(float nx, float ny, float nz, float f_noise) {
	const float f_lat = std::fabs(nz);
	if (f_lat > g_settings.polar_latitude) {
		return BIOME_POLAR;
	}
	if (f_noise < g_settings.ocean_threshold) {
		return BIOME_OCEAN;
	}
	if (f_lat > g_settings.tundra_latitude) {
		return BIOME_TUNDRA;
	}
	if (f_noise > g_settings.mountain_threshold) {
		return BIOME_MOUNTAIN;
	}
	if (f_lat < g_settings.tropical_latitude) {
		return BIOME_TROPICAL;
	}
	return BIOME_TEMPERATE;
}

float planet_sdf_signed_distance(const PGKVector3 *pt) {
	const float R = g_settings.radius_mm;
	const float H_MAX = g_settings.terrain_height_mm;

	float f_r = std::sqrt(pt->x * pt->x + pt->y * pt->y + pt->z * pt->z);
	if (f_r < 0.001f) {
		return -R;
	}

	float f_inv_r = 1.0f / f_r;
	float nx = pt->x * f_inv_r;
	float ny = pt->y * f_inv_r;
	float nz = pt->z * f_inv_r;

	float f_noise = sample_noise_on_sphere(nx, ny, nz);

	float f_lat = std::fabs(nz);
	float f_lat_inf = std::pow(f_lat, 2.5f);
	float f_terrain = f_noise * (1.0f - 0.4f * f_lat_inf);

	float f_ice = std::max(0.0f, f_lat - 0.78f) / 0.22f;
	f_terrain += f_ice * 0.35f;

	float f_surface_r = R + f_terrain * H_MAX;
	return f_r - f_surface_r;
}

float biome_sdf_signed_distance(int biome_id, const PGKVector3 *pt) {
	float f_planet_dist = planet_sdf_signed_distance(pt);
	if (f_planet_dist > 2.0f) {
		return f_planet_dist;
	}

	float f_r = std::sqrt(pt->x * pt->x + pt->y * pt->y + pt->z * pt->z);
	if (f_r < 0.001f) {
		return 1.0f;
	}

	float f_inv_r = 1.0f / f_r;
	float nx = pt->x * f_inv_r;
	float ny = pt->y * f_inv_r;
	float nz = pt->z * f_inv_r;
	float f_noise = sample_noise_on_sphere(nx, ny, nz);

	PlanetBiome e_this = classify_biome(nx, ny, nz, f_noise);
	return (e_this == (PlanetBiome)biome_id) ? f_planet_dist : 1.0f;
}

PGKBBox3 planet_bounding_box() {
	const float b = g_settings.radius_mm + 15.0f;
	PGKBBox3 box;
	box.vecMin = { -b, -b, -b };
	box.vecMax = { b, b, b };
	return box;
}
