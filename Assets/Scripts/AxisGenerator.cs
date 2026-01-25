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

        GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shaft.name = $"{name}_Shaft";
        shaft.transform.SetParent(parent, false);
        if (shaft.TryGetComponent(out Collider c)) Object.Destroy(c);

        shaft.transform.localScale = new Vector3(thickness, shaftLen / 2f, thickness);
        shaft.transform.localPosition = offset + dir * (shaftLen / 2f);
        shaft.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir);
        if (shaft.TryGetComponent(out Renderer r)) r.material = mat;
        list.Add(shaft);

        GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cap.name = $"{name}_Head";
        cap.transform.SetParent(parent, false);
        if (cap.TryGetComponent(out Collider cc)) Object.Destroy(cc);

        cap.transform.localScale = new Vector3(capRadius, capHeight / 2f, capRadius);
        cap.transform.localPosition = offset + dir * (shaftLen + capHeight / 2f);
        cap.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir);
        if (cap.TryGetComponent(out Renderer rr)) rr.material = mat;
        list.Add(cap);
    }
}