using Godot;
using PicogkGodotBridge;

namespace PicogkPlanetDemo;

public static class MeshUtil
{
	public static ArrayMesh ToArrayMesh(PicogkMeshData data)
	{
		var vertices = new Vector3[data.TriangleCount * 3];
		var normals = new Vector3[data.TriangleCount * 3];

		for (int i = 0; i < data.TriangleCount; i++)
		{
			int o = i * 9;
			int v = i * 3;
			vertices[v] = new Vector3(data.Vertices[o], data.Vertices[o + 1], data.Vertices[o + 2]);
			vertices[v + 1] = new Vector3(data.Vertices[o + 3], data.Vertices[o + 4], data.Vertices[o + 5]);
			vertices[v + 2] = new Vector3(data.Vertices[o + 6], data.Vertices[o + 7], data.Vertices[o + 8]);
			var n = new Vector3(data.Normals[o], data.Normals[o + 1], data.Normals[o + 2]);
			if (n.LengthSquared() < 1e-8f)
				n = Vector3.Up;
			normals[v] = n;
			normals[v + 1] = n;
			normals[v + 2] = n;
		}

		var arrays = new Godot.Collections.Array();
		arrays.Resize((int)Mesh.ArrayType.Max);
		arrays[(int)Mesh.ArrayType.Vertex] = vertices;
		arrays[(int)Mesh.ArrayType.Normal] = normals;

		var mesh = new ArrayMesh();
		mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
		return mesh;
	}

	public static ArrayMesh ToArrayMesh(ColoredPlanetMeshData data)
	{
		var geom = data.Geometry;
		var vertices = new Vector3[geom.TriangleCount * 3];
		var normals = new Vector3[geom.TriangleCount * 3];
		var colors = new Color[geom.TriangleCount * 3];

		for (int i = 0; i < geom.TriangleCount; i++)
		{
			int o = i * 9;
			int v = i * 3;
			vertices[v] = new Vector3(geom.Vertices[o], geom.Vertices[o + 1], geom.Vertices[o + 2]);
			vertices[v + 1] = new Vector3(geom.Vertices[o + 3], geom.Vertices[o + 4], geom.Vertices[o + 5]);
			vertices[v + 2] = new Vector3(geom.Vertices[o + 6], geom.Vertices[o + 7], geom.Vertices[o + 8]);

			var n = new Vector3(geom.Normals[o], geom.Normals[o + 1], geom.Normals[o + 2]);
			if (n.LengthSquared() < 1e-8f)
				n = Vector3.Up;
			normals[v] = n;
			normals[v + 1] = n;
			normals[v + 2] = n;

			colors[v] = new Color(data.VertexColorRgb[o], data.VertexColorRgb[o + 1], data.VertexColorRgb[o + 2]);
			colors[v + 1] = new Color(data.VertexColorRgb[o + 3], data.VertexColorRgb[o + 4], data.VertexColorRgb[o + 5]);
			colors[v + 2] = new Color(data.VertexColorRgb[o + 6], data.VertexColorRgb[o + 7], data.VertexColorRgb[o + 8]);
		}

		var arrays = new Godot.Collections.Array();
		arrays.Resize((int)Mesh.ArrayType.Max);
		arrays[(int)Mesh.ArrayType.Vertex] = vertices;
		arrays[(int)Mesh.ArrayType.Normal] = normals;
		arrays[(int)Mesh.ArrayType.Color] = colors;

		var mesh = new ArrayMesh();
		mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
		return mesh;
	}

	public static void LogBounds(string label, PicogkMeshData data)
	{
		if (data.TriangleCount == 0)
		{
			GD.Print($"{label}: empty");
			return;
		}

		var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
		var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
		for (int i = 0; i < data.Vertices.Length; i += 3)
		{
			var p = new Vector3(data.Vertices[i], data.Vertices[i + 1], data.Vertices[i + 2]);
			min = min.Min(p);
			max = max.Max(p);
		}
		var center = (min + max) * 0.5f;
		GD.Print($"{label}: tris={data.TriangleCount:N0} center={center} size={(max - min)}");
	}
}
