using UnityEngine;
using System.Collections.Generic;
using System;
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
            ExecuteMeshSlice(originalPart, ref successfullySlicedOriginals, ref originalPartIDs);
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

    private void ExecuteMeshSlice(GameObject originalPart, ref List<GameObject> successfullySlicedOriginals, ref List<string> originalPartIDs)
    {
        var result = SliceUtility.ExecuteMeshSlice(
            originalPart,
            planePoint,
            planeNormal,
            sliceManager.crossSectionMaterial,
            sliceManager,
            sliceManager.modelRootTransform
        );

        if (result.isValid)
        {
            newHulls.Add(result.upperHull);
            newHulls.Add(result.lowerHull);
            sliceManager.activeModelParts.Add(result.upperHull);
            sliceManager.activeModelParts.Add(result.lowerHull);

            originalPartIDs.Add(originalPart.name);
            successfullySlicedOriginals.Add(originalPart);
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
}
