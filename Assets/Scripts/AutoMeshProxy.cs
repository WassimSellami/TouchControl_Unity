using System.Collections.Generic;
using UnityEngine;

public static class AutoMeshProxy
{
    public static MeshNetworkData GenerateFromMesh(GameObject root)
    {
        MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>();
        if (filters.Length == 0)
            return null;

        List<Vector3> allLocalVerts = new List<Vector3>();
        List<int> allTris = new List<int>();

        int vertexOffset = 0;
        foreach (MeshFilter mf in filters)
        {
            Mesh m = mf.sharedMesh;
            if (m == null)
                continue;

            Vector3[] v = m.vertices;
            for (int i = 0; i < v.Length; i++)
            {
                allLocalVerts.Add(root.transform.InverseTransformPoint(mf.transform.TransformPoint(v[i])));
            }
            int[] t = m.triangles;
            for (int i = 0; i < t.Length; i++)
            {
                allTris.Add(t[i] + vertexOffset);
            }
            vertexOffset += v.Length;
        }

        Bounds tempBounds = new Bounds(Vector3.zero, Vector3.zero);
        if (allLocalVerts.Count > 0)
        {
            tempBounds.center = allLocalVerts[0];
            foreach (Vector3 v in allLocalVerts)
                tempBounds.Encapsulate(v);
        }

        for (int i = 0; i < allLocalVerts.Count; i++)
        {
            allLocalVerts[i] -= tempBounds.center;
        }

        float maxDim = Mathf.Max(tempBounds.size.x, tempBounds.size.y, tempBounds.size.z);
        float cellSize = (maxDim == 0) ? 1f : maxDim / Constants.POLY_GRID_RESOLUTION;

        Dictionary<string, int> gridToNewIndex = new Dictionary<string, int>();
        List<Vector3> clusteredVerts = new List<Vector3>();
        int[] oldToNewMap = new int[allLocalVerts.Count];

        for (int i = 0; i < allLocalVerts.Count; i++)
        {
            Vector3 v = allLocalVerts[i];
            int gx = Mathf.RoundToInt(v.x / cellSize);
            int gy = Mathf.RoundToInt(v.y / cellSize);
            int gz = Mathf.RoundToInt(v.z / cellSize);
            string key = gx + "_" + gy + "_" + gz;

            if (gridToNewIndex.TryGetValue(key, out int idx))
            {
                oldToNewMap[i] = idx;
            }
            else
            {
                Vector3 snapped = new Vector3(gx * cellSize, gy * cellSize, gz * cellSize);
                clusteredVerts.Add(snapped);
                int newIdx = clusteredVerts.Count - 1;
                gridToNewIndex[key] = newIdx;
                oldToNewMap[i] = newIdx;
            }
        }

        List<int> clusteredTris = new List<int>();
        for (int i = 0; i < allTris.Count; i += 3)
        {
            int a = oldToNewMap[allTris[i]], b = oldToNewMap[allTris[i + 1]], c = oldToNewMap[allTris[i + 2]];
            if (a != b && b != c && a != c)
            {
                clusteredTris.Add(a);
                clusteredTris.Add(b);
                clusteredTris.Add(c);
            }
        }

        return new MeshNetworkData { v = clusteredVerts.ToArray(), t = clusteredTris.ToArray(), isVolumetric = false };
    }

    public static MeshNetworkData GenerateFromVolume(VolumetricModelData data)
    {
        GameObject tempCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Mesh m = tempCube.GetComponent<MeshFilter>().sharedMesh;
        MeshNetworkData packet = new MeshNetworkData { v = m.vertices, t = m.triangles, isVolumetric = true };
        Object.DestroyImmediate(tempCube);
        return packet;
    }
}