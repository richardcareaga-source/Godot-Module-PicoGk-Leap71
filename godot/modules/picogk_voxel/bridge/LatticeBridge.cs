using Leap71.LatticeLibrary;
using Leap71.ShapeKernel;
using PicoGK;

namespace PicogkGodotBridge;

/// <summary>
/// LEAP71 LatticeLibrary — regular lattice inside a bounding volume (from example tasks).
/// </summary>
public static class LatticeBridge
{
    public static PicogkMeshData RegularLatticeInSphere(
        PicogkHeadlessRuntime rt,
        float sphereRadiusMm,
        int cellsX = 12,
        int cellsY = 12,
        int cellsZ = 12,
        float noiseLevel = 0f,
        bool reverseWinding = false)
    {
        var oSphere = new BaseSphere(new LocalFrame(), sphereRadiusMm);
        Voxels voxBounding = oSphere.voxConstruct();

        ICellArray xCellArray = new RegularCellArray(voxBounding, cellsX, cellsY, cellsZ, noiseLevel);
        ILatticeType xLatticeType = new BodyCentreLattice();
        IBeamThickness xBeamThickness = new ConstantBeamThickness(1.5f);

        Voxels voxLattice = BuildLatticeVoxels(xCellArray, xLatticeType, xBeamThickness);

        using var msh = rt.MeshFromVoxels(voxLattice);
        return new PicogkMeshData(msh, reverseWinding);
    }

    static Voxels BuildLatticeVoxels(ICellArray xCellArray, ILatticeType xLatticeType, IBeamThickness xBeamThickness, uint nSubSample = 2)
    {
        Lattice oLattice = new Lattice();
        foreach (IUnitCell xCell in xCellArray.aGetUnitCells())
        {
            xBeamThickness.UpdateCell(xCell);
            xLatticeType.AddCell(ref oLattice, xCell, xBeamThickness, nSubSample);
        }
        return new Voxels(oLattice);
    }
}
