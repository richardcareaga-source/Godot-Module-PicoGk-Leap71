using Godot;
using PicogkGodotBridge;

namespace PicogkPlanetDemo;

public static class VoxelPreviewUtil
{
	public static MultiMeshInstance3D? CreateLayer(VoxelPreviewData data, Color albedo, string name)
	{
		if (data.VoxelCount <= 0)
			return null;

		var box = new BoxMesh { Size = Vector3.One };
		var mm = new MultiMesh
		{
			Mesh = box,
			TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
			InstanceCount = data.VoxelCount,
			VisibleInstanceCount = data.VoxelCount,
		};

		float s = data.VoxelSizeM;
		var basis = Basis.Identity.Scaled(new Vector3(s, s, s));
		for (int i = 0; i < data.VoxelCount; i++)
		{
			int o = i * 3;
			var origin = new Vector3(data.Centers[o], data.Centers[o + 1], data.Centers[o + 2]);
			mm.SetInstanceTransform(i, new Transform3D(basis, origin));
		}

		albedo.A = 0.95f;
		return new MultiMeshInstance3D
		{
			Name = name,
			Multimesh = mm,
			MaterialOverride = new StandardMaterial3D
			{
				AlbedoColor = albedo,
				Metallic = 0.4f,
				Roughness = 0.7f,
				Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			},
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
		};
	}
}
