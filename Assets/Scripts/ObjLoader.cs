using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

public static class ObjLoader
{
    public static GameObject Load(string filePath)
    {
        if (!File.Exists(filePath)) return null;

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        // Minimal parser for v and f
        string[] lines = File.ReadAllLines(filePath);
        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;

            string[] parts = line.Split(new char[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;

            switch (parts[0])
            {
                case "v":
                    if (parts.Length >= 4)
                    {
                        float x = float.Parse(parts[1], CultureInfo.InvariantCulture);
                        float y = float.Parse(parts[2], CultureInfo.InvariantCulture);
                        float z = float.Parse(parts[3], CultureInfo.InvariantCulture);
                        vertices.Add(new Vector3(x, y, z)); // Unity flips Z usually, but raw OBJ is often OK
                    }
                    break;
                case "f":
                    // Simple triangulation (assumes convex polygons)
                    // Format: f v1/vt1/vn1 v2/vt2/vn2 ...
                    List<int> faceIndices = new List<int>();
                    for (int i = 1; i < parts.Length; i++)
                    {
                        string[] facePart = parts[i].Split('/');
                        if (int.TryParse(facePart[0], out int vIndex))
                        {
                            // OBJ is 1-based, Unity is 0-based
                            faceIndices.Add(vIndex < 0 ? vertices.Count + vIndex : vIndex - 1);
                        }
                    }

                    if (faceIndices.Count >= 3)
                    {
                        for (int i = 1; i < faceIndices.Count - 1; i++)
                        {
                            triangles.Add(faceIndices[0]);
                            triangles.Add(faceIndices[i]);
                            triangles.Add(faceIndices[i + 1]);
                        }
                    }
                    break;
            }
        }

        Mesh mesh = new Mesh();
        if (vertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GameObject go = new GameObject(Path.GetFileNameWithoutExtension(filePath));
        go.AddComponent<MeshFilter>().mesh = mesh;
        go.AddComponent<MeshRenderer>().material = new Material(Shader.Find("Standard"));

        // Ensure pivot is centered for rotation
        go.transform.position = -mesh.bounds.center;

        GameObject parent = new GameObject("OBJ_Container");
        go.transform.SetParent(parent.transform, true);

        return parent;
    }
}