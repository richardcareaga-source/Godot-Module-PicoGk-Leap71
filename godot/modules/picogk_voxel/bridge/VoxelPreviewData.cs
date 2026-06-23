using System.Numerics;
using PicoGK;

namespace PicogkGodotBridge;

public sealed class VoxelPreviewData
{
    const float MmToM = 0.001f;

    public float[] Centers { get; }
    public int VoxelCount { get; }
    public float VoxelSizeM { get; }

    VoxelPreviewData(float[] centers, float voxelSizeM)
    {
        Centers = centers;
        VoxelCount = centers.Length / 3;
        VoxelSizeM = voxelSizeM;
    }

    public static VoxelPreviewData FromVoxels(Voxels vox)
    {
        var lib = vox.lib;
        float halfMm = vox.fVoxelSize * 0.5f;
        var half = new Vector3(halfMm, halfMm, halfMm);

        vox.GetVoxelDimensions(
            out int nXOrigin, out int nYOrigin, out int nZOrigin,
            out _, out _, out _);

        var img = vox.imgAllocateSlice(out int nSlices, Voxels.ESliceAxis.Z);
        var list = new List<Vector3>(4096);

        for (int z = 0; z < nSlices; z++)
        {
            vox.GetVoxelSlice(z, ref img, Voxels.ESliceMode.BlackWhite, Voxels.ESliceAxis.Z);
            int gz = nZOrigin + z;
            for (int y = 0; y < img.nHeight; y++)
            {
                for (int x = 0; x < img.nWidth; x++)
                {
                    if (!img.bValue(x, y))
                        continue;

                    var mm = lib.vecVoxelsToMm(nXOrigin + x, nYOrigin + y, gz) + half;
                    list.Add(mm * MmToM);
                }
            }
        }

        var centers = new float[list.Count * 3];
        for (int i = 0; i < list.Count; i++)
        {
            int o = i * 3;
            centers[o] = list[i].X;
            centers[o + 1] = list[i].Y;
            centers[o + 2] = list[i].Z;
        }

        return new VoxelPreviewData(centers, vox.fVoxelSize * MmToM);
    }
}
