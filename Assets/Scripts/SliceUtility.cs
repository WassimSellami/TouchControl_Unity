using System.Collections;
using EzySlice;
using UnityEngine;

public static class SliceUtility
{
    public static void SetupMeshHull(GameObject hull, Transform parent, Material crossSectionMat)
    {
        hull.transform.SetParent(parent, false);
        MeshCollider collider = hull.AddComponent<MeshCollider>();
        collider.convex = true;

        MeshRenderer renderer = hull.GetComponent<MeshRenderer>();
        if (renderer != null && crossSectionMat != null)
        {
            renderer.material = crossSectionMat;
        }
    }

    public static void ApplyVolumeCut(GameObject root, Vector3 texturePoint, Vector3 worldNormal, bool invertNormal)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        foreach (Renderer rend in renderers)
        {
            if (rend.gameObject.name.Contains("Shaft") || rend.gameObject.name.Contains("Head"))
                continue;

            Vector3 localNormal = rend.transform.InverseTransformDirection(worldNormal);
            if (invertNormal)
                localNormal = -localNormal;

            rend.material.SetVector("_PlanePos", texturePoint);
            rend.material.SetVector("_PlaneNormal", localNormal);
        }
    }

    public static Bounds GetFullBounds(GameObject obj)
    {
        Renderer[] rends = obj.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0)
            return new Bounds(obj.transform.position, Vector3.one);

        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++)
        {
            if (rends[i].gameObject.name.Contains("Shaft") || rends[i].gameObject.name.Contains("Head"))
                continue;
            b.Encapsulate(rends[i].bounds);
        }
        return b;
    }

    public static void CalculateSeparationPoints(GameObject hull, Vector3 planeNormal, float factor, bool isUpper, out Vector3 startPos, out Vector3 endPos)
    {
        startPos = hull.transform.position;
        Bounds bounds = GetFullBounds(hull);
        float distance = bounds.size.magnitude * factor * 0.5f;
        Vector3 direction = isUpper ? planeNormal : -planeNormal;
        endPos = startPos + (direction * distance);
    }
    public static void SetupHull(GameObject hull, Transform parent)
    {
        hull.transform.SetParent(parent, false);
        MeshCollider collider = hull.AddComponent<MeshCollider>();
        collider.convex = true;
    }

    public static IEnumerator PerformSeparation(GameObject upperHull, GameObject lowerHull, Vector3 planeNormal, float separationFactor, float duration)
    {
        if (upperHull == null || lowerHull == null)
            yield break;

        Bounds b = GetFullBounds(upperHull);
        b.Encapsulate(GetFullBounds(lowerHull));

        float separationDistance = b.size.magnitude * separationFactor;
        Vector3 separationVector = planeNormal * (separationDistance * 0.5f);

        Vector3 upperStartPos = upperHull.transform.position;
        Vector3 lowerStartPos = lowerHull.transform.position;
        Vector3 upperEndPos = upperStartPos + separationVector;
        Vector3 lowerEndPos = lowerStartPos - separationVector;

        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            if (upperHull == null || lowerHull == null)
                yield break;

            float t = Mathf.SmoothStep(0.0f, 1.0f, elapsedTime / duration);
            upperHull.transform.position = Vector3.Lerp(upperStartPos, upperEndPos, t);
            lowerHull.transform.position = Vector3.Lerp(lowerStartPos, lowerEndPos, t);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        if (upperHull != null)
            upperHull.transform.position = upperEndPos;
        if (lowerHull != null)
            lowerHull.transform.position = lowerEndPos;
    }

    public struct SliceResult
    {
        public GameObject upperHull;
        public GameObject lowerHull;
        public bool isValid;
    }

    public static SliceResult ExecuteMeshSlice(
    GameObject originalPart,
    Vector3 planePoint,
    Vector3 planeNormal,
    Material crossSectionMaterial,
    MonoBehaviour coroutineRunner,
    Transform parent)
    {
        SlicedHull hull = originalPart.Slice(planePoint, planeNormal, crossSectionMaterial);

        if (hull == null)
            return new SliceResult { isValid = false };

        GameObject upper = hull.CreateUpperHull(originalPart, crossSectionMaterial);
        GameObject lower = hull.CreateLowerHull(originalPart, crossSectionMaterial);

        if (upper != null && lower != null)
        {
            upper.name = originalPart.name + "_A";
            lower.name = originalPart.name + "_B";

            SetupHull(upper, parent);
            SetupHull(lower, parent);

            coroutineRunner.StartCoroutine(PerformSeparation(
                upper, lower, planeNormal, Constants.SEPARATION_FACTOR, Constants.SEPARATION_ANIMATION_DURATION));

            return new SliceResult { upperHull = upper, lowerHull = lower, isValid = true };
        }

        return new SliceResult { isValid = false };
    }
}