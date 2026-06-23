# Third-party libraries (integrated into picogk_voxel)

These three repos at the workspace root are compiled together by `bridge/PicogkGodotBridge.csproj`:

| Folder | Role |
|--------|------|
| `PicoGK/` | Geometry kernel — `Voxels`, `Mesh`, `Lattice`, `IImplicit`, native runtime in `PicoGK/native/` |
| `LEAP71_ShapeKernel/` | BaseShapes, frames, `Sh.*` helpers |
| `LEAP71_LatticeLibrary/` | Lattice / implicit workflows |

The bridge uses **full C# source** from all three (not “DLL-only”). Native `picogk.26.1.dll` is copied from `PicoGK/native/win-x64` next to the bridge output — it is the PicoGK engine binary, not a substitute for ShapeKernel or LatticeLibrary.
