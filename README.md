# PicoGK + Godot 4.x .NET — Procedural Voxel Planet

This project integrates **PicoGK** into a **custom-built Godot 4.x editor** with .NET support through a native engine module named `picogk_voxel`.

Planet generation is **not reinvented**. The Godot module ports the working reference implementation from `PlanetTest/` into native C++ and exposes it to Godot as `PicogkPlanetGenerator`.

**Master reference:** [MERGED_LIBRARIES_README.md](MERGED_LIBRARIES_README.md)
Includes PicoGK, ShapeKernel, LatticeLibrary, Godot integration notes, and the API appendix.

**Related docs:**

- [GAME_ROADMAP.md](GAME_ROADMAP.md) — game phases
- [PROCEDURAL_PLATFORM.md](PROCEDURAL_PLATFORM.md) — caching, threads, and chunks
- Regenerate the API appendix with: `python _build_merged_readme.py`

---

## Source of truth: `PlanetTest/`

| File | Role |
|------|------|
| `PlanetTest/PlanetTest/Noise.cs` | 3D Perlin noise, fBm, and domain-warped fBm using seed `42` |
| `PlanetTest/PlanetTest/Planet.cs` | `PlanetSDF`, `BiomeSDF`, and `Planet` — spherical SDF plus biome masks |
| `PlanetTest/PlanetTest/Program.cs` | Voxelizes each biome, creates preview colors, and exports the full STL |

### Voxel structure

`PlanetTest` uses PicoGK **`Voxels`**, backed by **OpenVDB** inside the native `picogk.26.1` runtime.

Flow:

1. `new Voxels(implicit, bbox)` samples the SDF on a regular grid at the PicoGK `Library` voxel size.
2. `new Mesh(voxels)` calls the PicoGK voxel-to-mesh path, similar to marching cubes.
3. Per-biome meshes use `BiomeSDF`, which returns the planet SDF only inside the selected biome and returns `+1` outside the biome so empty space is produced.

There is **no separate custom octree** in `PlanetTest`.

### Default planet parameters

| Constant | Value | Meaning |
|----------|-------|---------|
| `PlanetSDF.R` | `80 mm` | Base sphere radius |
| `PlanetSDF.H_MAX` | `12 mm` | Maximum terrain relief |
| `NOISE_SCALE` | `0.028` | Surface noise frequency |
| Voxel size | `1.2 mm` | `Library.Go(1.2f, ...)` in `Program.cs` |

### Biomes

| ID | Enum | Classification |
|----|------|----------------|
| `0` | `Ocean` | `fNoise < -0.18` |
| `1` | `Tropical` | `abs(lat) < 0.32` after the ocean test |
| `2` | `Temperate` | Default belt |
| `3` | `Tundra` | `abs(lat) > 0.52` |
| `4` | `Mountain` | `fNoise > 0.28` |
| `5` | `Polar` | `abs(lat) > 0.78` |

PlanetTest preview colors:

- Ocean: blue
- Tropical: lemongrass
- Temperate: green
- Tundra: rock
- Mountain: gray
- Polar: frozen/ice color

### Coordinate units

- **PicoGK:** millimeters (`mm`)
- **Godot:** meters (`m`)
- The module divides PicoGK vertex positions by **1000** when building the Godot `ArrayMesh`.
- Default demo scale: `radius_mm = 80` becomes a `0.08 m` planet radius in Godot.

### Axis and triangle winding

Mesh vertices are read from PicoGK in millimeters, converted to meters, and written into a Godot `ArrayMesh`.

If the mesh appears inside-out or the normals look inverted, enable `reverse_winding` on the generator. This swaps the triangle vertex order.

---

## Recommended repository layout

Do **not** clone Godot into the same folder that stores your module source unless you already know the paths are correct. Keep the custom module source separate from the Godot engine checkout.

Recommended layout:

```text
PicoGK Procedural World Generation/
├── PicoGK/                         # PicoGK C# + native runtime clone
├── LEAP71_ShapeKernel/             # ShapeKernel clone
├── LEAP71_LatticeLibrary/          # Optional lattice examples
├── PlanetTest/                     # Reference planet implementation
├── modules/
│   └── picogk_voxel/               # Your Godot engine module source
├── godot_engine/                   # Godot source checkout
├── godot_demo/                     # Demo scene and test scripts
├── MERGED_LIBRARIES_README.md
└── README.md
```

The important part is this:

- `modules/picogk_voxel/` is your module source.
- `godot_engine/` is the Godot source tree you build.
- Before building, copy or link `modules/picogk_voxel/` into `godot_engine/modules/picogk_voxel/`.

---

## Step 1 — Clone Godot

From the repository root:

```powershell
cd "C:\Users\richa\OneDrive\Desktop\PicoGK Procedural World Generation"
git clone https://github.com/godotengine/godot.git godot_engine
cd godot_engine
git fetch --tags

# Use the Godot 4.x tag your project targets.
# Example:
git checkout 4.4-stable

git submodule update --init --recursive
```

---

## Step 2 — Add the PicoGK module to Godot

From the repository root, copy the module into the Godot engine source tree:

```powershell
Copy-Item -Recurse ".\modules\picogk_voxel" ".\godot_engine\modules\picogk_voxel"
```

Or create a Windows junction so the Godot checkout points directly at your module source:

```powershell
New-Item -ItemType Junction `
  -Path ".\godot_engine\modules\picogk_voxel" `
  -Target ".\modules\picogk_voxel"
```

Use the junction during development so edits to `modules/picogk_voxel/` are immediately seen by the Godot build.

---

## Step 3 — Windows build prerequisites

| Tool | Purpose |
|------|---------|
| **Visual Studio 2022** | C++ compiler and Windows build tools |
| **Python 3** | Required by SCons |
| **SCons** | Godot build system; install with `pip install scons` |
| **.NET SDK** | Required for Godot .NET / C# support |
| **Git** | Source checkout and submodules |

Install the Visual Studio workload **Desktop development with C++**.

---

## Step 4 — PicoGK native DLLs

Confirm these DLLs exist in your PicoGK checkout:

```text
PicoGK/native/win-x64/
  picogk.26.1.dll
  blosc.dll
  lz4.dll
  tbb12.dll
  zlib1.dll
  zstd.dll
```

Set the PicoGK native runtime path before building and running Godot:

```powershell
$env:PICOGK_NATIVE_DIR = "C:\Users\richa\OneDrive\Desktop\PicoGK Procedural World Generation\PicoGK\native\win-x64"
```

After Godot builds, copy the PicoGK DLLs next to the editor executable:

```powershell
Copy-Item "$env:PICOGK_NATIVE_DIR\*.dll" ".\godot_engine\bin\"
```

The DLLs must be next to:

- `godot_engine/bin/godot.windows.editor.x86_64.mono.exe`
- Any exported game executable that uses this module

The module `SCsub` can also copy the DLLs automatically when `PICOGK_NATIVE_DIR` is set.

---

## Step 5 — Build the custom Godot .NET editor

From the Godot source folder:

```powershell
cd "C:\Users\richa\OneDrive\Desktop\PicoGK Procedural World Generation\godot_engine"
$env:PICOGK_NATIVE_DIR = "..\PicoGK\native\win-x64"
scons platform=windows target=editor module_mono_enabled=yes -j8
```

After the build completes, copy the PicoGK runtime DLLs into `bin/`:

```powershell
Copy-Item "$env:PICOGK_NATIVE_DIR\*.dll" ".\bin\"
```

If your selected Godot tag requires Mono/.NET glue generation, run the glue step required by that Godot version. For many Godot 4.x source builds, the command is similar to:

```powershell
.\bin\godot.windows.editor.x86_64.mono.exe --headless --generate-mono-glue modules/mono/glue
```

Then rebuild if the Godot tag requires a second pass.

---

## Step 6 — Clone PicoGK dependencies

From the repository root, clone these if they are not already present:

```powershell
git clone https://github.com/leap71/PicoGK
git clone https://github.com/leap71/LEAP71_ShapeKernel
git clone https://github.com/leap71/LEAP71_LatticeLibrary
```

The Godot module uses:

- A C++ port of the `PlanetTest` noise and SDF logic
- Direct calls into `picogk.26.1.dll`
- PicoGK entry points matching the native interop layer used by `PicoGK/Internals/Interop.cs`

Game scripts do **not** reference PicoGK C# directly.

---

## Step 7 — Module contents

Module location inside the Godot engine tree:

```text
godot_engine/modules/picogk_voxel/
```

| File | Purpose |
|------|---------|
| `config.py` | Enables the module and reads `PICOGK_NATIVE_DIR` |
| `SCsub` | Compiles the module sources and optionally copies DLLs |
| `register_types.*` | Registers `PicogkPlanetGenerator` with Godot |
| `picogk_runtime.*` | Loads `picogk.26.1.dll` and wraps the native C API |
| `planet_noise.*` | C++ port of `Noise.cs` |
| `planet_sdf.*` | C++ port of `PlanetSDF` and `BiomeSDF` |
| `picogk_planet_generator.*` | Godot `RefCounted` API that returns an `ArrayMesh` |
| `picogk_voxel.*` | Module hooks |

Rebuild Godot after adding or changing the module:

```powershell
cd ".\godot_engine"
scons platform=windows target=editor module_mono_enabled=yes -j8
```

---

## Step 8 — Godot API

**ClassDB name:** `PicogkPlanetGenerator`
**Base type:** `RefCounted`

| Method | Description |
|--------|-------------|
| `generate_planet(radius_mm, voxel_size_mm, seed)` | Generates the full planet as an `ArrayMesh` |
| `generate_biome_mesh(biome_id, radius_mm, voxel_size_mm, seed)` | Generates one biome shell as an `ArrayMesh` |
| `generate_collision_mesh(radius_mm, voxel_size_mm, seed)` | Generates the full planet mesh for collision |
| `set_noise_settings(noise_scale, warp_strength, terrain_height)` | Sets noise scale, domain warp strength, and terrain height |
| `set_biome_thresholds(ocean_threshold, mountain_threshold, polar_latitude)` | Sets biome latitude and noise thresholds |
| `set_reverse_winding(enabled)` | Swaps triangle order if normals are inverted |

Generation should run on a **worker thread** so the editor does not freeze. The module should not open a PicoGK OpenGL viewer or ImGui overlay.

---

## Step 9 — Demo project

Open `godot_demo/` with your custom Godot editor binary:

```powershell
.\godot_engine\bin\godot.windows.editor.x86_64.mono.exe --path .\godot_demo
```

Demo layout:

```text
godot_demo/
  project.godot
  scenes/World.tscn
  scripts/planet_world.gd      # Builds biome MeshInstance3D nodes
  scripts/planet_test.gd       # Automated generation test
```

Run the `World` scene or attach `planet_test.gd` to verify:

- Full planet mesh generation
- Six biome mesh generations
- Collision mesh generation
- Correct scale in Godot meters
- No PicoGK viewer window
- Editor remains responsive during generation

---

## Testing checklist

1. Build the custom Godot editor with `module_mono_enabled=yes`.
2. Confirm `picogk_voxel` was compiled into the editor.
3. Copy all PicoGK runtime DLLs into `godot_engine/bin/`.
4. Start the editor and open `godot_demo/`.
5. Check the script console:

   ```gdscript
   ClassDB.class_exists("PicogkPlanetGenerator")
   ```

   Expected result:

   ```gdscript
   true
   ```

6. Run the `World` scene.
7. Confirm all biome meshes and collision are created.
8. Confirm no PicoGK viewer or ImGui window opens.
9. Confirm the editor does not freeze while generation runs.

---

## PicoGK API access notes

| API | Access in C# | Godot module approach |
|-----|--------------|-----------------------|
| `IImplicit.fSignedDistance` | Public interface | C++ callbacks in `planet_sdf.cpp` |
| `Voxels` + `RenderImplicit` | Public | Native voxel rendering through the PicoGK DLL |
| `Mesh` from voxels | Public `new Mesh(voxels)` | Native mesh creation from voxel handles |
| `Library.Go` + `Viewer` | Opens viewer | Not used; the module creates a library/runtime instance only |
| `Sh.PreviewVoxels` | ShapeKernel viewer | Not used in Godot |
| `Voxels` internal handles | Internal C# constructors | Native handles are used directly through the DLL |

The module does not require PicoGK C# internal APIs for mesh generation. It uses the PicoGK native ABI mirrored by the PicoGK C# interop layer.

If your PicoGK DLL version changes, update the DLL name in:

```text
godot_engine/modules/picogk_voxel/picogk_runtime.h
```

Look for:

```cpp
PICOGK_DLL_NAME
```

---

## One-command build summary

From the repository root:

```powershell
cd "C:\Users\richa\OneDrive\Desktop\PicoGK Procedural World Generation"
$env:PICOGK_NATIVE_DIR = ".\PicoGK\native\win-x64"
cd .\godot_engine
scons platform=windows target=editor module_mono_enabled=yes -j8
Copy-Item "$env:PICOGK_NATIVE_DIR\*.dll" ".\bin\"
.\bin\godot.windows.editor.x86_64.mono.exe --path ..\godot_demo
```

---

## Matching `PlanetTest` output

Keep these defaults aligned with `PlanetTest` unless you intentionally expose them as sliders:

```text
radius_mm          = 80
voxel_size_mm      = 1.2
seed               = 42
noise_scale        = 0.028
warp_strength      = 0.8
terrain_height     = 12
ocean_threshold    = -0.18
mountain_threshold = 0.28
polar_latitude     = 0.78
```

Compare the STL exported by `PlanetTest`:

```text
Desktop/PlanetTest_Output/Planet.STL
```

against the Godot collision mesh. Remember that PicoGK outputs millimeters and Godot uses meters.

---

## Troubleshooting

| Issue | Fix |
|-------|-----|
| `Failed to load picogk.26.1.dll` | Copy every DLL from `PicoGK/native/win-x64/` into `godot_engine/bin/` and confirm `PICOGK_NATIVE_DIR` is set |
| Empty mesh | Increase `radius_mm`, reduce `voxel_size_mm`, and check the console for PicoGK runtime errors |
| Editor freezes | Confirm generation is running on the worker-thread path, not directly on the main thread |
| Wrong scale | Confirm all PicoGK vertex positions are divided by `1000` before building the `ArrayMesh` |
| Inside-out mesh | Enable `reverse_winding` |
| Module not found | Confirm the final path is `godot_engine/modules/picogk_voxel/config.py` |
| Godot build does not include the module | Clean and rebuild after copying or linking the module into `godot_engine/modules/` |

---

## License

Respect the licenses for:

- Godot — MIT
- PicoGK — Apache-2.0
- LEAP71 ShapeKernel — Apache-2.0
- LEAP71 LatticeLibrary — Apache-2.0
- Your own project assets and code
