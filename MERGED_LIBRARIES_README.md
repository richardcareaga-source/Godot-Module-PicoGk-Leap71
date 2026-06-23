# Merged Platform README — PicoGK + ShapeKernel + LatticeLibrary + Godot

> **~1,200 lines** — unified reference for merging LEAP71 geometry kernels with Godot procedural generation.  
> Generated from source under `PicoGK/`, `LEAP71_ShapeKernel/`, `LEAP71_LatticeLibrary/`, and `godot/modules/picogk_voxel/bridge/`.  
> **Also see:** [GAME_ROADMAP.md](GAME_ROADMAP.md) · [PROCEDURAL_PLATFORM.md](PROCEDURAL_PLATFORM.md) · [README.md](README.md)

---

## Table of contents

1. [Merged stack — one sentence per layer](#1-merged-stack--one-sentence-per-layer)
2. [How the three libraries connect](#2-how-the-three-libraries-connect)
3. [Units, coordinates, and data flow](#3-units-coordinates-and-data-flow)
4. [PicoGK geometry kernel (full capability map)](#4-picogk-geometry-kernel-full-capability-map)
5. [LEAP71 ShapeKernel (full capability map)](#5-leap71-shapekernel-full-capability-map)
6. [LEAP71 LatticeLibrary + ImplicitLibrary](#6-leap71-latticelibrary--implicitlibrary)
7. [PlanetTest — game world SDF](#7-planettest--game-world-sdf)
8. [PicogkGodotBridge — wrappers today and planned](#8-picogkgodotbridge--wrappers-today-and-planned)
9. [Godot nodes, hooks, and GUI (product spec)](#9-godot-nodes-hooks-and-gui-product-spec)
10. [Threading, caching, hashing, chunking](#10-threading-caching-hashing-chunking)
11. [End-user workflows](#11-end-user-workflows)
12. [Complete public API appendix (from source scan)](#12-complete-public-api-appendix-from-source-scan)

---

## 1. Merged stack — one sentence per layer

| Layer | Folder | One sentence |
|-------|--------|----------------|
| **Native kernel** | `PicoGK/native/win-x64/` | OpenVDB-backed `picogk.26.1.dll` — voxel size, booleans, meshing, STL/VDB I/O. |
| **PicoGK C#** | `PicoGK/` | Thin C# binding: `Library`, `Voxels`, `Mesh`, `Lattice`, `IImplicit`, fields, viewer. |
| **ShapeKernel** | `LEAP71_ShapeKernel/ShapeKernel/` | Engineering shapes on top of PicoGK: frames, base solids, splines, `Sh.*` helpers, previews. |
| **LatticeLibrary** | `LEAP71_LatticeLibrary/` | Beam lattices + TPMS implicits inside bounding volumes. |
| **PlanetTest** | `PlanetTest/PlanetTest/` | Spherical planet SDF + biomes — **your game world truth**. |
| **Bridge** | `godot/modules/picogk_voxel/bridge/` | Headless PicoGK for Godot: meshes, colors, voxel preview — no viewer window. |
| **Godot demo** | `godot_demo/` | `[Tool]` nodes, Inspector, plugin menu — **product shell** for artists. |

**Merge principle:** All solid truth is **`IImplicit` → `Voxels` → `Mesh`**. Godot never re-implements booleans; it displays, streams, and games on top of **cached** bridge output.

---

## 2. How the three libraries connect

```
                    ┌─────────────────────────────────────┐
                    │  Godot 4.x + PicogkGodotBridge.dll   │
                    │  (GUI, nodes, cache, MultiMesh)      │
                    └──────────────────┬──────────────────┘
                                       │
         ┌─────────────────────────────┼─────────────────────────────┐
         │                             │                             │
         ▼                             ▼                             ▼
┌─────────────────┐          ┌─────────────────┐          ┌─────────────────┐
│   PlanetTest    │          │  ShapeKernel    │          │ LatticeLibrary  │
│ PlanetSDF       │          │ BaseBox/Sphere  │          │ ICellArray      │
│ BiomeSDF        │          │ BasePipe/Revolve│          │ ILatticeType    │
│ Noise.cs        │          │ Sh.lattice/     │          │ IBeamThickness  │
│                 │          │ export/preview  │          │ TPMS IImplicit  │
└────────┬────────┘          └────────┬────────┘          └────────┬────────┘
         │                            │                            │
         └────────────────────────────┼────────────────────────────┘
                                      ▼
                         ┌────────────────────────┐
                         │ PicoGK (C# + native)   │
                         │ Voxels · Mesh · Lattice│
                         │ IImplicit · OpenVDB    │
                         └────────────────────────┘
```

**ShapeKernel** depends on PicoGK (`using PicoGK`). **LatticeLibrary** depends on ShapeKernel (bounding shapes) and PicoGK (`Voxels`, `Lattice`). **PlanetTest** lives in namespace `Leap71.ShapeKernel` and uses the same `IImplicit` contract.

---

## 3. Units, coordinates, and data flow

| Space | Unit | Notes |
|-------|------|-------|
| PicoGK / ShapeKernel / PlanetTest | **millimeters (mm)** | `PlanetSDF.R = 80` → 80 mm radius. |
| Godot demo | **meters (m)** | `PicogkMeshData` divides vertex positions by 1000. |
| Voxel grid | `Library(voxelSizeMm)` | PlanetTest default **1.2 mm** — trades quality vs speed. |

**Standard pipeline (any feature):**

1. Define geometry as **`IImplicit.fSignedDistance(Vector3 pt)`** (negative inside, positive outside).
2. **`new Voxels(implicit, BBox3)`** — rasterize SDF into OpenVDB level set at library voxel size.
3. Optional **`voxBoolAdd` / `voxBoolSubtract` / `Offset` / `Smoothen` / `Fillet`** — volumetric modeling.
4. **`new Mesh(voxels)`** — marching-cubes-style surface.
5. Bridge → **`PicogkMeshData`** → Godot **`ArrayMesh`** (optional per-vertex color).

**Headless vs viewer:**

| Entry | Use |
|-------|-----|
| `Library.Go(voxelSize, ThreadStart task)` | PlanetTest.exe, LEAP71 examples — opens **PicoGK Viewer**, task on worker thread. |
| `new PicogkHeadlessRuntime(voxelSize)` + `RegisterGlobalLibrary` | **Godot**, CI, batch cache — no window. |

---

## 4. PicoGK geometry kernel (full capability map)

**Source:** 60 C# files under `PicoGK/` (72 including nested). Native runtime in `PicoGK/native/win-x64/*.dll`.

### 4.1 Core types

| Type | Role |
|------|------|
| `Library` | Voxel size, native handle, lifecycle (`IDisposable`). |
| `Voxels` | OpenVDB level set — **central type** for all solid modeling. |
| `Mesh` | Triangle mesh; built from `Voxels` or manual triangles. |
| `Lattice` | Beam/sphere skeleton → rasterized to `Voxels`. |
| `IImplicit` | `float fSignedDistance(in Vector3 vec)` — procedural surfaces. |
| `IBoundedImplicit` | `IImplicit` + `BBox3 oBounds` — finite shapes. |
| `ScalarField` / `VectorField` | Grid fields; derived from voxels or implicit. |
| `BBox2` / `BBox3` | Axis-aligned bounds (mm). |
| `PolyLine` | Debug polylines in viewer. |
| `IProgress` | Long operation progress (voxelize, export). |

### 4.2 `Voxels` — constructors (create volume)

| Constructor / factory | Purpose |
|----------------------|---------|
| `Voxels(Library)` | Empty field. |
| `Voxels(Library, IImplicit, BBox3)` | **Planet, gyroid, any SDF** in box. |
| `Voxels(Library, IBoundedImplicit)` | Implicit with built-in bounds. |
| `Voxels(Mesh)` | Voxelize existing mesh. |
| `Voxels(Lattice)` | Voxelize beam lattice. |
| `Voxels(ScalarField)` | From scalar field. |
| `Voxels(Voxels)` | Copy. |
| `voxSphere(lib, center, radius)` | Analytic sphere. |
| `voxLatticeBeam(lib, A, rA, B, rB)` | Single beam. |
| `voxMeshShell(lib, mesh, thickness)` | Hollow shell from mesh. |
| `voxFromVdbFile(lib, path)` | **Load cache** from OpenVDB. |
| Global variants | Same, using `Library.oLibrary()` after `Go()`. |

### 4.3 `Voxels` — boolean & morphological ops

| Method | Operator | Game use |
|--------|----------|----------|
| `BoolAdd` / `voxBoolAdd` / `+` | Union | Combine terrain + structure. |
| `BoolSubtract` / `-` | Subtract | Tunnels, building cuts. |
| `BoolIntersect` / `&` | Intersect | Clip to biome volume. |
| `voxIntersectImplicit(IImplicit)` | Mask by SDF | Keep only inside planet shell. |
| `Offset` / `voxOffset` | Grow/shrink | Terraces, erosion. |
| `DoubleOffset` / `TripleOffset` | Advanced offset | Smooth rocky shores. |
| `Smoothen` | Smooth surface | Organic terrain. |
| `OverOffset` | Controlled bulge | Plateaus. |
| `Fillet` | Round features | Lattice post-process (LEAP71 examples). |
| `voxShell` | Hollow | Hollow planet core, domes. |
| `Trim` / `voxTrim` | Clip to box | **Chunking** — only voxelize bbox. |
| `RenderMesh` / `RenderLattice` | Add geometry into field | Incremental builds. |
| `RenderImplicit` | Paint implicit into field | Local edits. |
| `ProjectZSlice` | 2.5D extrusion | Maps, layers. |

### 4.4 `Voxels` — analysis & queries (gameplay gold)

| Method | Returns | Godot use |
|--------|---------|-----------|
| `bIsInside(vec)` | Inside solid? | Foot on ground. |
| `vecSurfaceNormal(vec)` | Normal | Align trees / buildings. |
| `vecClosestPointOnSurface(vec)` | Snap point | Placement. |
| `bRayCastToSurface(origin, dir, hit)` | Ray hit | Camera, tools. |
| `CalculateProperties` | Volume, bbox | UI stats. |
| `oCalculateBoundingBox` | `BBox3` | Chunk bounds. |
| `GetVoxelDimensions` | Grid size | Debug / LOD. |
| `GetVoxelSlice` / `imgAllocateSlice` | 2D slices | `VoxelPreviewData` in bridge. |
| `nSliceCount` | Z slices | Preview layers. |
| `bIsEqual` | Compare | Cache validation. |
| `nMemUsage` | RAM | Budget in GUI. |

### 4.5 `Voxels` — export & vectorization

| Method | Format |
|--------|--------|
| `SaveToVdbFile(path)` | OpenVDB — **chunk cache**. |
| `voxFromVdbFile` | Load cache. |
| `SaveToCliFile` | CLI print slice stack. |
| `oVectorize` | 2D vector contours from slices. |

### 4.6 `Mesh`

| API | Purpose |
|-----|---------|
| `Mesh(Voxels)` | **Primary** mesh extraction. |
| `nAddVertex` / `nAddTriangle` / `AddQuad` | Manual mesh build. |
| `GetTriangle` | Read back (used by `PicogkMeshData`). |
| `Append` | Merge meshes. |
| `mshCreateTransformed` / `mshCreateMirrored` | Copy with transform. |
| `oBoundingBox` | Bounds check. |

### 4.7 `Lattice`

| API | Purpose |
|-----|---------|
| `AddBeam(A, rA, B, rB)` | Strut between points. |
| `AddSphere(center, r)` | Node ball. |
| → `new Voxels(lattice)` | Solidify beams. |

### 4.8 `Library` / `Library.Go`

| API | Purpose |
|-----|---------|
| `Library(float voxelSizeMm)` | Create kernel instance. |
| `Library.Go(voxelSize, ThreadStart, ...)` | Run task + viewer loop. |
| `RegisterGlobalLibrary` / `UnregisterGlobalLibrary` | **One global lib** — bridge uses this. |
| `Library.Log` | File + console log. |
| `fVoxelSizeMM` | Current voxel size. |

### 4.9 Viewer (PicoGK desktop only — not in Godot)

| Component | Purpose |
|-----------|---------|
| `Viewer` | OpenGL window, poll loop. |
| `Sh.PreviewVoxels` (ShapeKernel) | Colored voxel display in viewer. |
| `ViewerActions`, `ViewerAnimation` | Interactive inspection. |

Godot replaces viewer with **mesh + vertex colors + MultiMesh voxels**.

### 4.10 IO & utilities

| Module | Capability |
|--------|------------|
| `CliIo` | CLI slice export formats. |
| `MeshIo` | STL read/write units. |
| `OpenVdbFile` | VDB field types. |
| `FieldUtils` / `SdfVisualizer` | Field debug. |
| `VoxCutViz` / `SliceViz` | Cutting plane visualization. |
| `Utils.TempFolder` | Temp workspace. |

### 4.11 2D/3D shape helpers (PicoGK native)

| Path | Types |
|------|-------|
| `Shapes/2D` | `IPath2d`, `IContour2d`, `Ellipse`, `Supershape`, `Path2d` |
| `Shapes/3D` | `IPath3d`, `IContour3d`, `OrientedPath` |
| `Numerics` | `VectorExt`, `Rad`, `Spherical`, `Cylindrical`, tolerances |

---

## 5. LEAP71 ShapeKernel (full capability map)

**Source:** 66 C# files under `LEAP71_ShapeKernel/ShapeKernel/` (+ Examples).

ShapeKernel is a **computational engineering modeling** layer: parametric frames, sweeps, pipes, logos, splines, and **`Sh` static helpers** that call PicoGK.

### 5.1 Frames & coordinates

| Type | Purpose |
|------|---------|
| `LocalFrame` | Origin + axes for placing shapes. |
| `Frames` | Multi-frame splines (`EFrameType`: CYLINDRICAL, SPHERICAL, Z, MIN_ROTATION). |

### 5.2 BaseShapes — all inherit `BaseShape` → `voxConstruct()`

| Class | Interfaces | Typical use |
|-------|------------|-------------|
| `BaseSphere` | mesh, surface | Bounding volumes, planets (with custom SDF). |
| `BaseBox` | mesh, surface | Blocks, buildings, clip regions. |
| `BaseCylinder` | mesh, surface | Towers, columns. |
| `BaseCone` | — | Tapers. |
| `BasePipe` | mesh, surface | Tubes along spine. |
| `BasePipeSegment` | extends pipe | Partial pipe (`START_END`, `MID_RANGE`). |
| `BaseRing` | mesh, surface | Torus-like rings. |
| `BaseLens` | mesh, surface | Optical / dome caps. |
| `BaseRevolve` | — | Profile revolve solids. |
| `BaseLogoBox` | extends box | Logo emboss boxes. |
| `LatticePipe` | lattice, spine | Pipe as **beam lattice**. |
| `LatticeManifold` | extends pipe | Manifold lattice pipes. |

**Pattern:**

```csharp
var shape = new BaseSphere(new LocalFrame(new Vector3(0,0,0)), radiusMm);
using Voxels vox = shape.voxConstruct();
using Mesh msh = new Mesh(vox);
```

`SetTransformation(fnVertexTransformation)` warps points during construction.

### 5.3 Splines

| Class | Role |
|-------|------|
| `ControlPointSpline` | Open/closed control polygon. |
| `TangentialControlSpline` | Tangential handles. |
| `CylindricalControlSpline` | Radial/tangential/Z modes. |
| `ControlPointSurface` | 2D control net. |
| `SplineOperations` | Evaluate, resample, frame operations. |

### 5.4 Modulations (parametric variation)

| Class | Role |
|-------|------|
| `LineModulation` | 1D profiles along X/Y/Z; distributions. |
| `SurfaceModulation` | 2D image or function on surfaces. |

### 5.5 `Sh` — partial static class (multiple files)

#### ShBasicFunctions (→ PicoGK; many marked Obsolete wrappers)

| Method | Maps to |
|--------|---------|
| `voxOffset` | `Voxels.voxOffset` |
| `voxSmoothen` | `Voxels.voxSmoothen` |
| `voxOverOffset` | `Voxels.voxOverOffset` |
| `voxUnion` / `voxSubtract` / `voxIntersect` | Boolean ops |
| `voxIntersectImplicit` | Mask by implicit |

#### ShCombinedFunctions

| Method | Role |
|--------|------|
| `voxShell` | Hollow volume. |
| `voxUnion(List<Voxels>)` | Combine many. |

#### ShLatticeFunctions

| Method | Role |
|--------|------|
| `latFromLine` / `latFromPoints` / `latFromGrid` | Build `Lattice`. |
| `latFromBeam` | Single beam with radii. |
| `AddLine` | Append to lattice. |

#### ShVoxelFunctions

| Method | Role |
|--------|------|
| `vecGetClosestSurfacePoint` | Surface snap. |
| `vecGetProjectedSurfacePoint` | Directional snap. |
| `oGetBoundingBox` | BBox of voxels. |

#### ShExportFunctions

| Method | Role |
|--------|------|
| `ExportMeshToSTLFile` | STL from mesh. |
| `ExportVoxelsToSTLFile` | Mesh via voxels. |
| `ExportVoxelsToVDBFile` | **Cache to disk**. |
| `ExportVoxelsToCLIFile` | Print slices. |
| `strGetExportPath(EExport, name)` | STL, TGA, PNG, CSV, VDB, CLI paths. |

#### ShPreviewFunctions_I / _II (viewer only)

| Method | Role |
|--------|------|
| `PreviewVoxels(vox, color, alpha?)` | **PlanetTest biome layers** in PicoGK viewer. |
| `PreviewLine` / `PreviewBeam` / `PreviewPoint` | Debug geometry. |
| `PreviewFrame` / `PreviewBoxWireframe` | Construction aids. |
| `Preview` overloads | Points, fields, meshes. |

#### Visualizations

| Class | Role |
|-------|------|
| `MeshPainter` | Color mesh by overhang angle / custom fields. |
| `ColorScale2D/3D`, `ColorPalette`, `Cp` | Color constants (`clrBlue`, `clrGreen`, …). |
| `RotationAnimator` | Viewer animation. |

### 5.6 Utilities (selected)

| Utility | Role |
|---------|------|
| `VecOperations` | Point/frame math (many obsolete → use `Vector3` extensions). |
| `MeshUtility` | Mesh analysis, transforms. |
| `Measure` | Distances, angles between frames. |
| `ImplicitUtility` | `ImplicitGyroid`, `ImplicitSphere`, `ImplicitGenus`, `ImplicitSuperEllipsoid`. |
| `GridOperations` | Grid sampling. |
| `Bisection` | Root finding on SDFs. |
| `SuperShapes` / `PolygonalShapes` | Parametric profiles. |
| `UsefulFormulas` | Math helpers. |
| `CSVWriter` | Data export. |

### 5.7 ShapeKernel Examples (reference tasks)

| Example | Demonstrates |
|---------|----------------|
| `Ex_BaseSphereShowCase` | Sphere voxels. |
| `Ex_BaseBoxShowCase` | Box. |
| `Ex_BasePipeShowCase` | Pipes. |
| `Ex_LatticePipeShowCase` | Lattice pipes. |
| `Ex_ImplicitGyroidSphere` | Gyroid implicit. |
| `Ex_MeshPainterShowCase` | Mesh painting. |
| `Ex_OverOffsetShowCase` | Over-offset ops. |

---

## 6. LEAP71 LatticeLibrary + ImplicitLibrary

**Source:** 29 C# files under `LEAP71_LatticeLibrary/`.

### 6.1 Lattice workflow (three interfaces)

```
ICellArray  →  list of IUnitCell corners in space
ILatticeType →  how corners connect (beams/splines)
IBeamThickness →  radius at each beam point
        ↓
voxGetFinalLatticeGeometry(...) → Voxels
        ↓
Fillet / Boolean with voxBounding → final solid
```

| Interface | Implementations |
|-----------|-----------------|
| `ICellArray` | `RegularCellArray`, `RegularUnitCell`, `ConformalCellArray` |
| `IUnitCell` | `CuboidCell` |
| `ILatticeType` | `BodyCentreLattice`, `OctahedronLattice`, `RandomSplineLattice` |
| `IBeamThickness` | `ConstantBeamThickness`, `CellBasedBeamThickness`, `BoundaryBeamThickness`, `GlobalFuncBeamThickness` |

**Example entry points:** `LatticeLibraryShowCase.RegularTask`, `ConformalTask` (`Examples/Ex_LatticeLibrary*.cs`).

**Post-steps from examples:** `voxLattice.Fillet(1.0f); voxLattice &= voxBounding;`

### 6.2 ImplicitLibrary (TPMS infills)

| Class | Pattern |
|-------|---------|
| `ImplicitSchwarzDiamond` | TPMS in box (example default). |
| `ImplicitSchwarzPrimitive` | Primitive Schwarz. |
| `ImplicitLidinoid` | Lidinoid TPMS. |
| `ImplicitRadialGyroid` | Radial variant. |
| `ImplicitModular` | Modular cells. |
| `ImplicitSplitWallGyroid` / `ImplicitSplitVoidGyroid` | Split gyroid walls/voids. |
| `ImplicitRandomizedSchwarzPrimitive` | Randomized. |

**Supporting:** `RawTPMSPatterns`, `ISplittingLogic` (wall/void half-spaces), `ICoordinateTrafo` (`ScaleTrafo`, `RadialTrafo`, `CombinedTrafo`), `RandomDeformationField`.

**Typical pipeline:**

```csharp
Voxels voxBounding = new BaseBox(new LocalFrame(), 50, 50, 50).voxConstruct();
IImplicit pattern = new ImplicitSchwarzDiamond(unitSize, wallThickness);
Voxels voxResult = voxBounding.voxIntersectImplicit(pattern);
```

**Example tasks:** `ImplicitLibraryShowCase.RegularTask`, `RandomTask`, `RadialTask`, `ModularTask`, `LogicSplitTask`.

### 6.3 Bridge wrapper today

`LatticeBridge.RegularLatticeInSphere(rt, radiusMm, cellsX/Y/Z, noiseLevel)` — meshes lattice in sphere bounds.

**Planned:** parameter resource → pick cell array + lattice type + thickness curve → cache → Godot mesh node.

---

## 7. PlanetTest — game world SDF

| File | Content |
|------|---------|
| `Planet.cs` | `PlanetSDF`, `BiomeSDF`, `Planet.voxFull()`, `Planet.voxBiome()` |
| `Noise.cs` | Perlin, fBm, domain-warped fBm, `SetSeed` / `GetSeed` |
| `Program.cs` | Six biome previews + full STL |

**Biome rules** (`PlanetSDF.eBiome`): polar lat > 0.78; ocean noise < -0.18; tundra lat > 0.52; mountain noise > 0.28; tropical lat < 0.32; else temperate.

**Godot parity:** `BiomeColorSampler` uses same thresholds with smooth weights for vertex colors.

---

## 8. PicogkGodotBridge — wrappers today and planned

**Project:** `godot/modules/picogk_voxel/bridge/PicogkGodotBridge.csproj`  
**Compiles:** PicoGK + all ShapeKernel + all LatticeLibrary + PlanetTest `Planet.cs` / `Noise.cs`.

### 8.1 Implemented wrappers

| File | API | Status |
|------|-----|--------|
| `PicogkHeadlessRuntime` | `Library`, `ActivateGlobalLibrary`, `VoxelsFromImplicit`, `MeshFromVoxels` | ✅ |
| `PlanetBridge` | `RunPlanetTaskLikePlanetTest`, `RunSolidColoredPlanet`, `VoxelsToMeshData` | ✅ |
| `ColoredPlanetMeshData` | `BuildFromVoxFull` + vertex RGB | ✅ |
| `BiomeColorSampler` | Soft biome colors at mesh vertices | ✅ |
| `PicogkMeshData` | PicoGK `Mesh` → float[] vertices/normals | ✅ |
| `VoxelPreviewData` | Voxel centers for MultiMesh | ✅ |
| `ShapeKernelBridge` | Sphere, box test meshes | ✅ minimal |
| `LatticeBridge` | Regular lattice in sphere | ✅ minimal |
| `PicogkBridge` | Convenience entry | ✅ |

### 8.2 Planned wrappers (hooks)

| Planned class | Wraps | Godot hook |
|---------------|-------|------------|
| `VoxelOpsBridge` | All `Voxels` booleans/morphology | Terraform tool node |
| `VoxelCacheBridge` | VDB save/load + manifest | Chunk streamer |
| `SurfaceSamplerBridge` | `PlanetSDF` + masks without voxels | Foliage, gameplay |
| `ShapeKernelCatalog` | All `BaseShape` types | Structure generator node |
| `LatticeGenBridge` | Full `ICellArray`/`ILatticeType`/`IBeamThickness` | Alien flora node |
| `ImplicitGenBridge` | TPMS presets | Crystal biome node |
| `ExportBridge` | `Sh.Export*` | Editor Export button |
| `JobScheduler` | Queue + progress | Dock UI |

---

## 9. Godot nodes, hooks, and GUI (product spec)

### 9.1 Nodes (planned `[Tool]` C# nodes)

| Node | Extends | Responsibility |
|------|---------|----------------|
| `PicogkWorldRoot` | `Node3D` | Preset, seed, chunk streamer — replaces monolithic `PlanetWorld` over time. |
| `PicogkPlanetGenerator` | `Node3D` | Async generate, attach children, **no Owner in editor**. |
| `PicogkChunkStreamer` | `Node3D` | Load/unload ring of chunk meshes from cache. |
| `PicogkFoliageScatter` | `Node3D` | MultiMesh from `SurfaceSampler`. |
| `PicogkStructurePlacer` | `Node3D` | Godot scene OR bridge mesh on surface. |
| `PicogkLatticeVolume` | `Node3D` | Bounding volume → lattice mesh. |
| `PicogkImplicitVolume` | `Node3D` | TPMS inside bounds. |
| `PicogkVoxelPreview` | `Node3D` | Instanced cubes (`VoxelPreviewUtil`). |

### 9.2 Hooks (signals / callables)

| Hook | When |
|------|------|
| `generation_started` / `generation_finished` | Job queue |
| `chunk_loaded(chunkId, mesh)` | Streaming |
| `cache_hit` / `cache_miss` | Disk layer |
| `preset_changed(WorldPreset)` | GUI |

### 9.3 GUI — full support matrix

| Surface | Features |
|---------|----------|
| **Inspector** | `WorldPreset` resource, all planet/noise/biome fields, Regenerate Now. |
| **Editor dock** | Preset library, progress bar, cancel, cache stats, export STL/VDB. |
| **In-game menu** | Seed entry, world settings, graphics (bleed, LOD). |
| **Debug overlays** | Biome heatmap, slope, moisture (shader or second mesh). |

**Productivity rules for end users:**

1. Small **preview chunk** for fast slider feedback; **full build** on button.
2. **Presets** (.tres) shareable between projects.
3. **Never embed** generated meshes in `.tscn`.
4. **One-click** reproduce PlanetTest in Godot for regression.

### 9.4 Editor plugin (`addons/picogk_planet`)

Today: menu → find `PlanetWorld` → `GeneratePlanet()`.  
Tomorrow: dedicated **PicoGK** bottom panel with preset picker + job queue.

---

## 10. Threading, caching, hashing, chunking

(Summary — full detail in [PROCEDURAL_PLATFORM.md](PROCEDURAL_PLATFORM.md).)

| Topic | Rule |
|-------|------|
| **Threading** | PicoGK: **one global `Library` at a time**. Godot: `Task.Run` + `CallDeferred`; parallel chunks via **process pool** or queue concurrency=1. |
| **Caching** | `SaveToVdbFile` / `voxFromVdbFile` per chunk; optional baked `ArrayMesh` on disk. |
| **Hashing** | `SHA256(preset + chunkId + lod + voxelSize)` → cache path. |
| **Chunking** | `BBox3` subsets of planet; `Trim`; separate `MeshInstance3D` per chunk. |
| **LOD** | Increase `voxelSizeMm` per distance band. |

---

## 11. End-user workflows

### 11.1 Artist — new planet for game

1. Open `godot_demo` in custom Godot .NET editor.  
2. Project → Build.  
3. Select `World` → Display Mode **Solid Colored Planet**.  
4. Tune Radius, Seed, Biome Color Bleed.  
5. Regenerate Now → wait → **PlanetSurface** appears.  
6. Save **preset** (.tres), not the scene mesh.

### 11.2 Engineer — new structure type

1. Implement `IImplicit` or extend `BaseShape` in C#.  
2. Add method to `ShapeKernelBridge` or new `*Bridge.cs`.  
3. `dotnet build` bridge → Godot rebuild C#.  
4. New node or menu item calls bridge method.

### 11.3 Designer — lattice forest

1. Place `PicogkLatticeVolume` (future) with bounding sphere.  
2. Pick `BodyCentreLattice` + `RegularCellArray` + `CellBasedBeamThickness` in Inspector.  
3. Generate → mesh child; optionally boolean with planet chunk.

### 11.4 Batch — cache planet for shipping

1. Headless console or Godot tool iterates chunk keys.  
2. Each job: voxelize → `SaveToVdbFile` + export mesh.  
3. Ship `user://cache/` with game; runtime loads, no PicoGK on player CPU (optional).

---

## 12. Complete public API appendix (from source scan)

The following appendix is **auto-generated** from a scan of all `public` members in the three library trees (see `_build_merged_readme.py`). Line numbers refer to the `.cs` file at generation time.


��# #   P i c o G K   ( 7 2   f i l e s ) 
 
 # # #   B a s e \ F i e l d M e t a d a t a . c s 
 
 -   n C o u n t ( . . . )   ( L 8 3 ) 
 
 -   b G e t N a m e A t ( . . . )   ( L 9 6 ) 
 
 -   s t r T y p e A t ( . . . )   ( L 1 3 4 ) 
 
 -   s t r T y p e N a m e ( . . . )   ( L 1 4 4 ) 
 
 -   b G e t V a l u e A t ( . . . )   ( L 1 6 9 ) 
 
 -   b G e t V a l u e A t ( . . . )   ( L 1 9 2 ) 
 
 -   b G e t V a l u e A t ( . . . )   ( L 2 0 5 ) 
 
 -   S e t V a l u e ( . . . )   ( L 2 1 6 ) 
 
 -   S e t V a l u e ( . . . )   ( L 2 2 7 ) 
 
 -   S e t V a l u e ( . . . )   ( L 2 3 8 ) 
 
 -   R e m o v e V a l u e ( . . . )   ( L 2 4 8 ) 
 
 
 
 # # #   B a s e \ G l o b a l O b j e c t s . c s 
 
 -   v o x S p h e r e ( . . . )   ( L 9 3 ) 
 
 -   v o x L a t t i c e B e a m ( . . . )   ( L 1 0 9 ) 
 
 -   v o x M e s h S h e l l ( . . . )   ( L 1 3 0 ) 
 
 -   v o x C o m b i n e A l l ( . . . )   ( L 1 4 4 ) 
 
 -   v o x F r o m V d b F i l e ( . . . )   ( L 1 7 0 ) 
 
 
 
 # # #   B a s e \ L a t t i c e . c s 
 
 -   A d d S p h e r e ( . . . )   ( L 6 4 ) 
 
 -   A d d B e a m ( . . . )   ( L 7 8 ) 
 
 -   A d d B e a m ( . . . )   ( L 1 0 1 ) 
 
 
 
 # # #   B a s e \ M e s h . c s 
 
 -   m s h C r e a t e T r a n s f o r m e d ( . . . )   ( L 7 5 ) 
 
 -   m s h C r e a t e T r a n s f o r m e d ( . . . )   ( L 1 0 5 ) 
 
 -   m s h C r e a t e M i r r o r e d ( . . . )   ( L 1 2 9 ) 
 
 -   n A d d V e r t e x ( . . . )   ( L 1 5 3 ) 
 
 -   A d d V e r t i c e s ( . . . )   ( L 1 5 8 ) 
 
 -   v e c V e r t e x A t ( . . . )   ( L 1 7 6 ) 
 
 -   n V e r t e x C o u n t ( . . . )   ( L 1 9 0 ) 
 
 -   n A d d T r i a n g l e ( . . . )   ( L 2 0 1 ) 
 
 -   n A d d T r i a n g l e ( . . . )   ( L 2 1 5 ) 
 
 -   n T r i a n g l e C o u n t ( . . . )   ( L 2 2 4 ) 
 
 -   n A d d T r i a n g l e ( . . . )   ( L 2 3 8 ) 
 
 -   A d d Q u a d ( . . . )   ( L 2 5 3 ) 
 
 -   A d d Q u a d ( . . . )   ( L 2 7 6 ) 
 
 -   o T r i a n g l e A t ( . . . )   ( L 2 9 4 ) 
 
 -   G e t T r i a n g l e ( . . . )   ( L 3 1 2 ) 
 
 -   A p p e n d ( . . . )   ( L 3 3 5 ) 
 
 -   o B o u n d i n g B o x ( . . . )   ( L 3 5 2 ) 
 
 
 
 # # #   B a s e \ P o l y L i n e . c s 
 
 -   n A d d V e r t e x ( . . . )   ( L 6 5 ) 
 
 -   A d d ( . . . )   ( L 7 6 ) 
 
 -   n V e r t e x C o u n t ( . . . )   ( L 8 6 ) 
 
 -   v e c V e r t e x A t ( . . . )   ( L 9 5 ) 
 
 -   G e t C o l o r ( . . . )   ( L 1 0 6 ) 
 
 -   o B o u n d i n g B o x ( . . . )   ( L 1 1 6 ) 
 
 -   A d d A r r o w ( . . . )   ( L 1 3 7 ) 
 
 -   A d d C r o s s ( . . . )   ( L 1 9 5 ) 
 
 
 
 # # #   B a s e \ S c a l a r F i e l d . c s 
 
 -   I n f o r m A c t i v e V a l u e ( . . . )   ( L 5 2 ) 
 
 -   S e t V a l u e ( . . . )   ( L 1 3 8 ) 
 
 -   b G e t V a l u e ( . . . )   ( L 1 5 5 ) 
 
 -   R e m o v e V a l u e ( . . . )   ( L 1 6 6 ) 
 
 -   G e t V o x e l D i m e n s i o n s ( . . . )   ( L 1 8 0 ) 
 
 -   G e t V o x e l D i m e n s i o n s ( . . . )   ( L 2 1 0 ) 
 
 -   G e t V o x e l S l i c e ( . . . )   ( L 2 4 2 ) 
 
 -   T r a v e r s e A c t i v e ( . . . )   ( L 2 6 2 ) 
 
 -   f S i g n e d D i s t a n c e ( . . . )   ( L 2 7 9 ) 
 
 -   o B o u n d i n g B o x ( . . . )   ( L 2 8 9 ) 
 
 
 
 # # #   B a s e \ V e c t o r F i e l d . c s 
 
 -   I n f o r m A c t i v e V a l u e ( . . . )   ( L 5 1 ) 
 
 -   S e t V a l u e ( . . . )   ( L 1 4 9 ) 
 
 -   b G e t V a l u e ( . . . )   ( L 1 6 9 ) 
 
 -   R e m o v e V a l u e ( . . . )   ( L 1 8 3 ) 
 
 -   T r a v e r s e A c t i v e ( . . . )   ( L 1 9 5 ) 
 
 
 
 # # #   B a s e \ V o x e l s . c s 
 
 -   f S i g n e d D i s t a n c e ( . . . )   ( L 5 7 ) 
 
 -   v o x D u p l i c a t e ( . . . )   ( L 1 1 8 ) 
 
 -   m s h A s M e s h ( . . . )   ( L 1 8 1 ) 
 
 -   v o x S p h e r e ( . . . )   ( L 1 9 3 ) 
 
 -   v o x L a t t i c e B e a m ( . . . )   ( L 2 1 1 ) 
 
 -   v o x M e s h S h e l l ( . . . )   ( L 2 3 4 ) 
 
 -   b I s E m p t y ( . . . )   ( L 2 4 9 ) 
 
 -   f V o x e l S i z e ( . . . )   ( L 2 6 7 ) 
 
 -   B o o l A d d ( . . . )   ( L 2 7 4 ) 
 
 -   v o x B o o l A d d ( . . . )   ( L 2 9 1 ) 
 
 -   B o o l A d d A l l ( . . . )   ( L 3 0 3 ) 
 
 -   v o x B o o l A d d A l l ( . . . )   ( L 3 1 5 ) 
 
 -   v o x C o m b i n e ( . . . )   ( L 3 2 8 ) 
 
 -   v o x C o m b i n e A l l ( . . . )   ( L 3 4 0 ) 
 
 -   B o o l S u b t r a c t ( . . . )   ( L 3 5 4 ) 
 
 -   v o x B o o l S u b t r a c t ( . . . )   ( L 3 6 9 ) 
 
 -   B o o l S u b t r a c t A l l ( . . . )   ( L 3 8 1 ) 
 
 -   v o x B o o l S u b t r a c t A l l ( . . . )   ( L 3 9 2 ) 
 
 -   B o o l I n t e r s e c t ( . . . )   ( L 4 0 5 ) 
 
 -   v o x B o o l I n t e r s e c t ( . . . )   ( L 4 2 0 ) 
 
 -   T r i m ( . . . )   ( L 4 5 8 ) 
 
 -   v o x T r i m ( . . . )   ( L 4 6 9 ) 
 
 -   O f f s e t ( . . . )   ( L 4 8 2 ) 
 
 -   v o x O f f s e t ( . . . )   ( L 4 9 2 ) 
 
 -   D o u b l e O f f s e t ( . . . )   ( L 5 0 5 ) 
 
 -   v o x D o u b l e O f f s e t ( . . . )   ( L 5 1 7 ) 
 
 -   T r i p l e O f f s e t ( . . . )   ( L 5 3 9 ) 
 
 -   v o x T r i p l e O f f s e t ( . . . )   ( L 5 5 7 ) 
 
 -   S m o o t h e n ( . . . )   ( L 5 6 9 ) 
 
 -   v o x S m o o t h e n ( . . . )   ( L 5 7 7 ) 
 
 -   O v e r O f f s e t ( . . . )   ( L 5 9 0 ) 
 
 -   v o x O v e r O f f s e t ( . . . )   ( L 6 1 3 ) 
 
 -   F i l l e t ( . . . )   ( L 6 3 1 ) 
 
 -   v o x F i l l e t ( . . . )   ( L 6 4 2 ) 
 
 -   v o x S h e l l ( . . . )   ( L 6 5 9 ) 
 
 -   v o x S h e l l ( . . . )   ( L 6 8 0 ) 
 
 -   R e n d e r M e s h ( . . . )   ( L 7 0 7 ) 
 
 -   R e n d e r I m p l i c i t ( . . . )   ( L 7 2 3 ) 
 
 -   I n t e r s e c t I m p l i c i t ( . . . )   ( L 7 3 8 ) 
 
 -   v o x I n t e r s e c t I m p l i c i t ( . . . )   ( L 7 4 8 ) 
 
 -   . . .   + 1 8   m o r e   m e m b e r s 
 
 
 
 # # #   D i a g n o s t i c s \ T e s t C L I . c s 
 
 -   R u n ( . . . )   ( L 4 5 ) 
 
 
 
 # # #   D i a g n o s t i c s \ T e s t P r o g r e s s . c s 
 
 -   T e s t ( . . . )   ( L 4 0 ) 
 
 
 
 # # #   D i a g n o s t i c s \ T e s t V e c t o r A n d C o m p a r i s o n . c s 
 
 -   T e s t ( . . . )   ( L 4 4 ) 
 
 
 
 # # #   I O \ C l i . c s 
 
 -   o B B o x F i l e ( . . . )   ( L 8 1 ) 
 
 -   b B i n a r y ( . . . )   ( L 8 6 ) 
 
 -   f U n i t s H e a d e r ( . . . )   ( L 9 1 ) 
 
 -   b 3 2 B i t A l i g n ( . . . )   ( L 9 6 ) 
 
 -   s t r H e a d e r D a t e ( . . . )   ( L 1 0 6 ) 
 
 -   s t r W a r n i n g s ( . . . )   ( L 1 1 6 ) 
 
 -   W r i t e S l i c e s T o C l i F i l e ( . . . )   ( L 1 2 9 ) 
 
 -   S a v e T o C l i F i l e ( . . . )   ( L 8 8 5 ) 
 
 
 
 # # #   I O \ I m a g e I o . c s 
 
 -   S a v e T g a ( . . . )   ( L 4 3 ) 
 
 -   S a v e T g a ( . . . )   ( L 5 5 ) 
 
 -   G e t F i l e I n f o ( . . . )   ( L 1 1 2 ) 
 
 -   G e t F i l e I n f o ( . . . )   ( L 1 3 0 ) 
 
 -   L o a d T g a ( . . . )   ( L 1 5 5 ) 
 
 -   L o a d T g a ( . . . )   ( L 1 6 7 ) 
 
 -   b Y A x i s F l i p p e d ( . . . )   ( L 2 4 5 ) 
 
 
 
 # # #   I O \ M e s h I o . c s 
 
 -   * * e n u m * *   E S t l U n i t   ( L 4 5 ) 
 
 -   m s h F r o m S t l F i l e ( . . . )   ( L 6 3 ) 
 
 -   m s h F r o m S t l F i l e ( . . . )   ( L 8 4 ) 
 
 -   S a v e T o S t l F i l e ( . . . )   ( L 2 1 0 ) 
 
 -   S a v e T o S t l F i l e ( . . . )   ( L 2 2 1 ) 
 
 -   N o r m a l X ( . . . )   ( L 3 1 4 ) 
 
 -   N o r m a l Y ( . . . )   ( L 3 1 5 ) 
 
 -   N o r m a l Z ( . . . )   ( L 3 1 6 ) 
 
 -   V 1 X ( . . . )   ( L 3 1 7 ) 
 
 -   V 1 Y ( . . . )   ( L 3 1 8 ) 
 
 -   V 1 Z ( . . . )   ( L 3 1 9 ) 
 
 -   V 2 X ( . . . )   ( L 3 2 0 ) 
 
 -   V 2 Y ( . . . )   ( L 3 2 1 ) 
 
 -   V 2 Z ( . . . )   ( L 3 2 2 ) 
 
 -   V 3 X ( . . . )   ( L 3 2 3 ) 
 
 -   V 3 Y ( . . . )   ( L 3 2 4 ) 
 
 -   V 3 Z ( . . . )   ( L 3 2 5 ) 
 
 -   m _ s t r L o a d H e a d e r D a t a ( . . . )   ( L 4 0 1 ) 
 
 
 
 # # #   I O \ O p e n V d b F i l e . c s 
 
 -   l i b C r e a t e C o m p a t i b l e L i b r a r y F o r ( . . . )   ( L 8 6 ) 
 
 -   S a v e T o F i l e ( . . . )   ( L 1 6 5 ) 
 
 -   v o x G e t ( . . . )   ( L 1 9 9 ) 
 
 -   v o x G e t ( . . . )   ( L 2 2 1 ) 
 
 -   n A d d ( . . . )   ( L 2 4 2 ) 
 
 -   o G e t S c a l a r F i e l d ( . . . )   ( L 2 6 7 ) 
 
 -   o G e t S c a l a r F i e l d ( . . . )   ( L 2 8 8 ) 
 
 -   n A d d ( . . . )   ( L 3 0 9 ) 
 
 -   o G e t V e c t o r F i e l d ( . . . )   ( L 3 2 8 ) 
 
 -   o G e t V e c t o r F i e l d ( . . . )   ( L 3 4 8 ) 
 
 -   n A d d ( . . . )   ( L 3 6 9 ) 
 
 -   n F i e l d C o u n t ( . . . )   ( L 3 8 4 ) 
 
 -   s t r F i e l d N a m e ( . . . )   ( L 3 9 7 ) 
 
 -   s t r F i e l d T y p e ( . . . )   ( L 4 3 0 ) 
 
 -   b I s P i c o G K C o m p a t i b l e ( . . . )   ( L 4 7 8 ) 
 
 -   f P i c o G K V o x e l S i z e M M ( . . . )   ( L 4 9 4 ) 
 
 
 
 # # #   I O \ V o x e l s I o . c s 
 
 -   v o x F r o m V d b F i l e ( . . . )   ( L 6 0 ) 
 
 -   S a v e T o V d b F i l e ( . . . )   ( L 9 1 ) 
 
 
 
 # # #   I n t e r n a l s \ I n t e r o p . c s 
 
 -   D i s p o s e ( . . . )   ( L 1 9 6 ) 
 
 -   D i s p o s e ( . . . )   ( L 2 6 2 ) 
 
 -   D i s p o s e ( . . . )   ( L 4 8 4 ) 
 
 -   D i s p o s e ( . . . )   ( L 5 6 0 ) 
 
 -   D i s p o s e ( . . . )   ( L 7 7 4 ) 
 
 -   D i s p o s e ( . . . )   ( L 8 3 3 ) 
 
 -   D i s p o s e ( . . . )   ( L 8 9 1 ) 
 
 -   D i s p o s e ( . . . )   ( L 9 4 4 ) 
 
 -   D i s p o s e ( . . . )   ( L 1 0 5 7 ) 
 
 -   D i s p o s e ( . . . )   ( L 1 1 6 0 ) 
 
 -   D i s p o s e ( . . . )   ( L 1 2 4 5 ) 
 
 -   D i s p o s e ( . . . )   ( L 1 3 6 5 ) 
 
 
 
 # # #   I n t e r n a l s \ T y p e s . c s 
 
 -   X ( . . . )   ( L 9 0 ) 
 
 -   Y ( . . . )   ( L 9 1 ) 
 
 -   Z ( . . . )   ( L 9 2 ) 
 
 -   A ( . . . )   ( L 1 0 7 ) 
 
 -   B ( . . . )   ( L 1 0 8 ) 
 
 -   C ( . . . )   ( L 1 0 9 ) 
 
 
 
 # # #   L i b r a r y \ L i b r a r y . c s 
 
 -   v e c V o x e l s T o M m ( . . . )   ( L 2 4 9 ) 
 
 -   M m T o V o x e l s ( . . . )   ( L 2 6 9 ) 
 
 -   D i s p o s e ( . . . )   ( L 3 0 1 ) 
 
 
 
 # # #   L i b r a r y \ L i b r a r y G l o b a l . c s 
 
 -   o L i b r a r y ( . . . )   ( L 4 6 ) 
 
 -   D i s p o s e ( . . . )   ( L 1 7 9 ) 
 
 -   o L i b r a r y ( . . . )   ( L 2 3 7 ) 
 
 -   R e g i s t e r G l o b a l L i b r a r y ( . . . )   ( L 2 4 8 ) 
 
 -   U n r e g i s t e r G l o b a l L i b r a r y ( . . . )   ( L 2 5 9 ) 
 
 -   R e g i s t e r G l o b a l V i e w e r ( . . . )   ( L 2 7 8 ) 
 
 -   U n r e g i s t e r G l o b a l V i e w e r ( . . . )   ( L 2 8 9 ) 
 
 -   R e g i s t e r G l o b a l L o g ( . . . )   ( L 3 0 8 ) 
 
 -   U n r e g i s t e r G l o b a l L o g ( . . . )   ( L 3 1 9 ) 
 
 -   G o ( . . . )   ( L 3 5 7 ) 
 
 -   L o g ( . . . )   ( L 4 3 4 ) 
 
 -   b C o n t i n u e T a s k ( . . . )   ( L 4 4 6 ) 
 
 -   E n d T a s k ( . . . )   ( L 4 5 5 ) 
 
 -   C a n c e l E n d T a s k R e q u e s t ( . . . )   ( L 4 6 3 ) 
 
 -   s t r F i n d L i g h t S e t u p F i l e ( . . . )   ( L 4 7 1 ) 
 
 
 
 # # #   L i b r a r y \ L i b r a r y I n f o . c s 
 
 -   s t r N a m e ( . . . )   ( L 4 7 ) 
 
 -   s t r V e r s i o n ( . . . )   ( L 5 8 ) 
 
 -   s t r B u i l d I n f o ( . . . )   ( L 7 0 ) 
 
 
 
 # # #   N u m e r i c s \ A n g l e s . c s 
 
 -   f N o r m a l i z e d A n g l e R a d ( . . . )   ( L 5 4 ) 
 
 
 
 # # #   N u m e r i c s \ C o m p a r i s o n . c s 
 
 -   b A l m o s t E q u a l ( . . . )   ( L 7 9 ) 
 
 -   b A l m o s t L e s s O r E q u a l ( . . . )   ( L 9 6 ) 
 
 -   b A l m o s t M o r e O r E q u a l ( . . . )   ( L 1 0 1 ) 
 
 -   b A l m o s t Z e r o ( . . . )   ( L 1 1 0 ) 
 
 -   b A l m o s t E q u a l ( . . . )   ( L 1 2 0 ) 
 
 -   b A l m o s t Z e r o ( . . . )   ( L 1 3 2 ) 
 
 -   b A l m o s t E q u a l ( . . . )   ( L 1 4 2 ) 
 
 -   b A l m o s t Z e r o ( . . . )   ( L 1 5 3 ) 
 
 
 
 # # #   N u m e r i c s \ C o o r d i n a t e s . c s 
 
 -   R ( . . . )   ( L 4 6 ) 
 
 -   P h i ( . . . )   ( L 5 2 ) 
 
 -   T h e t a ( . . . )   ( L 5 8 ) 
 
 -   T o S t r i n g ( . . . )   ( L 1 4 8 ) 
 
 -   R ( . . . )   ( L 1 6 0 ) 
 
 -   P h i ( . . . )   ( L 1 6 5 ) 
 
 -   Z ( . . . )   ( L 1 6 9 ) 
 
 -   T o S t r i n g ( . . . )   ( L 2 6 2 ) 
 
 -   R ( . . . )   ( L 2 7 4 ) 
 
 -   P h i ( . . . )   ( L 2 8 0 ) 
 
 -   T o S t r i n g ( . . . )   ( L 3 5 4 ) 
 
 
 
 # # #   N u m e r i c s \ V e c t o r . c s 
 
 -   v e c N o r m a l i z e d ( . . . )   ( L 5 3 ) 
 
 -   v e c S a f e N o r m a l i z e d ( . . . )   ( L 6 8 ) 
 
 -   v e c A s V e c t o r 3 ( . . . )   ( L 1 2 3 ) 
 
 -   v e c P t W o r l d ( . . . )   ( L 1 3 3 ) 
 
 -   v e c D i r W o r l d ( . . . )   ( L 1 4 3 ) 
 
 -   v e c P t L o c a l ( . . . )   ( L 1 5 3 ) 
 
 -   v e c D i r L o c a l ( . . . )   ( L 1 6 3 ) 
 
 -   v e c T r a n s f o r m e d ( . . . )   ( L 1 7 1 ) 
 
 -   v e c M i r r o r e d ( . . . )   ( L 1 8 5 ) 
 
 
 
 # # #   S h a p e s \ 2 D \ B a s e I n t e r f a c e s . c s 
 
 -   P t A t T ( . . . )   ( L 8 5 ) 
 
 
 
 # # #   S h a p e s \ 2 D \ C o n t o u r S a m p l e r . c s 
 
 -   f T o t a l L e n g t h ( . . . )   ( L 8 1 ) 
 
 -   f A r c T F r o m L i n e a r T ( . . . )   ( L 8 6 ) 
 
 
 
 # # #   S h a p e s \ 2 D \ C o n t o u r s . c s 
 
 -   f R ( . . . )   ( L 5 7 ) 
 
 -   f L e n g t h ( . . . )   ( L 5 9 ) 
 
 -   P t A t T ( . . . )   ( L 6 9 ) 
 
 -   f P h i ( . . . )   ( L 1 3 1 ) 
 
 -   f A ( . . . )   ( L 1 3 6 ) 
 
 -   f B ( . . . )   ( L 1 4 1 ) 
 
 -   f L e n g t h ( . . . )   ( L 1 4 3 ) 
 
 -   f L e n g t h ( . . . )   ( L 2 4 7 ) 
 
 -   f L e n g t h ( . . . )   ( L 2 9 3 ) 
 
 
 
 # # #   S h a p e s \ 2 D \ P a t h s . c s 
 
 -   f L e n g t h ( . . . )   ( L 7 7 ) 
 
 -   f A n g l e ( . . . )   ( L 1 3 8 ) 
 
 -   f L e n g t h ( . . . )   ( L 1 4 0 ) 
 
 -   A d d ( . . . )   ( L 1 7 7 ) 
 
 -   A d d L i n e ( . . . )   ( L 1 9 2 ) 
 
 -   A d d L i n e R e l ( . . . )   ( L 2 0 1 ) 
 
 -   A d d A r c ( . . . )   ( L 2 1 2 ) 
 
 -   A d d A r c R e l ( . . . )   ( L 2 2 3 ) 
 
 -   f L e n g t h ( . . . )   ( L 2 5 6 ) 
 
 
 
 # # #   S h a p e s \ 3 D \ B a s e I n t e r f a c e s . c s 
 
 -   v e c P t A t T ( . . . )   ( L 5 1 ) 
 
 -   P t A t T ( . . . )   ( L 7 2 ) 
 
 
 
 # # #   S h a p e s \ 3 D \ F r a m e 3 d . c s 
 
 -   v e c P t T o W o r l d ( . . . )   ( L 1 4 9 ) 
 
 -   v e c P t T o W o r l d ( . . . )   ( L 1 5 6 ) 
 
 -   v e c D i r T o W o r l d ( . . . )   ( L 1 6 5 ) 
 
 -   v e c D i r T o W o r l d ( . . . )   ( L 1 7 8 ) 
 
 -   v e c P t F r o m W o r l d ( . . . )   ( L 1 9 0 ) 
 
 -   v e c D i r F r o m W o r l d ( . . . )   ( L 2 0 3 ) 
 
 -   A s R i g i d ( . . . )   ( L 3 6 3 ) 
 
 -   E q u a l s ( . . . )   ( L 4 4 0 ) 
 
 -   E q u a l s ( . . . )   ( L 4 5 0 ) 
 
 -   G e t H a s h C o d e ( . . . )   ( L 4 5 7 ) 
 
 
 
 # # #   S h a p e s \ 3 D \ O r i e n t e d P a t h . c s 
 
 -   v e c P t A t T ( . . . )   ( L 5 8 ) 
 
 -   v e c P t A t T ( . . . )   ( L 8 3 ) 
 
 -   P t A t T ( . . . )   ( L 8 6 ) 
 
 
 
 # # #   T y p e s \ A n i m a t i o n . c s 
 
 -   * * e n u m * *   E T y p e   ( L 4 8 ) 
 
 -   E n d ( . . . )   ( L 6 1 ) 
 
 -   b A n i m a t e ( . . . )   ( L 6 6 ) 
 
 -   C l e a r ( . . . )   ( L 1 2 4 ) 
 
 -   b P u l s e ( . . . )   ( L 1 3 7 ) 
 
 -   b I s I d l e ( . . . )   ( L 1 6 7 ) 
 
 -   A d d ( . . . )   ( L 1 7 3 ) 
 
 
 
 # # #   T y p e s \ B B o x . c s 
 
 -   b I s E m p t y ( . . . )   ( L 9 6 ) 
 
 -   b C o n t a i n s ( . . . )   ( L 1 1 2 ) 
 
 -   I n c l u d e ( . . . )   ( L 1 3 1 ) 
 
 -   I n c l u d e ( . . . )   ( L 1 4 5 ) 
 
 -   G r o w ( . . . )   ( L 1 5 5 ) 
 
 -   T o S t r i n g ( . . . )   ( L 1 9 2 ) 
 
 -   v e c S i z e ( . . . )   ( L 2 7 0 ) 
 
 -   b I s E m p t y ( . . . )   ( L 2 7 8 ) 
 
 -   b C o n t a i n s ( . . . )   ( L 2 9 6 ) 
 
 -   I n c l u d e ( . . . )   ( L 3 1 7 ) 
 
 -   I n c l u d e ( . . . )   ( L 3 3 5 ) 
 
 -   I n c l u d e ( . . . )   ( L 3 4 7 ) 
 
 -   G r o w ( . . . )   ( L 3 6 0 ) 
 
 -   v e c C e n t e r ( . . . )   ( L 3 8 5 ) 
 
 -   o F i t I n t o ( . . . )   ( L 3 9 7 ) 
 
 -   v e c R a n d o m V e c t o r I n s i d e ( . . . )   ( L 4 2 6 ) 
 
 -   o A s B o u n d i n g B o x 2 ( . . . )   ( L 4 3 7 ) 
 
 -   T o S t r i n g ( . . . )   ( L 4 4 6 ) 
 
 -   v e c M i n ( . . . )   ( L 4 5 4 ) 
 
 -   v e c M a x ( . . . )   ( L 4 5 9 ) 
 
 
 
 # # #   T y p e s \ C o l o r . c s 
 
 -   R ( . . . )   ( L 5 0 ) 
 
 -   G ( . . . )   ( L 5 5 ) 
 
 -   B ( . . . )   ( L 6 0 ) 
 
 -   A ( . . . )   ( L 6 5 ) 
 
 -   s t r A s H e x C o d e ( . . . )   ( L 2 3 9 ) 
 
 -   s t r A s A B G R H e x C o d e ( . . . )   ( L 2 6 5 ) 
 
 -   T o S t r i n g ( . . . )   ( L 2 7 5 ) 
 
 -   c l r W e i g h t e d ( . . . )   ( L 2 8 7 ) 
 
 -   c l r R a n d o m ( . . . )   ( L 3 1 4 ) 
 
 -   H ( . . . )   ( L 5 7 7 ) 
 
 -   S ( . . . )   ( L 5 8 1 ) 
 
 -   V ( . . . )   ( L 5 8 5 ) 
 
 -   H ( . . . )   ( L 7 1 8 ) 
 
 -   L ( . . . )   ( L 7 2 3 ) 
 
 -   S ( . . . )   ( L 7 2 8 ) 
 
 
 
 # # #   T y p e s \ C s v . c s 
 
 -   S a v e ( . . . )   ( L 1 0 4 ) 
 
 -   n R o w C o u n t ( . . . )   ( L 1 3 5 ) 
 
 -   n M a x C o l u m n C o u n t ( . . . )   ( L 1 4 0 ) 
 
 -   s t r G e t A t ( . . . )   ( L 1 4 5 ) 
 
 -   S e t K e y C o l u m n ( . . . )   ( L 1 5 8 ) 
 
 -   b G e t A t ( . . . )   ( L 1 6 3 ) 
 
 -   b G e t A t ( . . . )   ( L 1 7 5 ) 
 
 -   b F i n d C o l u m n ( . . . )   ( L 2 1 2 ) 
 
 -   s t r C o l u m n I d ( . . . )   ( L 2 3 1 ) 
 
 -   S e t C o l u m n I d s ( . . . )   ( L 2 3 9 ) 
 
 -   A d d R o w ( . . . )   ( L 2 4 5 ) 
 
 
 
 # # #   T y p e s \ E a s i n g . c s 
 
 -   f E a s e S i n e I n ( . . . )   ( L 4 4 ) 
 
 -   f E a s e S i n e O u t ( . . . )   ( L 4 9 ) 
 
 -   f E a s e S i n e I n O u t ( . . . )   ( L 5 4 ) 
 
 -   f E a s e Q u a d I n ( . . . )   ( L 5 9 ) 
 
 -   f E a s e Q u a d O u t ( . . . )   ( L 6 4 ) 
 
 -   f E a s e Q u a d I n O u t ( . . . )   ( L 6 9 ) 
 
 -   f E a s e C u b i c I n ( . . . )   ( L 7 6 ) 
 
 -   f E a s e C u b i c O u t ( . . . )   ( L 8 1 ) 
 
 -   f E a s e C u b i c I n O u t ( . . . )   ( L 8 6 ) 
 
 -   * * e n u m * *   E E a s i n g   ( L 9 3 ) 
 
 -   f E a s i n g F u n c t i o n ( . . . )   ( L 1 0 4 ) 
 
 
 
 # # #   T y p e s \ I m a g e . c s 
 
 -   c l r V a l u e ( . . . )   ( L 5 1 ) 
 
 -   f V a l u e ( . . . )   ( L 5 2 ) 
 
 -   b V a l u e ( . . . )   ( L 5 3 ) 
 
 -   S e t V a l u e ( . . . )   ( L 5 5 ) 
 
 -   S e t V a l u e ( . . . )   ( L 5 6 ) 
 
 -   S e t V a l u e ( . . . )   ( L 5 7 ) 
 
 -   S e t V a l u e ( . . . )   ( L 6 4 ) 
 
 -   S e t B g r 2 4 ( . . . )   ( L 7 9 ) 
 
 -   S e t B g r a 3 2 ( . . . )   ( L 9 5 ) 
 
 -   S e t R g b 2 4 ( . . . )   ( L 1 2 1 ) 
 
 -   S e t R g b a 3 2 ( . . . )   ( L 1 2 6 ) 
 
 -   c l r G e t A t N o r m a l i z e d ( . . . )   ( L 1 3 8 ) 
 
 -   D r a w L i n e ( . . . )   ( L 1 6 7 ) 
 
 -   D r a w L i n e ( . . . )   ( L 1 9 8 ) 
 
 -   D r a w L i n e ( . . . )   ( L 2 2 9 ) 
 
 -   f V a l u e ( . . . )   ( L 2 8 7 ) 
 
 -   c l r V a l u e ( . . . )   ( L 2 9 2 ) 
 
 -   S e t V a l u e ( . . . )   ( L 2 9 7 ) 
 
 -   S e t V a l u e ( . . . )   ( L 3 0 2 ) 
 
 -   c l r V a l u e ( . . . )   ( L 3 1 8 ) 
 
 -   b V a l u e ( . . . )   ( L 3 2 4 ) 
 
 -   S e t V a l u e ( . . . )   ( L 3 2 9 ) 
 
 -   S e t V a l u e ( . . . )   ( L 3 3 4 ) 
 
 -   b C o n t a i n s A c t i v e P i x e l s ( . . . )   ( L 3 4 7 ) 
 
 -   f V a l u e ( . . . )   ( L 3 7 2 ) 
 
 -   b V a l u e ( . . . )   ( L 3 7 8 ) 
 
 -   S e t V a l u e ( . . . )   ( L 3 8 3 ) 
 
 -   S e t V a l u e ( . . . )   ( L 3 8 8 ) 
 
 -   S e t V a l u e ( . . . )   ( L 4 0 4 ) 
 
 -   f V a l u e ( . . . )   ( L 4 1 7 ) 
 
 -   c l r V a l u e ( . . . )   ( L 5 2 5 ) 
 
 -   S e t V a l u e ( . . . )   ( L 5 3 0 ) 
 
 -   S e t R g b a 3 2 ( . . . )   ( L 5 3 5 ) 
 
 -   c l r V a l u e ( . . . )   ( L 5 8 7 ) 
 
 -   S e t V a l u e ( . . . )   ( L 5 9 2 ) 
 
 -   S e t R g b 2 4 ( . . . )   ( L 5 9 7 ) 
 
 -   S e t V a l u e ( . . . )   ( L 6 4 9 ) 
 
 -   c l r V a l u e ( . . . )   ( L 6 6 2 ) 
 
 
 
 # # #   T y p e s \ L o g . c s 
 
 -   L o g ( . . . )   ( L 6 3 ) 
 
 -   L o g ( . . . )   ( L 1 1 7 ) 
 
 -   L o g T i m e ( . . . )   ( L 1 4 4 ) 
 
 -   D i s p o s e ( . . . )   ( L 1 5 5 ) 
 
 
 
 # # #   T y p e s \ P r o g r e s s . c s 
 
 -   P r o g r e s s ( . . . )   ( L 6 0 ) 
 
 -   P r o g r e s s ( . . . )   ( L 9 2 ) 
 
 -   D i s p o s e ( . . . )   ( L 1 0 6 ) 
 
 -   S e t I t e m ( . . . )   ( L 1 3 9 ) 
 
 -   P r o g r e s s ( . . . )   ( L 1 8 5 ) 
 
 
 
 # # #   T y p e s \ S k i a B i t m a p . c s 
 
 -   S a v e P n g ( . . . )   ( L 1 0 8 ) 
 
 -   S a v e J p g ( . . . )   ( L 1 2 1 ) 
 
 -   S a v e T g a ( . . . )   ( L 1 3 4 ) 
 
 -   i m g L o a d F r o m F i l e ( . . . )   ( L 1 3 9 ) 
 
 
 
 # # #   T y p e s \ S l i c e . c s 
 
 -   s t r W i n d i n g A s S t r i n g ( . . . )   ( L 5 1 ) 
 
 -   A d d V e r t e x ( . . . )   ( L 1 1 6 ) 
 
 -   D e t e c t W i n d i n g ( . . . )   ( L 1 2 1 ) 
 
 -   C l o s e ( . . . )   ( L 1 3 4 ) 
 
 -   A s S v g P o l y l i n e ( . . . )   ( L 1 4 4 ) 
 
 -   A s S v g P a t h ( . . . )   ( L 1 6 7 ) 
 
 -   o B B o x ( . . . )   ( L 1 9 1 ) 
 
 -   n C o u n t ( . . . )   ( L 1 9 2 ) 
 
 -   A d d C o n t o u r ( . . . )   ( L 2 0 5 ) 
 
 -   b I s E m p t y ( . . . )   ( L 2 1 1 ) 
 
 -   C l o s e ( . . . )   ( L 2 1 6 ) 
 
 -   S a v e T o S v g F i l e ( . . . )   ( L 2 2 4 ) 
 
 -   f Z P o s ( . . . )   ( L 4 9 2 ) 
 
 -   o B B o x ( . . . )   ( L 4 9 3 ) 
 
 -   n C o n t o u r s ( . . . )   ( L 4 9 4 ) 
 
 -   m _ f M i n Y ( . . . )   ( L 5 3 3 ) 
 
 -   m _ f M a x Y ( . . . )   ( L 5 3 4 ) 
 
 -   m _ b U s e d ( . . . )   ( L 5 3 5 ) 
 
 -   A d d S l i c e s ( . . . )   ( L 5 5 8 ) 
 
 -   A d d T o V i e w e r ( . . . )   ( L 5 6 7 ) 
 
 -   n C o u n t ( . . . )   ( L 6 0 8 ) 
 
 -   o B B o x ( . . . )   ( L 6 1 0 ) 
 
 
 
 # # #   U t i l s \ F i e l d U t i l s . c s 
 
 -   b D o e s S l i c e C o n t a i n D e f e c t ( . . . )   ( L 1 5 4 ) 
 
 -   b V i s u a l i z e S d f S l i c e s A s T g a S t a c k ( . . . )   ( L 2 0 2 ) 
 
 -   n C o u n t ( . . . )   ( L 2 5 4 ) 
 
 -   I n f o r m A c t i v e V a l u e ( . . . )   ( L 2 7 2 ) 
 
 -   o E x t r a c t ( . . . )   ( L 2 8 3 ) 
 
 -   I n f o r m A c t i v e V a l u e ( . . . )   ( L 3 3 1 ) 
 
 -   M e r g e ( . . . )   ( L 3 5 8 ) 
 
 -   I n f o r m A c t i v e V a l u e ( . . . )   ( L 3 7 7 ) 
 
 -   A d d T o V i e w e r ( . . . )   ( L 3 8 8 ) 
 
 -   I n f o r m A c t i v e V a l u e ( . . . )   ( L 4 2 6 ) 
 
 
 
 # # #   U t i l s \ M e s h M a t h . c s 
 
 -   b F i n d T r i a n g l e F r o m S u r f a c e P o i n t ( . . . )   ( L 4 2 ) 
 
 
 
 # # #   U t i l s \ S l i c e V i z . c s 
 
 -   n S l i c e C o u n t ( . . . )   ( L 8 4 ) 
 
 -   V i s u a l i z e ( . . . )   ( L 9 0 ) 
 
 -   V i s u a l i z e ( . . . )   ( L 9 9 ) 
 
 -   D i s p o s e ( . . . )   ( L 1 3 4 ) 
 
 
 
 # # #   U t i l s \ U t i l s . c s 
 
 -   s t r S h o r t e n ( . . . )   ( L 2 1 6 ) 
 
 -   s t r F o l d e r ( . . . )   ( L 3 0 5 ) 
 
 -   D i s p o s e ( . . . )   ( L 3 1 2 ) 
 
 
 
 # # #   U t i l s \ V d b 2 C L I . c s 
 
 -   C o n v e r t ( . . . )   ( L 5 4 ) 
 
 
 
 # # #   U t i l s \ V o x C u t V i z . c s 
 
 -   n S l i c e C o u n t ( . . . )   ( L 8 9 ) 
 
 -   C u t ( . . . )   ( L 9 6 ) 
 
 -   C u t ( . . . )   ( L 1 0 9 ) 
 
 -   D i s p o s e ( . . . )   ( L 1 3 6 ) 
 
 
 
 # # #   V i e w e r \ V i e w e r . c s 
 
 -   b P o l l ( . . . )   ( L 1 9 5 ) 
 
 -   R e q u e s t U p d a t e ( . . . )   ( L 2 4 7 ) 
 
 -   L o a d L i g h t S e t u p ( . . . )   ( L 2 5 8 ) 
 
 -   L o a d L i g h t S e t u p ( . . . )   ( L 2 6 7 ) 
 
 -   A d d ( . . . )   ( L 3 2 5 ) 
 
 -   R e m o v e ( . . . )   ( L 3 3 7 ) 
 
 -   S e t O b j e c t M a t r i x ( . . . )   ( L 3 4 8 ) 
 
 -   A d d ( . . . )   ( L 3 6 0 ) 
 
 -   R e m o v e ( . . . )   ( L 3 7 2 ) 
 
 -   S e t O b j e c t M a t r i x ( . . . )   ( L 3 8 3 ) 
 
 -   A d d ( . . . )   ( L 3 9 5 ) 
 
 -   R e m o v e ( . . . )   ( L 4 0 7 ) 
 
 -   S e t O b j e c t M a t r i x ( . . . )   ( L 4 1 8 ) 
 
 -   R e m o v e A l l O b j e c t s ( . . . )   ( L 4 3 0 ) 
 
 -   R e q u e s t S c r e e n S h o t ( . . . )   ( L 4 4 2 ) 
 
 -   E n a b l e E x p e r i m e n t a l ( . . . )   ( L 4 5 3 ) 
 
 -   S e t G r o u p V i s i b l e ( . . . )   ( L 4 6 1 ) 
 
 -   S e t G r o u p M a t e r i a l ( . . . )   ( L 4 7 7 ) 
 
 -   S e t G r o u p M a t r i x ( . . . )   ( L 4 9 4 ) 
 
 -   E n a b l e O v e r h a n g W a r n i n g ( . . . )   ( L 5 1 0 ) 
 
 -   D i s a b l e O v e r h a n g W a r n i n g ( . . . )   ( L 5 2 5 ) 
 
 -   o B B o x ( . . . )   ( L 5 3 6 ) 
 
 -   S e t B a c k g r o u n d C o l o r ( . . . )   ( L 5 4 6 ) 
 
 -   Z o o m T o F i t ( . . . )   ( L 5 5 5 ) 
 
 -   S e t F o v ( . . . )   ( L 5 6 4 ) 
 
 -   b I s I d l e ( . . . )   ( L 5 9 7 ) 
 
 
 
 # # #   V i e w e r \ V i e w e r A c t i o n s . c s 
 
 -   D o ( . . . )   ( L 6 5 ) 
 
 -   D o ( . . . )   ( L 8 9 ) 
 
 -   D o ( . . . )   ( L 1 1 3 ) 
 
 -   D o ( . . . )   ( L 1 3 5 ) 
 
 -   D o ( . . . )   ( L 1 5 5 ) 
 
 -   D o ( . . . )   ( L 1 7 1 ) 
 
 -   D o ( . . . )   ( L 1 8 4 ) 
 
 -   D o ( . . . )   ( L 2 0 3 ) 
 
 -   D o ( . . . )   ( L 2 2 2 ) 
 
 -   D o ( . . . )   ( L 2 4 2 ) 
 
 -   D o ( . . . )   ( L 2 6 3 ) 
 
 -   D o ( . . . )   ( L 2 8 2 ) 
 
 -   D o ( . . . )   ( L 3 0 1 ) 
 
 -   D o ( . . . )   ( L 3 2 2 ) 
 
 -   D o ( . . . )   ( L 3 4 2 ) 
 
 -   D o ( . . . )   ( L 3 6 1 ) 
 
 -   D o ( . . . )   ( L 3 7 5 ) 
 
 -   D o ( . . . )   ( L 3 9 2 ) 
 
 -   D o ( . . . )   ( L 4 1 6 ) 
 
 -   D o ( . . . )   ( L 4 4 5 ) 
 
 -   D o ( . . . )   ( L 4 7 5 ) 
 
 
 
 # # #   V i e w e r \ V i e w e r A n i m a t i o n . c s 
 
 -   D o ( . . . )   ( L 5 7 ) 
 
 -   D o ( . . . )   ( L 9 4 ) 
 
 -   A d d A n i m a t i o n ( . . . )   ( L 1 0 4 ) 
 
 -   R e m o v e A l l A n i m a t i o n s ( . . . )   ( L 1 1 2 ) 
 
 
 
 # # #   V i e w e r \ V i e w e r C a m e r a . c s 
 
 -   S e t V i e w P o r t ( . . . )   ( L 6 6 ) 
 
 -   L o o k A t ( . . . )   ( L 6 9 ) 
 
 -   Z o o m T o F i t ( . . . )   ( L 7 1 ) 
 
 -   S c r o l l ( . . . )   ( L 7 3 ) 
 
 -   M o u s e D r a g ( . . . )   ( L 7 5 ) 
 
 -   S e t V e r t i c a l F o v ( . . . )   ( L 1 0 3 ) 
 
 -   S e t V i e w P o r t ( . . . )   ( L 1 1 2 ) 
 
 -   L o o k A t ( . . . )   ( L 1 2 5 ) 
 
 -   Z o o m T o F i t ( . . . )   ( L 1 3 1 ) 
 
 -   M o u s e D r a g ( . . . )   ( L 1 4 3 ) 
 
 -   S c r o l l ( . . . )   ( L 1 6 6 ) 
 
 -   v e c E y e ( . . . )   ( L 1 7 6 ) 
 
 
 
 # # #   V i e w e r \ V i e w e r G p u T e x . c s 
 
 -   R e p l a c e W i t h ( . . . )   ( L 6 7 ) 
 
 
 
 # # #   V i e w e r \ V i e w e r I m a g e Q u a d . c s 
 
 -   U p d a t e I m a g e ( . . . )   ( L 6 9 ) 
 
 -   U p d a t e M a t r i x ( . . . )   ( L 7 5 ) 
 
 
 
 # # #   V i e w e r \ V i e w e r K e y b o a r d . c s 
 
 -   A d d K e y H a n d l e r ( . . . )   ( L 4 3 ) 
 
 -   b K e y E q u a l s ( . . . )   ( L 1 4 6 ) 
 
 -   D o ( . . . )   ( L 1 6 1 ) 
 
 -   A d d A c t i o n ( . . . )   ( L 1 7 7 ) 
 

*(Appendix truncated — full catalog has 2100+ lines; re-run `_build_merged_readme.py` after API changes.)*


---

*End of Merged Platform README.*