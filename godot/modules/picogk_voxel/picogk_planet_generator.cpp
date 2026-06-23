#include "picogk_planet_generator.h"

#include "picogk_runtime.h"
#include "planet_sdf.h"

#include "core/object/class_db.h"
#include "scene/resources/array_mesh.h"
#include "scene/resources/mesh.h"

#include <cmath>

static int g_callback_biome = -1;

static float _cb_planet_sdf(const PGKVector3 *pt) {
	return planet_sdf_signed_distance(pt);
}

static float _cb_biome_sdf(const PGKVector3 *pt) {
	return biome_sdf_signed_distance(g_callback_biome, pt);
}

void PicogkPlanetGenerator::_bind_methods() {
	ClassDB::bind_method(D_METHOD("generate_planet", "radius_mm", "voxel_size_mm", "seed"), &PicogkPlanetGenerator::generate_planet);
	ClassDB::bind_method(D_METHOD("generate_biome_mesh", "biome_id", "radius_mm", "voxel_size_mm", "seed"), &PicogkPlanetGenerator::generate_biome_mesh);
	ClassDB::bind_method(D_METHOD("generate_collision_mesh", "radius_mm", "voxel_size_mm", "seed"), &PicogkPlanetGenerator::generate_collision_mesh);
	ClassDB::bind_method(D_METHOD("set_noise_settings", "noise_scale", "warp_strength", "terrain_height"), &PicogkPlanetGenerator::set_noise_settings);
	ClassDB::bind_method(D_METHOD("set_biome_thresholds", "ocean_threshold", "mountain_threshold", "polar_latitude"), &PicogkPlanetGenerator::set_biome_thresholds);
	ClassDB::bind_method(D_METHOD("set_reverse_winding", "enabled"), &PicogkPlanetGenerator::set_reverse_winding);
	ClassDB::bind_method(D_METHOD("get_last_error"), &PicogkPlanetGenerator::get_last_error);

	BIND_ENUM_CONSTANT(BIOME_OCEAN);
	BIND_ENUM_CONSTANT(BIOME_TROPICAL);
	BIND_ENUM_CONSTANT(BIOME_TEMPERATE);
	BIND_ENUM_CONSTANT(BIOME_TUNDRA);
	BIND_ENUM_CONSTANT(BIOME_MOUNTAIN);
	BIND_ENUM_CONSTANT(BIOME_POLAR);
}

String PicogkPlanetGenerator::get_last_error() const {
	return last_error;
}

void PicogkPlanetGenerator::set_noise_settings(double noise_scale, double warp_strength, double terrain_height) {
	settings.noise_scale = (float)noise_scale;
	settings.warp_strength = (float)warp_strength;
	settings.terrain_height_mm = (float)terrain_height;
	planet_settings_set(settings);
}

void PicogkPlanetGenerator::set_biome_thresholds(double ocean_threshold, double mountain_threshold, double polar_latitude) {
	settings.ocean_threshold = (float)ocean_threshold;
	settings.mountain_threshold = (float)mountain_threshold;
	settings.polar_latitude = (float)polar_latitude;
	planet_settings_set(settings);
}

void PicogkPlanetGenerator::set_reverse_winding(bool enabled) {
	reverse_winding = enabled;
}

Ref<ArrayMesh> PicogkPlanetGenerator::generate_planet(double radius_mm, double voxel_size_mm, int seed) {
	return build_mesh_from_sdf(false, -1, radius_mm, voxel_size_mm, seed);
}

Ref<ArrayMesh> PicogkPlanetGenerator::generate_biome_mesh(int biome_id, double radius_mm, double voxel_size_mm, int seed) {
	return build_mesh_from_sdf(true, biome_id, radius_mm, voxel_size_mm, seed);
}

Ref<ArrayMesh> PicogkPlanetGenerator::generate_collision_mesh(double radius_mm, double voxel_size_mm, int seed) {
	return generate_planet(radius_mm, voxel_size_mm, seed);
}

Ref<ArrayMesh> PicogkPlanetGenerator::build_mesh_from_sdf(bool p_biome_mode, int p_biome_id, double radius_mm, double voxel_size_mm, int seed) {
	MutexLock lock(mutex);

	settings.radius_mm = (float)radius_mm;
	settings.seed = seed;
	planet_settings_set(settings);

	PicogkRuntime &rt = PicogkRuntime::get_singleton();
	if (!rt.ensure_loaded()) {
		last_error = rt.get_last_error();
		return Ref<ArrayMesh>();
	}

	PGKLibHandle h_lib = rt.library_create((float)voxel_size_mm);
	if (!h_lib) {
		last_error = "Library_hCreateInstance failed.";
		return Ref<ArrayMesh>();
	}

	PGKVoxHandle h_vox = rt.voxels_create(h_lib);
	if (!h_vox) {
		rt.library_destroy(h_lib);
		last_error = "Voxels_hCreate failed.";
		return Ref<ArrayMesh>();
	}

	PGKBBox3 bounds = planet_bounding_box();
	if (p_biome_mode) {
		g_callback_biome = p_biome_id;
		rt.voxels_render_implicit(h_lib, h_vox, bounds, _cb_biome_sdf);
	} else {
		rt.voxels_render_implicit(h_lib, h_vox, bounds, _cb_planet_sdf);
	}

	PGKMshHandle h_msh = rt.mesh_from_voxels(h_lib, h_vox);
	rt.voxels_destroy(h_lib, h_vox);

	if (!h_msh) {
		rt.library_destroy(h_lib);
		last_error = "Mesh_hCreateFromVoxels failed.";
		ERR_FAIL_COND_V(true, Ref<ArrayMesh>());
	}

	const int tri_count = rt.mesh_triangle_count(h_lib, h_msh);
	PackedVector3Array vertices;
	PackedVector3Array normals;
	vertices.resize(tri_count * 3);
	normals.resize(tri_count * 3);

	const float mm_to_m = 0.001f;

	for (int i = 0; i < tri_count; i++) {
		PGKVector3 a, b, c;
		rt.mesh_get_triangle_v(h_lib, h_msh, i, a, b, c);

		if (reverse_winding) {
			PGKVector3 tmp = b;
			b = c;
			c = tmp;
		}

		Vector3 va(a.x * mm_to_m, a.y * mm_to_m, a.z * mm_to_m);
		Vector3 vb(b.x * mm_to_m, b.y * mm_to_m, b.z * mm_to_m);
		Vector3 vc(c.x * mm_to_m, c.y * mm_to_m, c.z * mm_to_m);
		Vector3 n = (vb - va).cross(vc - va).normalized();

		const int base = i * 3;
		vertices.set(base + 0, va);
		vertices.set(base + 1, vb);
		vertices.set(base + 2, vc);
		normals.set(base + 0, n);
		normals.set(base + 1, n);
		normals.set(base + 2, n);
	}

	rt.mesh_destroy(h_lib, h_msh);
	rt.library_destroy(h_lib);

	Ref<ArrayMesh> mesh;
	mesh.instantiate();
	Array arrays;
	arrays.resize(Mesh::ARRAY_MAX);
	arrays[Mesh::ARRAY_VERTEX] = vertices;
	arrays[Mesh::ARRAY_NORMAL] = normals;
	mesh->add_surface_from_arrays(Mesh::PRIMITIVE_TRIANGLES, arrays);

	last_error = String();
	return mesh;
}
