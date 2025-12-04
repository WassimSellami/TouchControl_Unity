using UnityEngine;
using EzySlice;
using System.Collections;
using System.Collections.Generic;
using System;

public class SliceCommand : ICommand
{
    public string ActionID { get; private set; }

    private List<GameObject> originals;
    private List<GameObject> newHulls = new List<GameObject>();
    private Vector3 planePoint;   // WORLD space
    private Vector3 planeNormal;  // WORLD space
    private CuttingPlaneManager sliceManager;
    private WebSocketClientManager webSocketClientManager;
    private bool hasBeenExecuted = false;

    public SliceCommand(List<GameObject> objectsToSlice,
                        Vector3 point,
                        Vector3 normal,
                        CuttingPlaneManager manager,
                        WebSocketClientManager wsManager)
    {
        ActionID = Guid.NewGuid().ToString();
        originals = objectsToSlice;
        planePoint = point;    // assume world
        planeNormal = normal;  // assume world
        sliceManager = manager;
        webSocketClientManager = wsManager;
    }

    public void Execute()
    {
        if (hasBeenExecuted)
        {
            foreach (var hull in newHulls)
            {
                if (hull != null)
                {
                    hull.SetActive(true);
                    if (!sliceManager.activeModelParts.Contains(hull))
                        sliceManager.activeModelParts.Add(hull);
                }
            }

            foreach (var original in originals)
            {
                if (original != null)
                {
                    original.SetActive(false);
                    sliceManager.activeModelParts.Remove(original);
                }
            }

            return;
        }

        List<string> originalPartIDs = new List<string>();
        List<GameObject> successfullySlicedOriginals = new List<GameObject>();

        foreach (var originalPart in originals)
        {
            if (originalPart == null) continue;

            // planePoint and planeNormal are world‑space here
            SlicedHull sliceResult = originalPart.Slice(
                planePoint,
                planeNormal,
                sliceManager.crossSectionMaterial
            );

            if (sliceResult != null)
            {
                GameObject upperHull = sliceResult.CreateUpperHull(originalPart, sliceManager.crossSectionMaterial);
                GameObject lowerHull = sliceResult.CreateLowerHull(originalPart, sliceManager.crossSectionMaterial);

                if (upperHull != null && lowerHull != null)
                {
                    upperHull.name = originalPart.name + "_U";
                    lowerHull.name = originalPart.name + "_L";

                    var upperInfo = upperHull.AddComponent<ModelComponentInfo>();
                    upperInfo.sourceActionID = ActionID;
                    upperInfo.side = "Upper";

                    var lowerInfo = lowerHull.AddComponent<ModelComponentInfo>();
                    lowerInfo.sourceActionID = ActionID;
                    lowerInfo.side = "Lower";

                    SetupHull(upperHull, originalPart);
                    SetupHull(lowerHull, originalPart);

                    sliceManager.StartCoroutine(
                        AnimateSeparation(upperHull, lowerHull, originalPart)
                    );

                    newHulls.Add(upperHull);
                    newHulls.Add(lowerHull);
                    sliceManager.activeModelParts.Add(upperHull);
                    sliceManager.activeModelParts.Add(lowerHull);

                    originalPartIDs.Add(originalPart.name);
                    successfullySlicedOriginals.Add(originalPart);
                }
            }
        }

        foreach (var successfulOriginal in successfullySlicedOriginals)
        {
            if (successfulOriginal != null)
            {
                successfulOriginal.SetActive(false);
                sliceManager.activeModelParts.Remove(successfulOriginal);
            }
        }

        if (webSocketClientManager != null && originalPartIDs.Count > 0)
        {
            var sliceData = new SliceActionData
            {
                actionID = this.ActionID,
                planePoint = this.planePoint,       // world space
                planeNormal = this.planeNormal,     // world space
                separationFactor = Constants.SEPARATION_FACTOR,
                targetPartIDs = originalPartIDs.ToArray()
            };

            webSocketClientManager.SendExecuteSlice(sliceData);
        }

        hasBeenExecuted = true;
    }


    public void Undo()
    {
        foreach (var hull in newHulls)
        {
            if (hull != null)
            {
                hull.SetActive(false);
                sliceManager.activeModelParts.Remove(hull);
            }
        }

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

    public void CleanUp()
    {
        foreach (var hull in newHulls)
        {
            if (hull != null)
            {
                UnityEngine.Object.Destroy(hull);
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
        float duration = Constants.SEPARATION_ANIMATION_DURATION;
        Bounds originalBounds = original.GetComponent<Renderer>().bounds;
        float separationDistance = originalBounds.size.magnitude * Constants.SEPARATION_FACTOR;
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