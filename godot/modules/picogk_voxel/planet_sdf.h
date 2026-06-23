#ifndef PLANET_SDF_H
#define PLANET_SDF_H

// Port of PlanetTest/Planet.cs — PlanetSDF + BiomeSDF.

enum PlanetBiome {
	BIOME_OCEAN = 0,
	BIOME_TROPICAL = 1,
	BIOME_TEMPERATE = 2,
	BIOME_TUNDRA = 3,
	BIOME_MOUNTAIN = 4,
	BIOME_POLAR = 5,
	BIOME_COUNT = 6,
};

struct PlanetSettings {
	float radius_mm = 80.0f;
	float terrain_height_mm = 12.0f;
	float noise_scale = 0.028f;
	float warp_strength = 0.8f;
	float ocean_threshold = -0.18f;
	float mountain_threshold = 0.28f;
	float polar_latitude = 0.78f;
	float tundra_latitude = 0.52f;
	float tropical_latitude = 0.32f;
	int seed = 42;
};

void planet_settings_set(const PlanetSettings &p_settings);
const PlanetSettings &planet_settings_get();

float planet_sdf_signed_distance(const struct PGKVector3 *pt);
float biome_sdf_signed_distance(int biome_id, const struct PGKVector3 *pt);

struct PGKBBox3 planet_bounding_box();

#endif // PLANET_SDF_H
