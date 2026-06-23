#ifndef PICOGK_PLANET_GENERATOR_H
#define PICOGK_PLANET_GENERATOR_H

#include "core/object/ref_counted.h"
#include "core/os/mutex.h"

#include "planet_sdf.h"

class ArrayMesh;

class PicogkPlanetGenerator : public RefCounted {
	GDCLASS(PicogkPlanetGenerator, RefCounted);

public:
	enum BiomeId {
		BIOME_OCEAN = 0,
		BIOME_TROPICAL = 1,
		BIOME_TEMPERATE = 2,
		BIOME_TUNDRA = 3,
		BIOME_MOUNTAIN = 4,
		BIOME_POLAR = 5,
	};

	Ref<ArrayMesh> generate_planet(double radius_mm, double voxel_size_mm, int seed);
	Ref<ArrayMesh> generate_biome_mesh(int biome_id, double radius_mm, double voxel_size_mm, int seed);
	Ref<ArrayMesh> generate_collision_mesh(double radius_mm, double voxel_size_mm, int seed);

	void set_noise_settings(double noise_scale, double warp_strength, double terrain_height);
	void set_biome_thresholds(double ocean_threshold, double mountain_threshold, double polar_latitude);
	void set_reverse_winding(bool enabled);

	String get_last_error() const { return last_error; }

protected:
	static void _bind_methods();

private:
	PlanetSettings settings;
	bool reverse_winding = false;
	String last_error;
	Mutex mutex;

	Ref<ArrayMesh> build_mesh_from_sdf(bool p_biome_mode, int p_biome_id, double radius_mm, double voxel_size_mm, int seed);
};

VARIANT_ENUM_CAST(PicogkPlanetGenerator::BiomeId);

#endif // PICOGK_PLANET_GENERATOR_H
