#if TOOLS
using Godot;

namespace PicogkPlanetDemo;

[Tool]
public partial class PicogkPlanetPlugin : EditorPlugin
{
	public override void _EnterTree()
	{
		AddToolMenuItem("PicoGK / Generate Planet", Callable.From(OnGeneratePlanet));
	}

	public override void _ExitTree()
	{
		RemoveToolMenuItem("PicoGK / Generate Planet");
	}

	void OnGeneratePlanet()
	{
		var root = EditorInterface.Singleton.GetEditedSceneRoot();
		if (root == null)
		{
			GD.PrintErr("PicoGK: open a scene first.");
			return;
		}

		PlanetWorld world = FindPlanetWorld(root);
		if (world == null)
		{
			GD.PrintErr("PicoGK: add PlanetWorld to the scene root (e.g. scenes/World.tscn).");
			return;
		}

		world.GeneratePlanet();
		GD.Print("PicoGK: generation started — check Output and the 3D viewport.");
	}

	static PlanetWorld FindPlanetWorld(Node node)
	{
		if (node is PlanetWorld w)
			return w;
		foreach (var child in node.GetChildren())
		{
			var found = FindPlanetWorld(child);
			if (found != null)
				return found;
		}
		return null;
	}
}
#endif
