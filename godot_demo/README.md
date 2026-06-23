# Picogk Planet Demo

Procedural voxel planet in Godot using **PicoGK + ShapeKernel + PlanetTest** via `PicogkGodotBridge`.

**Full library + Godot merge doc (~1200 lines):** [../MERGED_LIBRARIES_README.md](../MERGED_LIBRARIES_README.md) · [GAME_ROADMAP.md](../GAME_ROADMAP.md) · [PROCEDURAL_PLATFORM.md](../PROCEDURAL_PLATFORM.md)

## Open project

Use your **custom** editor (not stock Godot):

```
..\godot_engine\bin\godot.windows.editor.x86_64.mono.exe
```

**Project → Open** → select this `godot_demo` folder.

First open may take a moment while C# builds.

## Pipeline (same as PlanetTest)

1. **PlanetSDF** — spherical terrain SDF  
2. **BiomeSDF** — biome mask per layer  
3. **`new Voxels(implicit, bbox)`** — PicoGK / OpenVDB voxelize  
4. PlanetTest: **`Sh.PreviewVoxels`** (PicoGK viewer)  
5. This demo: **`VoxelPreviewData`** → instanced cubes in Godot (same data as `Viewer_AddVoxels` / `PreviewVoxels`)  
6. **Default display:** one **`voxFull()`** mesh with **vertex colors** (biome palette bleeds at borders).

## See the planet in the editor

1. Open `godot_demo` in your **custom Godot .NET editor** (or stock 4.4 mono).
2. **Project → Build** (C# must compile; copies `picogk.26.1.dll` next to the game assembly).
3. Enable plugin **PicoGK Planet** under **Project → Project Settings → Plugins**.
4. Open `scenes/World.tscn` (small scene — do **not** save generated meshes into the `.tscn`).
5. In the Scene tree, select the **World** root (not `PlanetRoot` or old `Biome_*` nodes).
6. Inspector → set **Display Mode** to **Solid Colored Planet** → check **Regenerate Now** once, then uncheck.
7. Wait 1–3 minutes. Under `PlanetRoot` you should see only **PlanetSurface** (one mesh, blended colors).

**Inspector felt dead?** An old `World.tscn` had six multi‑million‑triangle meshes saved inside it (~85 MB). That freezes the editor. The scene is now empty until you regenerate; runtime meshes are no longer written into the scene file.

If the status label shows **ERROR**, check **Output** for missing native DLLs.

## Run

Press **F5** (main scene: `scenes/World.tscn`).

- Same steps as `PlanetTest/Program.cs` on a background thread
- Six biome voxel layers + one full planet for collision
- Orbit camera rotates around the planet

## Settings (PlanetWorld node)

| Export | Default | Meaning |
|--------|---------|---------|
| Display Mode | Solid Colored Planet | One mesh + biome color bleed (what you want) |
| Biome Color Bleed | 1 | Wider = softer biome borders |
| RadiusMm | 80 | Planet radius (mm) |
| VoxelSizeMm | 1.2 | PicoGK voxel size (PlanetTest) |
| Noise Seed | 42 | Noise seed |
| Regenerate Now | — | Check once after changing settings |

## Build bridge first (if DLL missing)

```powershell
cd ..\godot\modules\picogk_voxel\scripts
.\build_bridge.ps1
```

Native DLLs must be next to the game executable (copied automatically via `.csproj`).
