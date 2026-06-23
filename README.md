# PicoGK + Custom Godot 4.x .NET Editor — Procedural Voxel Planet

This repository integrates **PicoGK** into a **custom-built Godot 4.x editor** (with .NET/Mono) via a native engine module `picogk_voxel`. Planet generation logic is **not reinvented** — it is ported from the working reference in **`PlanetTest/`**.

**Master reference (~1200 lines — PicoGK + ShapeKernel + LatticeLibrary + Godot merge, full API appendix):** **[MERGED_LIBRARIES_README.md](MERGED_LIBRARIES_README.md)**  

**Also:** [GAME_ROADMAP.md](GAME_ROADMAP.md) (game phases) · [PROCEDURAL_PLATFORM.md](PROCEDURAL_PLATFORM.md) (caching, threads, chunks) · Regenerate API appendix: `python _build_merged_readme.py`

---

## Source of truth: `PlanetTest/`

| File | Role |
|------|------|
| `PlanetTest/PlanetTest/Noise.cs` | 3D Perlin, fBm, domain-warped fBm (seed 42) |
| `PlanetTest/PlanetTest/Planet.cs` | `PlanetSDF`, `BiomeSDF`, `Planet` — spherical SDF + biome masks |
| `PlanetTest/PlanetTest/Program.cs` | Voxelise per biome, preview colours, export full STL |

### Voxel structure (not a custom octree)

PlanetTest uses PicoGK **`Voxels`**, backed by **OpenVDB** inside the native `picogk.26.1` runtime. Flow:

1. `new Voxels(implicit, bbox)` → `RenderImplicit` samples the SDF on a regular grid at `Library` voxel size.
2. `new Mesh(voxels)` → `Mesh_hCreateFromVoxels` — marching-cubes-style surface mesh.
3. Per-biome meshes: `BiomeSDF` returns the planet SDF only inside the target biome, `+1` outside (empty).

There is **no** separate octree implementation in PlanetTest.

### Default planet parameters (PlanetTest)

| Constant | Value | Meaning |
|----------|-------|---------|
| `PlanetSDF.R` | 80 mm | Base sphere radius |
| `PlanetSDF.H_MAX` | 12 mm | Max terrain relief |
| `NOISE_SCALE` | 0.028 | Surface noise frequency |
| Voxel size | 1.2 mm | `Library.Go(1.2f, ...)` in `Program.cs` |

### Biomes (`Planet.cs`)

| ID | Enum | Classification |
|----|------|----------------|
| 0 | Ocean | `fNoise < -0.18` |
| 1 | Tropical | `\|lat\| < 0.32` (after above) |
| 2 | Temperate | default belt |
| 3 | Tundra | `\|lat\| > 0.52` |
| 4 | Mountain | `fNoise > 0.28` |
| 5 | Polar | `\|lat\| > 0.78` |

Visual colours in PlanetTest previews: blue ocean, lemongrass tropical, green temperate, rock tundra, gray mountain, frozen polar.

### Coordinate units

- **PicoGK**: millimeters (mm).
- **Godot**: meters (m). Module divides positions by **1000** when building `ArrayMesh`.
- Default demo: `radius_mm = 80` → **0.08 m** planet radius in Godot.

### Axis / winding

Mesh vertices are taken from PicoGK `Mesh_GetTriangleV` in mm, scaled to m. If normals look inverted, toggle `reverse_winding` in the generator (swaps triangle vertex order).

---

## Repository layout

```
PicoGK Procedural World Generation/
├── PicoGK/                    # PicoGK C# + native runtime (clone)
├── LEAP71_ShapeKernel/        # ShapeKernel (LocalFrame, Sh.*)
├── LEAP71_LatticeLibrary/     # Optional lattice examples
├── PlanetTest/                # ★ Reference planet implementation
├── godot/
│   └── modules/
│       └── picogk_voxel/      # ★ Copy into Godot source tree
├── godot_demo/                # Demo scene + test script
└── README.md                  # This file
```

---

## Step 1 — Clone Godot (stable 4.x)

```powershell
cd "c:\Users\richa\OneDrive\Desktop\PicoGK Procedural World Generation"
git clone https://github.com/godotengine/godot.git godot
cd godot
git fetch --tags
# Example: latest 4.4 stable (check https://github.com/godotengine/godot/releases)
git checkout 4.4-stable
git submodule update --init --recursive
```

Copy the module into the Godot tree:

```powershell
Copy-Item -Recurse "..\godot\modules\picogk_voxel" "modules\picogk_voxel"
```

Or symlink:

```powershell
New-Item -ItemType Junction -Path "modules\picogk_voxel" -Target "..\..\godot\modules\picogk_voxel"
```

---

## Step 2 — Windows build prerequisites

| Tool | Purpose |
|------|---------|
| **Visual Studio 2022** | Desktop development with C++ |
| **Python 3** | SCons |
| **SCons** | `pip install scons` |
| **.NET SDK** | Match Godot tag (e.g. **.NET 8** for Godot 4.4) |
| **Git** | Source + submodules |

Enable **Mono/.NET** in Godot build (see Step 3).

---

## Step 3 — PicoGK native DLLs

From your PicoGK checkout, ensure runtime DLLs exist:

```
PicoGK/native/win-x64/
  picogk.26.1.dll
  blosc.dll
  lz4.dll
  tbb12.dll
  zlib1.dll
  zstd.dll
```

If missing, build or download PicoGK per [picogk.org](https://picogk.org).

Set environment variable before building/running Godot (optional):

```powershell
$env:PICOGK_NATIVE_DIR = "C:\...\PicoGK\native\win-x64"
```

After building the editor, **copy all DLLs** next to:

- `godot/bin/godot.windows.editor.x86_64.mono.exe` (custom editor)
- Exported game executable

The module `SCsub` post-step can copy DLLs when `PICOGK_NATIVE_DIR` is set.

---

## Step 4 — Build custom Godot .NET editor

```powershell
cd godot
scons platform=windows target=editor module_mono_enabled=yes
```

Generate .NET glue (Godot 4.x; exact target name may vary by version):

```powershell
.\bin\godot.windows.editor.x86_64.mono.exe --headless --generate-mono-glue modules/mono/glue
# Or build glue via scons target documented for your tag, e.g.:
# scons platform=windows target=editor module_mono_enabled=yes modules=mono
```

Confirm the editor starts and can create a **C#** project.

---

## Step 5 — Clone PicoGK dependencies (already in this workspace)

```powershell
# From repo root (if not present):
git clone https://github.com/leap71/PicoGK
git clone https://github.com/leap71/LEAP71_ShapeKernel
git clone https://github.com/leap71/LEAP71_LatticeLibrary
```

Planet generation for the Godot module uses the **C++ port** of `PlanetTest` noise/SDF logic plus **direct calls** to `picogk.26.1.dll` (same entry points as `PicoGK/Internals/Interop.cs`). It does **not** reference PicoGK C# from game scripts.

---

## Step 6 — Module `picogk_voxel`

Location: `godot/modules/picogk_voxel/`

| File | Purpose |
|------|---------|
| `config.py` | Enable module, `PICOGK_NATIVE_DIR` |
| `SCsub` | Compile sources, optional DLL copy |
| `register_types.*` | Register `PicogkPlanetGenerator` |
| `picogk_runtime.*` | Load `picogk.26.1.dll`, P/Invoke-equivalent C API |
| `planet_noise.*` | Port of `Noise.cs` |
| `planet_sdf.*` | Port of `PlanetSDF` / `BiomeSDF` |
| `picogk_planet_generator.*` | Godot `RefCounted` API → `ArrayMesh` |
| `picogk_voxel.*` | Module hooks |

Rebuild Godot after adding the module:

```powershell
scons platform=windows target=editor module_mono_enabled=yes -j8
```

---

## Step 7 — Godot API (`PicogkPlanetGenerator`)

**ClassDB name:** `PicogkPlanetGenerator` (extends `RefCounted`)

| Method | Description |
|--------|-------------|
| `generate_planet(radius_mm, voxel_size_mm, seed)` | Full planet `ArrayMesh` |
| `generate_biome_mesh(biome_id, radius_mm, voxel_size_mm, seed)` | Single biome shell |
| `generate_collision_mesh(radius_mm, voxel_size_mm, seed)` | Full planet (collision) |
| `set_noise_settings(noise_scale, warp_strength, terrain_height)` | Maps to `NOISE_SCALE`, warp, `H_MAX` |
| `set_biome_thresholds(ocean_threshold, mountain_threshold, polar_latitude)` | Biome latitude/noise cuts |
| `set_reverse_winding(enabled)` | Fix inside-out meshes |

Generation runs on a **worker thread** (does not block the main thread). **No** PicoGK OpenGL viewer is created.

---

## Step 8 — Demo project

Open `godot_demo/` with your **custom** Godot editor binary.

```
godot_demo/
  project.godot
  scenes/World.tscn
  scripts/planet_world.gd      # Builds biome MeshInstance3D nodes
  scripts/planet_test.gd       # Automated generation test
```

Run scene **World** or attach `planet_test.gd` to verify all biomes + collision.

---

## Step 9 — Testing checklist

1. Launch custom `godot.*.mono.exe`.
2. **Project → Project Settings → Modules** — confirm `picogk_voxel` built.
3. Editor console: `ClassDB.class_exists("PicogkPlanetGenerator")` → `true`.
4. Run `godot_demo` — six biome meshes + collision.
5. No `picogk` viewer window / ImGui overlay.
6. Editor remains responsive during generation (worker thread).

---

## PicoGK API access notes

| API | Access in C# | Godot module approach |
|-----|----------------|------------------------|
| `IImplicit.fSignedDistance` | Public interface | C++ callbacks (`planet_sdf.cpp`) |
| `Voxels` + `RenderImplicit` | Public | `Voxels_RenderImplicit` via DLL |
| `Mesh` from voxels | Public `new Mesh(voxels)` | `Mesh_hCreateFromVoxels` |
| `Library.Go` + `Viewer` | Opens viewer | **Not used** — `Library_hCreateInstance` only |
| `Sh.PreviewVoxels` | ShapeKernel viewer | **Not used** in Godot |
| `Voxels` internal handles | `internal` constructors | Native handles via DLL only |

No PicoGK C# **internal** APIs were required for mesh generation. The module uses the **published C ABI** mirrored in `Interop.cs`. If your PicoGK version differs from `picogk.26.1`, update `PICOGK_DLL_NAME` in `picogk_runtime.h`.

---

## Custom build command (summary)

```powershell
cd godot
$env:PICOGK_NATIVE_DIR = "..\PicoGK\native\win-x64"
scons platform=windows target=editor module_mono_enabled=yes -j8
Copy-Item "$env:PICOGK_NATIVE_DIR\*.dll" "bin\"
.\bin\godot.windows.editor.x86_64.mono.exe --path ..\godot_demo
```

---

## Matching PlanetTest output

Keep defaults aligned with PlanetTest unless you intentionally change sliders:

- `radius_mm = 80`, `voxel_size_mm = 1.2`, `seed = 42`
- `noise_scale = 0.028`, `warp_strength = 0.8`, `terrain_height = 12`
- `ocean_threshold = -0.18`, `mountain_threshold = 0.28`, `polar_latitude = 0.78`

Compare exported STL from PlanetTest (`Desktop/PlanetTest_Output/Planet.STL`) with Godot collision mesh scale in meters.

---

## Troubleshooting

| Issue | Fix |
|-------|-----|
| `Failed to load picogk.26.1.dll` | Copy `native/win-x64/*.dll` next to Godot exe; set `PICOGK_NATIVE_DIR` |
| Empty mesh | Increase `radius_mm` or decrease `voxel_size_mm`; check log for PicoGK errors |
| Editor freeze | Ensure you use async/worker path in demo scripts |
| Wrong scale | Remember mm → m (/1000) |
| Module not found | Folder must be `godot/modules/picogk_voxel` with `config.py` |

---

## License

Respect licenses of Godot (MIT), PicoGK (Apache-2.0), LEAP71 ShapeKernel (Apache-2.0), and your project terms.
#   G o d o t - M o d u l e - P i c o G k - L e a p 7 1  
 