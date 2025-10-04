using UnityEngine;
using EzySlice;
using System.Collections;
using System.Collections.Generic;

public class SliceCommand : ICommand
{
    private List<GameObject> originals;
    private List<GameObject> newHulls = new List<GameObject>();
    private Vector3 planePoint;
    private Vector3 planeNormal;
    private CuttingPlaneManager sliceManager;
    private bool hasBeenExecuted = false;

    public SliceCommand(List<GameObject> objectsToSlice, Vector3 point, Vector3 normal, CuttingPlaneManager manager)
    {
        originals = objectsToSlice;
        planePoint = point;
        planeNormal = normal;
        sliceManager = manager;
    }

    public void Execute()
    {
        if (hasBeenExecuted)
        {
            // This is a Redo: reactivate existing hulls
            foreach (var hull in newHulls)
            {
                if (hull != null)
                {
                    hull.SetActive(true);
                    if (!sliceManager.activeModelParts.Contains(hull))
                    {
                        sliceManager.activeModelParts.Add(hull);
                    }
                }
            }
        }
        else
        {
            // This is the first execution: create new hulls
            foreach (var originalPart in originals)
            {
                if (originalPart == null) continue;

                SlicedHull sliceResult = originalPart.Slice(planePoint, planeNormal, sliceManager.crossSectionMaterial);
                if (sliceResult != null)
                {
                    GameObject upperHull = sliceResult.CreateUpperHull(originalPart, sliceManager.crossSectionMaterial);
                    GameObject lowerHull = sliceResult.CreateLowerHull(originalPart, sliceManager.crossSectionMaterial);

                    if (upperHull != null && lowerHull != null)
                    {
                        SetupHull(upperHull, originalPart);
                        SetupHull(lowerHull, originalPart);

                        sliceManager.StartCoroutine(AnimateSeparation(upperHull, lowerHull, originalPart));

                        newHulls.Add(upperHull);
                        newHulls.Add(lowerHull);
                        sliceManager.activeModelParts.Add(upperHull);
                        sliceManager.activeModelParts.Add(lowerHull);
                    }
                }
            }
            hasBeenExecuted = true;
        }

        foreach (var original in originals)
        {
            if (original != null)
            {
                original.SetActive(false);
                sliceManager.activeModelParts.Remove(original);
            }
        }
    }

    public void Undo()
    {
        // Deactivate new hulls instead of destroying them
        foreach (var hull in newHulls)
        {
            if (hull != null)
            {
                hull.SetActive(false);
                sliceManager.activeModelParts.Remove(hull);
            }
        }

        // Reactivate the original pieces
        foreach (var original in originals)
        {
            if (original != null)
            {
                original.SetActive(true);
                if (!sliceManager.activeModelParts.Contains(original))
                {
                    sliceManager.activeModelParts.Add(original);
                }
            }
        }
    }

    // This is called only when the redo history is cleared
    public void CleanUp()
    {
        foreach (var hull in newHulls)
        {
            if (hull != null)
            {
                Object.Destroy(hull);
            }
        }
    }

    private void SetupHull(GameObject hull, GameObject original)
    {
        hull.transform.SetParent(sliceManager.modelRootTransform, false);
        var collider = hull.AddComponent<MeshCollider>();
        collider.convex = true;
    }

    private IEnumerator AnimateSeparation(GameObject upperHull, GameObject lowerHull, GameObject original)
    {
        float duration = sliceManager.separationAnimationDuration;
        Bounds originalBounds = original.GetComponent<Renderer>().bounds;
        float separationDistance = originalBounds.size.magnitude * sliceManager.separationFactor;
        Vector3 separationVector = planeNormal * (separationDistance * 0.5f);

        Vector3 upperStartPos = upperHull.transform.position;
        Vector3 lowerStartPos = lowerHull.transform.position;
        Vector3 upperEndPos = upperStartPos + separationVector;
        Vector3 lowerEndPos = lowerStartPos - separationVector;

        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            if (upperHull == null || lowerHull == null) yield break;

            float t = Mathf.SmoothStep(0.0f, 1.0f, elapsedTime / duration);
            upperHull.transform.position = Vector3.Lerp(upperStartPos, upperEndPos, t);
            lowerHull.transform.position = Vector3.Lerp(lowerStartPos, lowerEndPos, t);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        if (upperHull != null) upperHull.transform.position = upperEndPos;
        if (lowerHull != null) lowerHull.transform.position = lowerEndPos;
    }
}