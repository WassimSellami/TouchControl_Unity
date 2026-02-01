using UnityEngine;
using EzySlice;
using System.Collections;
using System.Collections.Generic;
using System;
using UnityVolumeRendering;

public class SliceCommand : ICommand
{
    public string ActionID { get; private set; }

    private List<GameObject> originals;
    private List<GameObject> newHulls = new List<GameObject>();
    private Vector3 planePoint;
    private Vector3 planeNormal;
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
        planePoint = point;
        planeNormal = normal;
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

            if (originalPart.GetComponentInChildren<VolumeRenderedObject>() != null)
            {
                ExecuteVolumetricSlice(originalPart, ref successfullySlicedOriginals, ref originalPartIDs);
            }
            else
            {
                ExecuteMeshSlice(originalPart, ref successfullySlicedOriginals, ref originalPartIDs);
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
                planePoint = this.planePoint,
                planeNormal = this.planeNormal,
                separationFactor = Constants.SEPARATION_FACTOR,
                targetPartIDs = originalPartIDs.ToArray()
            };

            webSocketClientManager.SendExecuteSlice(sliceData);
        }

        hasBeenExecuted = true;
    }

    private void ExecuteVolumetricSlice(GameObject originalPart, ref List<GameObject> successfullySlicedOriginals, ref List<string> originalPartIDs)
    {
        GameObject partA = UnityEngine.Object.Instantiate(originalPart, originalPart.transform.parent);
        GameObject partB = UnityEngine.Object.Instantiate(originalPart, originalPart.transform.parent);

        partA.name = originalPart.name + "_A";
        partB.name = originalPart.name + "_B";

        Renderer volRenderer = originalPart.GetComponentInChildren<Renderer>();
        if (volRenderer == null) return;

        Vector3 localHitPos = volRenderer.transform.InverseTransformPoint(planePoint);
        Vector3 textureSpacePos = localHitPos + new Vector3(0.5f, 0.5f, 0.5f);

        ApplyVolumeCut(partA, textureSpacePos, planeNormal, false);
        ApplyVolumeCut(partB, textureSpacePos, planeNormal, true);

        sliceManager.StartCoroutine(AnimateSeparation(partA, partB, originalPart));

        newHulls.Add(partA);
        newHulls.Add(partB);
        sliceManager.activeModelParts.Add(partA);
        sliceManager.activeModelParts.Add(partB);

        originalPartIDs.Add(originalPart.name);
        successfullySlicedOriginals.Add(originalPart);
    }

    private void ApplyVolumeCut(GameObject root, Vector3 texturePoint, Vector3 worldNormal, bool invertNormal)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        foreach (Renderer rend in renderers)
        {
            if (!rend.gameObject.name.Contains("Shaft") && !rend.gameObject.name.Contains("Head"))
            {
                Vector3 localNormal = rend.transform.InverseTransformDirection(worldNormal);
                if (invertNormal) localNormal = -localNormal;

                rend.material.SetVector("_PlanePos", texturePoint);
                rend.material.SetVector("_PlaneNormal", localNormal);
            }
        }
    }

    private void ExecuteMeshSlice(GameObject originalPart, ref List<GameObject> successfullySlicedOriginals, ref List<string> originalPartIDs)
    {
        SlicedHull sliceResult = originalPart.Slice(planePoint, planeNormal, sliceManager.crossSectionMaterial);

        if (sliceResult != null)
        {
            GameObject upperHull = sliceResult.CreateUpperHull(originalPart, sliceManager.crossSectionMaterial);
            GameObject lowerHull = sliceResult.CreateLowerHull(originalPart, sliceManager.crossSectionMaterial);

            if (upperHull != null && lowerHull != null)
            {
                upperHull.name = originalPart.name + "_U";
                lowerHull.name = originalPart.name + "_L";

                SetupHull(upperHull, originalPart);
                SetupHull(lowerHull, originalPart);

                sliceManager.StartCoroutine(AnimateSeparation(upperHull, lowerHull, originalPart));

                newHulls.Add(upperHull);
                newHulls.Add(lowerHull);
                sliceManager.activeModelParts.Add(upperHull);
                sliceManager.activeModelParts.Add(lowerHull);

                originalPartIDs.Add(originalPart.name);
                successfullySlicedOriginals.Add(originalPart);
            }
        }
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
        Renderer rend = original.GetComponentInChildren<Renderer>();
        if (rend == null) yield break;

        Bounds originalBounds = rend.bounds;
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