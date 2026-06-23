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

<details>
<summary><strong>Expand generated public API catalog</strong></summary>

## PicoGK (72 files)

### Base/FieldMetadata.cs

- nCount(...) (L83)

- bGetNameAt(...) (L96)

- strTypeAt(...) (L134)

- strTypeName(...) (L144)

- bGetValueAt(...) (L169)

- bGetValueAt(...) (L192)

- bGetValueAt(...) (L205)

- SetValue(...) (L216)

- SetValue(...) (L227)

- SetValue(...) (L238)

- RemoveValue(...) (L248)


### Base/GlobalObjects.cs

- voxSphere(...) (L93)

- voxLatticeBeam(...) (L109)

- voxMeshShell(...) (L130)

- voxCombineAll(...) (L144)

- voxFromVdbFile(...) (L170)


### Base/Lattice.cs

- AddSphere(...) (L64)

- AddBeam(...) (L78)

- AddBeam(...) (L101)


### Base/Mesh.cs

- mshCreateTransformed(...) (L75)

- mshCreateTransformed(...) (L105)

- mshCreateMirrored(...) (L129)

- nAddVertex(...) (L153)

- AddVertices(...) (L158)

- vecVertexAt(...) (L176)

- nVertexCount(...) (L190)

- nAddTriangle(...) (L201)

- nAddTriangle(...) (L215)

- nTriangleCount(...) (L224)

- nAddTriangle(...) (L238)

- AddQuad(...) (L253)

- AddQuad(...) (L276)

- oTriangleAt(...) (L294)

- GetTriangle(...) (L312)

- Append(...) (L335)

- oBoundingBox(...) (L352)


### Base/PolyLine.cs

- nAddVertex(...) (L65)

- Add(...) (L76)

- nVertexCount(...) (L86)

- vecVertexAt(...) (L95)

- GetColor(...) (L106)

- oBoundingBox(...) (L116)

- AddArrow(...) (L137)

- AddCross(...) (L195)


### Base/ScalarField.cs

- InformActiveValue(...) (L52)

- SetValue(...) (L138)

- bGetValue(...) (L155)

- RemoveValue(...) (L166)

- GetVoxelDimensions(...) (L180)

- GetVoxelDimensions(...) (L210)

- GetVoxelSlice(...) (L242)

- TraverseActive(...) (L262)

- fSignedDistance(...) (L279)

- oBoundingBox(...) (L289)


### Base/VectorField.cs

- InformActiveValue(...) (L51)

- SetValue(...) (L149)

- bGetValue(...) (L169)

- RemoveValue(...) (L183)

- TraverseActive(...) (L195)


### Base/Voxels.cs

- fSignedDistance(...) (L57)

- voxDuplicate(...) (L118)

- mshAsMesh(...) (L181)

- voxSphere(...) (L193)

- voxLatticeBeam(...) (L211)

- voxMeshShell(...) (L234)

- bIsEmpty(...) (L249)

- fVoxelSize(...) (L267)

- BoolAdd(...) (L274)

- voxBoolAdd(...) (L291)

- BoolAddAll(...) (L303)

- voxBoolAddAll(...) (L315)

- voxCombine(...) (L328)

- voxCombineAll(...) (L340)

- BoolSubtract(...) (L354)

- voxBoolSubtract(...) (L369)

- BoolSubtractAll(...) (L381)

- voxBoolSubtractAll(...) (L392)

- BoolIntersect(...) (L405)

- voxBoolIntersect(...) (L420)

- Trim(...) (L458)

- voxTrim(...) (L469)

- Offset(...) (L482)

- voxOffset(...) (L492)

- DoubleOffset(...) (L505)

- voxDoubleOffset(...) (L517)

- TripleOffset(...) (L539)

- voxTripleOffset(...) (L557)

- Smoothen(...) (L569)

- voxSmoothen(...) (L577)

- OverOffset(...) (L590)

- voxOverOffset(...) (L613)

- Fillet(...) (L631)

- voxFillet(...) (L642)

- voxShell(...) (L659)

- voxShell(...) (L680)

- RenderMesh(...) (L707)

- RenderImplicit(...) (L723)

- IntersectImplicit(...) (L738)

- voxIntersectImplicit(...) (L748)

- ... +18 more members


### Diagnostics/TestCLI.cs

- Run(...) (L45)


### Diagnostics/TestProgress.cs

- Test(...) (L40)


### Diagnostics/TestVectorAndComparison.cs

- Test(...) (L44)


### IO/Cli.cs

- oBBoxFile(...) (L81)

- bBinary(...) (L86)

- fUnitsHeader(...) (L91)

- b32BitAlign(...) (L96)

- strHeaderDate(...) (L106)

- strWarnings(...) (L116)

- WriteSlicesToCliFile(...) (L129)

- SaveToCliFile(...) (L885)


### IO/ImageIo.cs

- SaveTga(...) (L43)

- SaveTga(...) (L55)

- GetFileInfo(...) (L112)

- GetFileInfo(...) (L130)

- LoadTga(...) (L155)

- LoadTga(...) (L167)

- bYAxisFlipped(...) (L245)


### IO/MeshIo.cs

- **enum** EStlUnit (L45)

- mshFromStlFile(...) (L63)

- mshFromStlFile(...) (L84)

- SaveToStlFile(...) (L210)

- SaveToStlFile(...) (L221)

- NormalX(...) (L314)

- NormalY(...) (L315)

- NormalZ(...) (L316)

- V1X(...) (L317)

- V1Y(...) (L318)

- V1Z(...) (L319)

- V2X(...) (L320)

- V2Y(...) (L321)

- V2Z(...) (L322)

- V3X(...) (L323)

- V3Y(...) (L324)

- V3Z(...) (L325)

- m_strLoadHeaderData(...) (L401)


### IO/OpenVdbFile.cs

- libCreateCompatibleLibraryFor(...) (L86)

- SaveToFile(...) (L165)

- voxGet(...) (L199)

- voxGet(...) (L221)

- nAdd(...) (L242)

- oGetScalarField(...) (L267)

- oGetScalarField(...) (L288)

- nAdd(...) (L309)

- oGetVectorField(...) (L328)

- oGetVectorField(...) (L348)

- nAdd(...) (L369)

- nFieldCount(...) (L384)

- strFieldName(...) (L397)

- strFieldType(...) (L430)

- bIsPicoGKCompatible(...) (L478)

- fPicoGKVoxelSizeMM(...) (L494)


### IO/VoxelsIo.cs

- voxFromVdbFile(...) (L60)

- SaveToVdbFile(...) (L91)


### Internals/Interop.cs

- Dispose(...) (L196)

- Dispose(...) (L262)

- Dispose(...) (L484)

- Dispose(...) (L560)

- Dispose(...) (L774)

- Dispose(...) (L833)

- Dispose(...) (L891)

- Dispose(...) (L944)

- Dispose(...) (L1057)

- Dispose(...) (L1160)

- Dispose(...) (L1245)

- Dispose(...) (L1365)


### Internals/Types.cs

- X(...) (L90)

- Y(...) (L91)

- Z(...) (L92)

- A(...) (L107)

- B(...) (L108)

- C(...) (L109)


### Library/Library.cs

- vecVoxelsToMm(...) (L249)

- MmToVoxels(...) (L269)

- Dispose(...) (L301)


### Library/LibraryGlobal.cs

- oLibrary(...) (L46)

- Dispose(...) (L179)

- oLibrary(...) (L237)

- RegisterGlobalLibrary(...) (L248)

- UnregisterGlobalLibrary(...) (L259)

- RegisterGlobalViewer(...) (L278)

- UnregisterGlobalViewer(...) (L289)

- RegisterGlobalLog(...) (L308)

- UnregisterGlobalLog(...) (L319)

- Go(...) (L357)

- Log(...) (L434)

- bContinueTask(...) (L446)

- EndTask(...) (L455)

- CancelEndTaskRequest(...) (L463)

- strFindLightSetupFile(...) (L471)


### Library/LibraryInfo.cs

- strName(...) (L47)

- strVersion(...) (L58)

- strBuildInfo(...) (L70)


### Numerics/Angles.cs

- fNormalizedAngleRad(...) (L54)


### Numerics/Comparison.cs

- bAlmostEqual(...) (L79)

- bAlmostLessOrEqual(...) (L96)

- bAlmostMoreOrEqual(...) (L101)

- bAlmostZero(...) (L110)

- bAlmostEqual(...) (L120)

- bAlmostZero(...) (L132)

- bAlmostEqual(...) (L142)

- bAlmostZero(...) (L153)


### Numerics/Coordinates.cs

- R(...) (L46)

- Phi(...) (L52)

- Theta(...) (L58)

- ToString(...) (L148)

- R(...) (L160)

- Phi(...) (L165)

- Z(...) (L169)

- ToString(...) (L262)

- R(...) (L274)

- Phi(...) (L280)

- ToString(...) (L354)


### Numerics/Vector.cs

- vecNormalized(...) (L53)

- vecSafeNormalized(...) (L68)

- vecAsVector3(...) (L123)

- vecPtWorld(...) (L133)

- vecDirWorld(...) (L143)

- vecPtLocal(...) (L153)

- vecDirLocal(...) (L163)

- vecTransformed(...) (L171)

- vecMirrored(...) (L185)


### Shapes/2D/BaseInterfaces.cs

- PtAtT(...) (L85)


### Shapes/2D/ContourSampler.cs

- fTotalLength(...) (L81)

- fArcTFromLinearT(...) (L86)


### Shapes/2D/Contours.cs

- fR(...) (L57)

- fLength(...) (L59)

- PtAtT(...) (L69)

- fPhi(...) (L131)

- fA(...) (L136)

- fB(...) (L141)

- fLength(...) (L143)

- fLength(...) (L247)

- fLength(...) (L293)


### Shapes/2D/Paths.cs

- fLength(...) (L77)

- fAngle(...) (L138)

- fLength(...) (L140)

- Add(...) (L177)

- AddLine(...) (L192)

- AddLineRel(...) (L201)

- AddArc(...) (L212)

- AddArcRel(...) (L223)

- fLength(...) (L256)


### Shapes/3D/BaseInterfaces.cs

- vecPtAtT(...) (L51)

- PtAtT(...) (L72)


### Shapes/3D/Frame3d.cs

- vecPtToWorld(...) (L149)

- vecPtToWorld(...) (L156)

- vecDirToWorld(...) (L165)

- vecDirToWorld(...) (L178)

- vecPtFromWorld(...) (L190)

- vecDirFromWorld(...) (L203)

- AsRigid(...) (L363)

- Equals(...) (L440)

- Equals(...) (L450)

- GetHashCode(...) (L457)


### Shapes/3D/OrientedPath.cs

- vecPtAtT(...) (L58)

- vecPtAtT(...) (L83)

- PtAtT(...) (L86)


### Types/Animation.cs

- **enum** EType (L48)

- End(...) (L61)

- bAnimate(...) (L66)

- Clear(...) (L124)

- bPulse(...) (L137)

- bIsIdle(...) (L167)

- Add(...) (L173)


### Types/BBox.cs

- bIsEmpty(...) (L96)

- bContains(...) (L112)

- Include(...) (L131)

- Include(...) (L145)

- Grow(...) (L155)

- ToString(...) (L192)

- vecSize(...) (L270)

- bIsEmpty(...) (L278)

- bContains(...) (L296)

- Include(...) (L317)

- Include(...) (L335)

- Include(...) (L347)

- Grow(...) (L360)

- vecCenter(...) (L385)

- oFitInto(...) (L397)

- vecRandomVectorInside(...) (L426)

- oAsBoundingBox2(...) (L437)

- ToString(...) (L446)

- vecMin(...) (L454)

- vecMax(...) (L459)


### Types/Color.cs

- R(...) (L50)

- G(...) (L55)

- B(...) (L60)

- A(...) (L65)

- strAsHexCode(...) (L239)

- strAsABGRHexCode(...) (L265)

- ToString(...) (L275)

- clrWeighted(...) (L287)

- clrRandom(...) (L314)

- H(...) (L577)

- S(...) (L581)

- V(...) (L585)

- H(...) (L718)

- L(...) (L723)

- S(...) (L728)


### Types/Csv.cs

- Save(...) (L104)

- nRowCount(...) (L135)

- nMaxColumnCount(...) (L140)

- strGetAt(...) (L145)

- SetKeyColumn(...) (L158)

- bGetAt(...) (L163)

- bGetAt(...) (L175)

- bFindColumn(...) (L212)

- strColumnId(...) (L231)

- SetColumnIds(...) (L239)

- AddRow(...) (L245)


### Types/Easing.cs

- fEaseSineIn(...) (L44)

- fEaseSineOut(...) (L49)

- fEaseSineInOut(...) (L54)

- fEaseQuadIn(...) (L59)

- fEaseQuadOut(...) (L64)

- fEaseQuadInOut(...) (L69)

- fEaseCubicIn(...) (L76)

- fEaseCubicOut(...) (L81)

- fEaseCubicInOut(...) (L86)

- **enum** EEasing (L93)

- fEasingFunction(...) (L104)


### Types/Image.cs

- clrValue(...) (L51)

- fValue(...) (L52)

- bValue(...) (L53)

- SetValue(...) (L55)

- SetValue(...) (L56)

- SetValue(...) (L57)

- SetValue(...) (L64)

- SetBgr24(...) (L79)

- SetBgra32(...) (L95)

- SetRgb24(...) (L121)

- SetRgba32(...) (L126)

- clrGetAtNormalized(...) (L138)

- DrawLine(...) (L167)

- DrawLine(...) (L198)

- DrawLine(...) (L229)

- fValue(...) (L287)

- clrValue(...) (L292)

- SetValue(...) (L297)

- SetValue(...) (L302)

- clrValue(...) (L318)

- bValue(...) (L324)

- SetValue(...) (L329)

- SetValue(...) (L334)

- bContainsActivePixels(...) (L347)

- fValue(...) (L372)

- bValue(...) (L378)

- SetValue(...) (L383)

- SetValue(...) (L388)

- SetValue(...) (L404)

- fValue(...) (L417)

- clrValue(...) (L525)

- SetValue(...) (L530)

- SetRgba32(...) (L535)

- clrValue(...) (L587)

- SetValue(...) (L592)

- SetRgb24(...) (L597)

- SetValue(...) (L649)

- clrValue(...) (L662)


### Types/Log.cs

- Log(...) (L63)

- Log(...) (L117)

- LogTime(...) (L144)

- Dispose(...) (L155)


### Types/Progress.cs

- Progress(...) (L60)

- Progress(...) (L92)

- Dispose(...) (L106)

- SetItem(...) (L139)

- Progress(...) (L185)


### Types/SkiaBitmap.cs

- SavePng(...) (L108)

- SaveJpg(...) (L121)

- SaveTga(...) (L134)

- imgLoadFromFile(...) (L139)


### Types/Slice.cs

- strWindingAsString(...) (L51)

- AddVertex(...) (L116)

- DetectWinding(...) (L121)

- Close(...) (L134)

- AsSvgPolyline(...) (L144)

- AsSvgPath(...) (L167)

- oBBox(...) (L191)

- nCount(...) (L192)

- AddContour(...) (L205)

- bIsEmpty(...) (L211)

- Close(...) (L216)

- SaveToSvgFile(...) (L224)

- fZPos(...) (L492)

- oBBox(...) (L493)

- nContours(...) (L494)

- m_fMinY(...) (L533)

- m_fMaxY(...) (L534)

- m_bUsed(...) (L535)

- AddSlices(...) (L558)

- AddToViewer(...) (L567)

- nCount(...) (L608)

- oBBox(...) (L610)


### Utils/FieldUtils.cs

- bDoesSliceContainDefect(...) (L154)

- bVisualizeSdfSlicesAsTgaStack(...) (L202)

- nCount(...) (L254)

- InformActiveValue(...) (L272)

- oExtract(...) (L283)

- InformActiveValue(...) (L331)

- Merge(...) (L358)

- InformActiveValue(...) (L377)

- AddToViewer(...) (L388)

- InformActiveValue(...) (L426)


### Utils/MeshMath.cs

- bFindTriangleFromSurfacePoint(...) (L42)


### Utils/SliceViz.cs

- nSliceCount(...) (L84)

- Visualize(...) (L90)

- Visualize(...) (L99)

- Dispose(...) (L134)


### Utils/Utils.cs

- strShorten(...) (L216)

- strFolder(...) (L305)

- Dispose(...) (L312)


### Utils/Vdb2CLI.cs

- Convert(...) (L54)


### Utils/VoxCutViz.cs

- nSliceCount(...) (L89)

- Cut(...) (L96)

- Cut(...) (L109)

- Dispose(...) (L136)


### Viewer/Viewer.cs

- bPoll(...) (L195)

- RequestUpdate(...) (L247)

- LoadLightSetup(...) (L258)

- LoadLightSetup(...) (L267)

- Add(...) (L325)

- Remove(...) (L337)

- SetObjectMatrix(...) (L348)

- Add(...) (L360)

- Remove(...) (L372)

- SetObjectMatrix(...) (L383)

- Add(...) (L395)

- Remove(...) (L407)

- SetObjectMatrix(...) (L418)

- RemoveAllObjects(...) (L430)

- RequestScreenShot(...) (L442)

- EnableExperimental(...) (L453)

- SetGroupVisible(...) (L461)

- SetGroupMaterial(...) (L477)

- SetGroupMatrix(...) (L494)

- EnableOverhangWarning(...) (L510)

- DisableOverhangWarning(...) (L525)

- oBBox(...) (L536)

- SetBackgroundColor(...) (L546)

- ZoomToFit(...) (L555)

- SetFov(...) (L564)

- bIsIdle(...) (L597)


### Viewer/ViewerActions.cs

- Do(...) (L65)

- Do(...) (L89)

- Do(...) (L113)

- Do(...) (L135)

- Do(...) (L155)

- Do(...) (L171)

- Do(...) (L184)

- Do(...) (L203)

- Do(...) (L222)

- Do(...) (L242)

- Do(...) (L263)

- Do(...) (L282)

- Do(...) (L301)

- Do(...) (L322)

- Do(...) (L342)

- Do(...) (L361)

- Do(...) (L375)

- Do(...) (L392)

- Do(...) (L416)

- Do(...) (L445)

- Do(...) (L475)


### Viewer/ViewerAnimation.cs

- Do(...) (L57)

- Do(...) (L94)

- AddAnimation(...) (L104)

- RemoveAllAnimations(...) (L112)


### Viewer/ViewerCamera.cs

- SetViewPort(...) (L66)

- LookAt(...) (L69)

- ZoomToFit(...) (L71)

- Scroll(...) (L73)

- MouseDrag(...) (L75)

- SetVerticalFov(...) (L103)

- SetViewPort(...) (L112)

- LookAt(...) (L125)

- ZoomToFit(...) (L131)

- MouseDrag(...) (L143)

- Scroll(...) (L166)

- vecEye(...) (L176)


### Viewer/ViewerGpuTex.cs

- ReplaceWith(...) (L67)


### Viewer/ViewerImageQuad.cs

- UpdateImage(...) (L69)

- UpdateMatrix(...) (L75)


### Viewer/ViewerKeyboard.cs

- AddKeyHandler(...) (L43)

- bKeyEquals(...) (L146)

- Do(...) (L161)

- AddAction(...) (L177)


*(Appendix truncated — full catalog has 2100+ lines; re-run `_build_merged_readme.py` after API changes.)*

</details>

---

*End of Merged Platform README.*
