using UnityEngine;
using System.Collections.Generic;

public static class AxisGenerator
{
    public static List<GameObject> CreateAxes(Transform parent, float length, float thickness, Vector3 offset, Material matX, Material matY, Material matZ)
    {
        List<GameObject> visuals = new List<GameObject>();

        CreateSingleAxis(parent, Vector3.right, length, thickness, offset, matX, "X", visuals);
        CreateSingleAxis(parent, Vector3.up, length, thickness, offset, matY, "Y", visuals);
        CreateSingleAxis(parent, Vector3.forward, length, thickness, offset, matZ, "Z", visuals);

        return visuals;
    }

    private static void CreateSingleAxis(Transform parent, Vector3 dir, float length, float thickness, Vector3 offset, Material mat, string name, List<GameObject> list)
    {
        float capHeight = thickness * Constants.ARROWHEAD_HEIGHT_FACTOR;
        float shaftLen = Mathf.Max(thickness * 2f, length - capHeight);
        float capRadius = thickness * Constants.ARROWHEAD_RADIUS_FACTOR;

        // 1. Create Shaft (Cylinder)
        GameObject shaft = new GameObject($"{name}_Shaft");
        shaft.transform.SetParent(parent, false);

        MeshFilter shaftMf = shaft.AddComponent<MeshFilter>();
        MeshRenderer shaftMr = shaft.AddComponent<MeshRenderer>();
        shaftMf.mesh = CreateCylinderMesh(12); // Low poly cylinder
        shaftMr.material = mat;

        shaft.transform.localScale = new Vector3(thickness, shaftLen / 2f, thickness);
        shaft.transform.localPosition = offset + dir * (shaftLen / 2f);
        shaft.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir);

        list.Add(shaft);

        // 2. Create Head (Cone)
        GameObject cap = new GameObject($"{name}_Head");
        cap.transform.SetParent(parent, false);

        MeshFilter capMf = cap.AddComponent<MeshFilter>();
        MeshRenderer capMr = cap.AddComponent<MeshRenderer>();
        capMf.mesh = CreateConeMesh(12); // Low poly cone
        capMr.material = mat;

        cap.transform.localScale = new Vector3(capRadius, capHeight, capRadius);
        // Offset logic slightly adjusted for anchor point of custom cone mesh (base)
        cap.transform.localPosition = offset + dir * shaftLen;
        cap.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir);

        list.Add(cap);
    }

    // --- Procedural Mesh Generation (No Physics Dependency) ---

    private static Mesh CreateCylinderMesh(int segments)
    {
        Mesh mesh = new Mesh();
        Vector3[] vertices = new Vector3[(segments + 1) * 2 + segments * 2 + 2]; // Cap + Side
        List<int> triangles = new List<int>();

        float angleStep = 360.0f / segments;

        // Top and Bottom vertices (Y is up/down, range -1 to 1 for scaling)
        for (int i = 0; i <= segments; i++)
        {
            float angle = Mathf.Deg2Rad * i * angleStep;
            float x = Mathf.Cos(angle) * 0.5f;
            float z = Mathf.Sin(angle) * 0.5f;

            vertices[i] = new Vector3(x, 1f, z); // Top Ring
            vertices[segments + 1 + i] = new Vector3(x, -1f, z); // Bottom Ring
        }

        // Side Triangles
        for (int i = 0; i < segments; i++)
        {
            int current = i;
            int next = i + 1;
            int bottomCurrent = segments + 1 + i;
            int bottomNext = segments + 1 + i + 1;

            triangles.Add(current); triangles.Add(next); triangles.Add(bottomCurrent);
            triangles.Add(bottomCurrent); triangles.Add(next); triangles.Add(bottomNext);
        }

        // NOTE: Caps omitted for simplicity as axes are usually solid color, 
        // but can be added if needed. This reduces vertex count.

        mesh.vertices = vertices;
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();
        return mesh;
    }

    private static Mesh CreateConeMesh(int segments)
    {
        Mesh mesh = new Mesh();
        Vector3[] vertices = new Vector3[segments + 2];
        List<int> triangles = new List<int>();

        float angleStep = 360.0f / segments;

        // Tip
        vertices[0] = new Vector3(0, 1f, 0);

        // Base Ring
        for (int i = 0; i <= segments; i++)
        {
            float angle = Mathf.Deg2Rad * i * angleStep;
            float x = Mathf.Cos(angle) * 0.5f;
            float z = Mathf.Sin(angle) * 0.5f;
            vertices[i + 1] = new Vector3(x, 0f, z);
        }

        // Side Triangles
        for (int i = 1; i <= segments; i++)
        {
            triangles.Add(0); // Tip
            triangles.Add(i + 1 > segments ? 1 : i + 1); // Next Base
            triangles.Add(i); // Current Base
        }

        // Base Cap
        // (Omitted for axis visual simplicity)

        mesh.vertices = vertices;
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();
        return mesh;
    }
}