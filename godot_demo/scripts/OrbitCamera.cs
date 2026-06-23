using Godot;

namespace PicogkPlanetDemo;

public partial class OrbitCamera : Node3D
{
	[Export] public float Distance { get; set; } = 5f;
	[Export] public float Speed { get; set; } = 0.35f;

	float _angle;

	public override void _Process(double delta)
	{
		_angle += Speed * (float)delta;
		var cam = GetNodeOrNull<Camera3D>("Camera3D");
		if (cam == null)
			return;

		float y = Mathf.Sin(_angle) * 0.15f;
		cam.Position = new Vector3(
			Mathf.Cos(_angle) * Distance,
			y,
			Mathf.Sin(_angle) * Distance);
		cam.LookAt(GlobalPosition, Vector3.Up);
	}
}
