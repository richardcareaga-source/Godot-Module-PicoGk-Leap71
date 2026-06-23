using System;
using System.Threading.Tasks;
using Godot;
using PicogkGodotBridge;

namespace PicogkPlanetDemo;

[Tool]
public partial class PlanetWorld : Node3D
{
	/// <summary>SolidColoredPlanet = one voxFull mesh, biome colors blend at borders.</summary>
	public enum PlanetDisplayMode
	{
		SolidColoredPlanet,
		BiomeMeshes,
		FullMeshOnly,
		BiomeMeshesAndVoxels,
	}

	[ExportGroup("PicoGK")]
	[Export] public float RadiusMm { get; set; } = 80f;
	[Export] public float VoxelSizeMm { get; set; } = 1.2f;
	[Export] public int NoiseSeed { get; set; } = 42;
	[Export] public float DisplayScale { get; set; } = 50f;
	[Export(PropertyHint.Enum, "Solid Colored Planet (1 mesh + biome bleed),Six Biome Shells,Full Mesh Green Only,Biomes + Voxel Cubes")]
	public PlanetDisplayMode DisplayMode { get; set; } = PlanetDisplayMode.SolidColoredPlanet;
	[Export(PropertyHint.Range, "0.25,2,0.05")]
	public float BiomeColorBleed { get; set; } = 1f;
	[Export] public bool GenerateOnReady { get; set; } = true;
	[Export] public bool GenerateInEditor { get; set; } = false;
	/// <summary>Match PlanetTest PreviewVoxels (0.95).</summary>
	[Export] public float BiomeLayerAlpha { get; set; } = 0.95f;

	[ExportGroup("Actions")]
	[Export(PropertyHint.None, "Check ON then click away — builds planet with settings above")]
	public bool RegenerateNow
	{
		get => false;
		set
		{
			if (!value || _busy)
				return;
			CallDeferred(MethodName.GeneratePlanet);
		}
	}

	Node3D? _planetRoot;
	Label? _status;
	bool _busy;
	PlanetBridge.PlanetTaskResult? _pendingResult;
	ColoredPlanetMeshData? _pendingColored;
	string? _pendingError;

	public override void _Ready()
	{
		_planetRoot = GetNodeOrNull<Node3D>("PlanetRoot");
		_status = GetNodeOrNull<Label>("UI/StatusLabel");

		bool inEditor = Engine.IsEditorHint();
		bool shouldRun = GenerateOnReady && (!inEditor || GenerateInEditor);

		SetStatus(inEditor
			? $"PicoGK — seed {NoiseSeed}, mode {DisplayMode}. Toggle Regenerate Now."
			: "Generating…");

		if (shouldRun)
			CallDeferred(MethodName.GeneratePlanet);
	}

	public override void _Process(double delta)
	{
		if (!Engine.IsEditorHint())
			return;
		// Keep empty PlanetRoot scale in sync when DisplayScale changes in Inspector.
		if (_planetRoot != null && _planetRoot.Scale != Vector3.One * DisplayScale)
			_planetRoot.Scale = Vector3.One * DisplayScale;
	}

	public void GeneratePlanet()
	{
		if (_busy)
			return;
		_ = GeneratePlanetAsync();
	}

	async Task GeneratePlanetAsync()
	{
		if (_planetRoot == null)
		{
			SetStatus("ERROR: missing PlanetRoot node");
			return;
		}

		_busy = true;
		_pendingResult = null;
		_pendingColored = null;
		_pendingError = null;
		SetStatus($"Generating (seed {NoiseSeed}, {DisplayMode})…");

		try
		{
			if (DisplayMode == PlanetDisplayMode.SolidColoredPlanet)
			{
				_pendingColored = await Task.Run(() =>
					PlanetBridge.RunSolidColoredPlanet(
						RadiusMm, VoxelSizeMm, NoiseSeed, BiomeColorBleed));
			}
			else
			{
				bool needBiomes = DisplayMode != PlanetDisplayMode.FullMeshOnly;
				bool needVoxels = DisplayMode == PlanetDisplayMode.BiomeMeshesAndVoxels;

				_pendingResult = await Task.Run(() =>
					PlanetBridge.RunPlanetTaskLikePlanetTest(
						RadiusMm,
						VoxelSizeMm,
						null,
						includeBiomeMeshes: needBiomes,
						includeBiomeVoxels: needVoxels,
						noiseSeed: NoiseSeed));
			}
		}
		catch (Exception ex)
		{
			_pendingError = ex.ToString();
			GD.PrintErr(ex);
		}

		CallDeferred(MethodName.CompleteGenerate);
	}

	void CompleteGenerate()
	{
		try
		{
			if (_pendingError != null)
			{
				SetStatus($"ERROR: {_pendingError}");
				return;
			}

			if (DisplayMode == PlanetDisplayMode.SolidColoredPlanet)
			{
				if (_pendingColored == null)
				{
					SetStatus("ERROR: no colored mesh");
					return;
				}
				ApplyColoredResult(_pendingColored);
			}
			else
			{
				if (_pendingResult == null)
				{
					SetStatus("ERROR: no result");
					return;
				}
				ApplyResult(_pendingResult);
			}
			float diamM = RadiusMm * 0.002f * DisplayScale;
			SetStatus($"OK — {DisplayMode}, seed {NoiseSeed}, ~{diamM:F1} m — F on PlanetRoot");
		}
		catch (Exception ex)
		{
			GD.PrintErr(ex);
			SetStatus($"ERROR: {ex.Message}");
		}
		finally
		{
			_pendingResult = null;
			_pendingColored = null;
			_pendingError = null;
			_busy = false;
		}
	}

	void ApplyColoredResult(ColoredPlanetMeshData colored)
	{
		foreach (var child in _planetRoot!.GetChildren())
			child.QueueFree();

		_planetRoot.Scale = Vector3.One * DisplayScale;

		var mi = new MeshInstance3D
		{
			Name = "PlanetSurface",
			Mesh = MeshUtil.ToArrayMesh(colored),
			CastShadow = GeometryInstance3D.ShadowCastingSetting.On,
		};
		mi.MaterialOverride = new StandardMaterial3D
		{
			AlbedoColor = Colors.White,
			VertexColorUseAsAlbedo = true,
			Roughness = 0.72f,
			Metallic = 0.35f,
			CullMode = BaseMaterial3D.CullModeEnum.Back,
		};
		AttachGeneratedNode(mi);
		GD.Print($"Planet surface (colored): {colored.Geometry.TriangleCount:N0} triangles");

		CallDeferred(MethodName.FitViewToPlanet);
	}

	void ApplyResult(PlanetBridge.PlanetTaskResult result)
	{
		foreach (var child in _planetRoot!.GetChildren())
			child.QueueFree();

		_planetRoot.Scale = Vector3.One * DisplayScale;

		bool showBiomes = DisplayMode != PlanetDisplayMode.FullMeshOnly;
		bool showFull = DisplayMode == PlanetDisplayMode.FullMeshOnly;

		// PlanetTest PreviewVoxels — six biome layers (Cp colors), no green shell on top.
		if (showBiomes)
		{
			foreach (var (_, logName) in PlanetBridge.PlanetTestBiomeOrder)
			{
				if (!result.BiomeMeshes.TryGetValue(logName, out var biomeMesh) || biomeMesh.TriangleCount == 0)
					continue;

				var clr = PlanetBridge.PlanetTestBiomeColors[logName];
				CreateBiomeMeshNode(logName, biomeMesh, clr);
				GD.Print($"  Biome_{logName}: {biomeMesh.TriangleCount:N0} tris");
			}
		}

		if (showFull)
		{
			CreateMeshNode("PlanetSurface", result.DisplayMesh, new Color(0.35f, 0.55f, 0.4f), biomeStyle: false);
			GD.Print($"Planet surface: {result.DisplayMesh.TriangleCount:N0} triangles");
		}

		if (DisplayMode == PlanetDisplayMode.BiomeMeshesAndVoxels)
		{
			foreach (var (_, logName) in PlanetBridge.PlanetTestBiomeOrder)
			{
				if (!result.BiomeVoxels.TryGetValue(logName, out var vox) || vox.VoxelCount == 0)
					continue;
				var clr = PlanetBridge.PlanetTestBiomeColors[logName];
				var layer = VoxelPreviewUtil.CreateLayer(vox, new Color(clr.R, clr.G, clr.B), $"Voxels_{logName}");
				if (layer != null)
					AttachGeneratedNode(layer);
			}
		}

		CallDeferred(MethodName.FitViewToPlanet);
	}

	void CreateBiomeMeshNode(string logName, PicogkMeshData data, PicoGK.ColorFloat clr)
	{
		var color = new Color(clr.R, clr.G, clr.B, BiomeLayerAlpha);
		CreateMeshNode($"Biome_{logName}", data, color, biomeStyle: true);
	}

	MeshInstance3D CreateMeshNode(string name, PicogkMeshData data, Color color, bool biomeStyle)
	{
		var mi = new MeshInstance3D
		{
			Name = name,
			Mesh = MeshUtil.ToArrayMesh(data),
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
		};

		var mat = new StandardMaterial3D
		{
			AlbedoColor = color,
			Roughness = biomeStyle ? 0.7f : 0.75f,
			Metallic = biomeStyle ? 0.4f : 0f,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled,
		};

		if (biomeStyle)
		{
			mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
			mat.BlendMode = BaseMaterial3D.BlendModeEnum.Mix;
		}

		mi.MaterialOverride = mat;
		AttachGeneratedNode(mi);
		return mi;
	}

	void AttachGeneratedNode(Node node)
	{
		_planetRoot!.AddChild(node);
		// Never set Owner in the editor — that embeds megabyte meshes into World.tscn and breaks the Inspector.
		if (!Engine.IsEditorHint())
		{
			var scene = GetTree().CurrentScene;
			if (scene != null)
				node.Owner = scene;
		}
	}

	void FitViewToPlanet()
	{
		float radiusM = RadiusMm * 0.001f * DisplayScale;
		var cam = GetNodeOrNull<Camera3D>("OrbitPivot/Camera3D")
			?? GetNodeOrNull<Camera3D>("Camera3D");
		if (cam == null)
			return;

		float dist = Math.Max(radiusM * 3f, 1f);
		cam.Position = new Vector3(dist * 0.7f, dist * 0.45f, dist * 0.7f);
		cam.LookAt(Vector3.Zero, Vector3.Up);
		cam.Near = 0.01f;
		cam.Far = 500f;
	}

	void SetStatus(string text)
	{
		if (_status != null)
			_status.Text = text;
		GD.Print(text);
	}
}
