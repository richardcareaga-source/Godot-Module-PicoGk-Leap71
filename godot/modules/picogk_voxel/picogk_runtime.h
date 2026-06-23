#ifndef PICOGK_RUNTIME_H
#define PICOGK_RUNTIME_H

#include "core/error/error_macros.h"
#include "core/string/ustring.h"

// Matches PicoGK Config.strPicoGKLib — picogk.26.1.dll on Windows.
#define PICOGK_DLL_NAME "picogk.26.1.dll"

struct PGKVector3 {
	float x = 0.0f;
	float y = 0.0f;
	float z = 0.0f;
};

struct PGKBBox3 {
	PGKVector3 vecMin;
	PGKVector3 vecMax;
};

typedef void *PGKLibHandle;
typedef void *PGKVoxHandle;
typedef void *PGKMshHandle;

typedef float (*PGKCallbackImplicitDistance)(const PGKVector3 *vec);

class PicogkRuntime {
public:
	static PicogkRuntime &get_singleton();

	bool ensure_loaded();
	bool is_loaded() const { return loaded; }

	PGKLibHandle library_create(float voxel_size_mm);
	void library_destroy(PGKLibHandle h_lib);

	PGKVoxHandle voxels_create(PGKLibHandle h_lib);
	void voxels_destroy(PGKLibHandle h_lib, PGKVoxHandle h_vox);
	void voxels_render_implicit(PGKLibHandle h_lib, PGKVoxHandle h_vox, const PGKBBox3 &bounds, PGKCallbackImplicitDistance cb);

	PGKMshHandle mesh_from_voxels(PGKLibHandle h_lib, PGKVoxHandle h_vox);
	void mesh_destroy(PGKLibHandle h_lib, PGKMshHandle h_msh);
	int mesh_triangle_count(PGKLibHandle h_lib, PGKMshHandle h_msh);
	void mesh_get_triangle_v(PGKLibHandle h_lib, PGKMshHandle h_msh, int index, PGKVector3 &a, PGKVector3 &b, PGKVector3 &c);

	String get_last_error() const { return last_error; }

private:
	PicogkRuntime() = default;

	bool loaded = false;
	String last_error;

#ifdef WINDOWS_ENABLED
	void *dll_handle = nullptr;
#endif

	using FnLibCreate = PGKLibHandle (*)(float);
	using FnLibDestroy = void (*)(PGKLibHandle);
	using FnVoxCreate = PGKVoxHandle (*)(PGKLibHandle);
	using FnVoxDestroy = void (*)(PGKLibHandle, PGKVoxHandle);
	using FnVoxRender = void (*)(PGKLibHandle, PGKVoxHandle, const PGKBBox3 *, PGKCallbackImplicitDistance);
	using FnMshFromVox = PGKMshHandle (*)(PGKLibHandle, PGKVoxHandle);
	using FnMshDestroy = void (*)(PGKLibHandle, PGKMshHandle);
	using FnMshTriCount = int (*)(PGKLibHandle, PGKMshHandle);
	using FnMshGetTriV = void (*)(PGKLibHandle, PGKMshHandle, int, PGKVector3 *, PGKVector3 *, PGKVector3 *);

	FnLibCreate fn_lib_create = nullptr;
	FnLibDestroy fn_lib_destroy = nullptr;
	FnVoxCreate fn_vox_create = nullptr;
	FnVoxDestroy fn_vox_destroy = nullptr;
	FnVoxRender fn_vox_render = nullptr;
	FnMshFromVox fn_msh_from_vox = nullptr;
	FnMshDestroy fn_msh_destroy = nullptr;
	FnMshTriCount fn_msh_tri_count = nullptr;
	FnMshGetTriV fn_msh_get_tri_v = nullptr;

	bool load_functions();
};

#endif // PICOGK_RUNTIME_H
