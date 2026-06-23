#include "picogk_runtime.h"

#include <type_traits>

#ifdef WINDOWS_ENABLED
#include <windows.h>
#endif

PicogkRuntime &PicogkRuntime::get_singleton() {
	static PicogkRuntime instance;
	return instance;
}

bool PicogkRuntime::ensure_loaded() {
	if (loaded) {
		return true;
	}
#ifdef WINDOWS_ENABLED
	if (dll_handle) {
		return load_functions();
	}

	const char *dll_name = PICOGK_DLL_NAME;
	dll_handle = (void *)LoadLibraryA(dll_name);
	if (!dll_handle) {
#ifdef PICOGK_NATIVE_DIR
		String path = String(PICOGK_NATIVE_DIR) + "/" + dll_name;
		dll_handle = (void *)LoadLibraryW(path.to_wcstring());
#endif
	}
	if (!dll_handle) {
		last_error = vformat("Failed to load %s (error %lu). Copy PicoGK native/win-x64 DLLs next to Godot executable or set PICOGK_NATIVE_DIR.", dll_name, (unsigned long)GetLastError());
		return false;
	}
	return load_functions();
#else
	last_error = "picogk_voxel: PicoGK native runtime is only wired for Windows in this module.";
	return false;
#endif
}

bool PicogkRuntime::load_functions() {
#ifdef WINDOWS_ENABLED
	auto resolve = [&](auto &fn, const char *export_name) -> bool {
		fn = (std::remove_reference_t<decltype(fn)>)(GetProcAddress((HMODULE)dll_handle, export_name));
		if (!fn) {
			last_error = vformat("Missing export: %s", export_name);
			return false;
		}
		return true;
	};

	if (!resolve(fn_lib_create, "Library_hCreateInstance")) {
		return false;
	}
	if (!resolve(fn_lib_destroy, "Library_DestroyInstance")) {
		return false;
	}
	if (!resolve(fn_vox_create, "Voxels_hCreate")) {
		return false;
	}
	if (!resolve(fn_vox_destroy, "Voxels_Destroy")) {
		return false;
	}
	if (!resolve(fn_vox_render, "Voxels_RenderImplicit")) {
		return false;
	}
	if (!resolve(fn_msh_from_vox, "Mesh_hCreateFromVoxels")) {
		return false;
	}
	if (!resolve(fn_msh_destroy, "Mesh_Destroy")) {
		return false;
	}
	if (!resolve(fn_msh_tri_count, "Mesh_nTriangleCount")) {
		return false;
	}
	if (!resolve(fn_msh_get_tri_v, "Mesh_GetTriangleV")) {
		return false;
	}

	loaded = true;
	return true;
#else
	return false;
#endif
}

PGKLibHandle PicogkRuntime::library_create(float voxel_size_mm) {
	ERR_FAIL_COND_V(!ensure_loaded(), nullptr);
	return fn_lib_create(voxel_size_mm);
}

void PicogkRuntime::library_destroy(PGKLibHandle h_lib) {
	if (fn_lib_destroy && h_lib) {
		fn_lib_destroy(h_lib);
	}
}

PGKVoxHandle PicogkRuntime::voxels_create(PGKLibHandle h_lib) {
	ERR_FAIL_COND_V(!h_lib, nullptr);
	return fn_vox_create(h_lib);
}

void PicogkRuntime::voxels_destroy(PGKLibHandle h_lib, PGKVoxHandle h_vox) {
	if (fn_vox_destroy && h_lib && h_vox) {
		fn_vox_destroy(h_lib, h_vox);
	}
}

void PicogkRuntime::voxels_render_implicit(PGKLibHandle h_lib, PGKVoxHandle h_vox, const PGKBBox3 &bounds, PGKCallbackImplicitDistance cb) {
	ERR_FAIL_COND(!h_lib || !h_vox || !cb);
	fn_vox_render(h_lib, h_vox, &bounds, cb);
}

PGKMshHandle PicogkRuntime::mesh_from_voxels(PGKLibHandle h_lib, PGKVoxHandle h_vox) {
	ERR_FAIL_COND_V(!h_lib || !h_vox, nullptr);
	return fn_msh_from_vox(h_lib, h_vox);
}

void PicogkRuntime::mesh_destroy(PGKLibHandle h_lib, PGKMshHandle h_msh) {
	if (fn_msh_destroy && h_lib && h_msh) {
		fn_msh_destroy(h_lib, h_msh);
	}
}

int PicogkRuntime::mesh_triangle_count(PGKLibHandle h_lib, PGKMshHandle h_msh) {
	ERR_FAIL_COND_V(!h_lib || !h_msh, 0);
	return fn_msh_tri_count(h_lib, h_msh);
}

void PicogkRuntime::mesh_get_triangle_v(PGKLibHandle h_lib, PGKMshHandle h_msh, int index, PGKVector3 &a, PGKVector3 &b, PGKVector3 &c) {
	ERR_FAIL_COND(!h_lib || !h_msh);
	fn_msh_get_tri_v(h_lib, h_msh, index, &a, &b, &c);
}
