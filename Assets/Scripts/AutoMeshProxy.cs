using UnityEngine;
using System.Collections.Generic;
using System.IO;

public static class AutoMeshProxy
{
    // Resolution for Poly simplification (Higher = more detail, 15-20 is good)
    private const int POLY_GRID_RESOLUTION = 100;

    // =========================================================
    // 1. POLYGONAL: VERTEX CLUSTERING (Low Poly Shape)
    // =========================================================
    public static MeshNetworkData GenerateFromMesh(GameObject root)
    {
        // A. Combine all meshes
        MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>();
        if (filters.Length == 0) return null;

        CombineInstance[] combine = new CombineInstance[filters.Length];
        for (int i = 0; i < filters.Length; i++)
        {
            combine[i].mesh = filters[i].sharedMesh;
            combine[i].transform = filters[i].transform.localToWorldMatrix;
        }

        Mesh tempMesh = new Mesh();
        tempMesh.CombineMeshes(combine);
        Vector3[] originalVerts = tempMesh.vertices;
        int[] originalTris = tempMesh.triangles;

        if (originalVerts.Length == 0) return null;

        // B. Calculate Grid Size
        Bounds bounds = tempMesh.bounds;
        float maxDim = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        if (maxDim == 0) maxDim = 1;

        // Size of one "voxel"
        float cellSize = maxDim / (float)POLY_GRID_RESOLUTION;

        // C. Cluster Vertices (Snap to grid)
        Dictionary<string, int> gridToNewIndex = new Dictionary<string, int>();
        List<Vector3> newVerts = new List<Vector3>();
        int[] oldToNewMap = new int[originalVerts.Length];

        for (int i = 0; i < originalVerts.Length; i++)
        {
            Vector3 v = originalVerts[i];

            // Snap to grid coordinates
            int gx = Mathf.RoundToInt(v.x / cellSize);
            int gy = Mathf.RoundToInt(v.y / cellSize);
            int gz = Mathf.RoundToInt(v.z / cellSize);

            string key = $"{gx},{gy},{gz}";

            if (gridToNewIndex.TryGetValue(key, out int existingIndex))
            {
                oldToNewMap[i] = existingIndex;
            }
            else
            {
                // Create new vertex at the snapped position (Absolute World Coords)
                Vector3 snappedPos = new Vector3(gx * cellSize, gy * cellSize, gz * cellSize);
                newVerts.Add(snappedPos);
                int newIndex = newVerts.Count - 1;
                gridToNewIndex[key] = newIndex;
                oldToNewMap[i] = newIndex;
            }
        }

        // D. Rebuild Triangles
        List<int> newTris = new List<int>();
        for (int i = 0; i < originalTris.Length; i += 3)
        {
            int a = oldToNewMap[originalTris[i]];
            int b = oldToNewMap[originalTris[i + 1]];
            int c = oldToNewMap[originalTris[i + 2]];

            // Filter out degenerate triangles (lines/points)
            if (a != b && b != c && a != c)
            {
                newTris.Add(a);
                newTris.Add(b);
                newTris.Add(c);
            }
        }

        Object.DestroyImmediate(tempMesh);

        return new MeshNetworkData
        {
            v = newVerts.ToArray(),
            t = newTris.ToArray()
        };
    }

    // =========================================================
    // 2. VOLUMETRIC: SIMPLE BOX (Scaled to Dimensions)
    // =========================================================
    public static MeshNetworkData GenerateFromVolume(VolumetricModelData data)
    {
        // We ignore the file content completely.
        // We just create a box that matches the volume dimensions.

        GameObject tempCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Mesh mesh = tempCube.GetComponent<MeshFilter>().sharedMesh;
        Vector3[] verts = mesh.vertices;
        int[] tris = mesh.triangles;

        // Determine size
        Vector3 size = new Vector3(data.dimX, data.dimY, data.dimZ);

        // Apply Scale to vertices immediately.
        // The Primitive Cube is 1x1x1 centered at 0.
        // So vertices range from -0.5 to 0.5.
        // We multiply by size.
        for (int i = 0; i < verts.Length; i++)
        {
            verts[i] = Vector3.Scale(verts[i], size);
        }

        Object.DestroyImmediate(tempCube);

        return new MeshNetworkData
        {
            v = verts,
            t = tris
        };
    }
}