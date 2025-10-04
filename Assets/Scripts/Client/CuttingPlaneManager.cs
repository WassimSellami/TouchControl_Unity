using UnityEngine;
using EzySlice;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class CuttingPlaneManager : MonoBehaviour
{
    [Header("Core Components")]
    public Camera mainCamera;
    public GameObject targetModel;
    public Transform modelRootTransform;
    public WebSocketClientManager webSocketClientManager;

    [Header("Slicing Options")]
    public Material crossSectionMaterial;
    public bool showPlaneVisualizer = true;
    public float planeScaleFactor = 50f;
    public float separationFactor = 0.1f;
    [Tooltip("How long the separation animation takes in seconds.")]
    public float separationAnimationDuration = 0.3f;

    [Header("Visuals Prefabs")]
    public GameObject lineRendererPrefab;
    public GameObject planeVisualizerPrefab;

    [Header("Interaction Settings")]
    public float planeDepth = 10f;
    public float lineDuration = 0.5f;

    private GameObject activePlaneVisualizer;
    private LineRenderer activeLineRenderer;

    private Vector3 currentPlaneNormal = Vector3.up;
    private Vector3 currentPlanePoint = Vector3.zero;
    private Vector2 startPointScreen;
    private Vector3 startWorldPos;
    private Vector3 initialPlanePointForDepth;
    private Vector3 finalEndWorldPosRaw;
    private Vector3 modelCenterWorld;

    private List<GameObject> activeModelParts = new List<GameObject>();

    private const float VISUAL_DEPTH_OFFSET = 0.005f;
    private const float MIN_DRAG_DISTANCE_SQUARED = 4f;

    void Start()
    {
        if (webSocketClientManager == null)
        {
            webSocketClientManager = FindObjectOfType<WebSocketClientManager>();
        }

        if (mainCamera == null) mainCamera = Camera.main;
        if (targetModel == null) { Debug.LogError("Target Model GameObject not assigned."); return; }

        if (targetModel.GetComponent<Collider>() == null)
        {
            targetModel.AddComponent<MeshCollider>();
        }

        if (modelRootTransform == null)
        {
            modelRootTransform = targetModel.transform.parent ?? new GameObject("ModelRoot").transform;
        }

        if (planeVisualizerPrefab != null)
        {
            activePlaneVisualizer = Instantiate(planeVisualizerPrefab, transform);
            activePlaneVisualizer.SetActive(false);
            activePlaneVisualizer.transform.SetParent(modelRootTransform, false);
        }

        if (lineRendererPrefab != null)
        {
            GameObject lineObject = Instantiate(lineRendererPrefab, transform);
            activeLineRenderer = lineObject.GetComponent<LineRenderer>();
            if (activeLineRenderer != null)
            {
                activeLineRenderer.positionCount = 0;
                activeLineRenderer.enabled = false;
            }
        }

        ResetCrop();
    }

    public void StartCutDrag(Vector2 screenPoint)
    {
        modelCenterWorld = GetCollectiveBounds().center;
        startPointScreen = screenPoint;

        if (activePlaneVisualizer != null)
        {
            activePlaneVisualizer.SetActive(false);
        }

        UnityEngine.Plane centerPlane = new UnityEngine.Plane(-mainCamera.transform.forward, modelCenterWorld);
        Ray rayOrigin = mainCamera.ScreenPointToRay(screenPoint);

        if (centerPlane.Raycast(rayOrigin, out float enter))
        {
            initialPlanePointForDepth = rayOrigin.GetPoint(enter);
        }
        else
        {
            initialPlanePointForDepth = rayOrigin.GetPoint(planeDepth);
        }

        float distanceToPlane = Vector3.Distance(mainCamera.transform.position, initialPlanePointForDepth);
        startWorldPos = mainCamera.ScreenToWorldPoint(
            new Vector3(startPointScreen.x, startPointScreen.y, distanceToPlane)
        );

        if (activeLineRenderer != null)
        {
            activeLineRenderer.enabled = true;
        }
    }

    public void UpdateCutDrag(Vector2 screenPoint)
    {
        if (activeLineRenderer == null || mainCamera == null) return;

        Vector3 depthReference = initialPlanePointForDepth;
        float distanceToPlane = Vector3.Distance(mainCamera.transform.position, depthReference);

        finalEndWorldPosRaw = mainCamera.ScreenToWorldPoint(
            new Vector3(screenPoint.x, screenPoint.y, distanceToPlane)
        );

        Vector3 cameraForward = mainCamera.transform.forward;
        Vector3 startWorld = startWorldPos + cameraForward * VISUAL_DEPTH_OFFSET;
        Vector3 endWorld = finalEndWorldPosRaw + cameraForward * VISUAL_DEPTH_OFFSET;

        activeLineRenderer.positionCount = 2;
        activeLineRenderer.SetPosition(0, startWorld);
        activeLineRenderer.SetPosition(1, endWorld);

        if (webSocketClientManager != null)
        {
            webSocketClientManager.SendLineData(startWorld, endWorld);
        }
    }

    public void EndCutGesture(Vector2 endPointScreen)
    {
        if (Vector2.SqrMagnitude(startPointScreen - endPointScreen) > MIN_DRAG_DISTANCE_SQUARED)
        {
            CalculatePlaneNormalByWorldPoints();
            ProjectPlaneOriginToModelCenter(startWorldPos);
            PerformSlice();
            Invoke(nameof(HideLineAndShowPlane), lineDuration);
        }
        else
        {
            ResetDrawing();
        }
    }

    private bool DoesPlaneIntersectBounds(UnityEngine.Plane plane, Bounds bounds)
    {
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;

        Vector3[] corners = new Vector3[8]
        {
            center + new Vector3(extents.x, extents.y, extents.z),
            center + new Vector3(extents.x, extents.y, -extents.z),
            center + new Vector3(extents.x, -extents.y, extents.z),
            center + new Vector3(extents.x, -extents.y, -extents.z),
            center + new Vector3(-extents.x, extents.y, extents.z),
            center + new Vector3(-extents.x, extents.y, -extents.z),
            center + new Vector3(-extents.x, -extents.y, extents.z),
            center + new Vector3(-extents.x, -extents.y, -extents.z)
        };

        bool firstSide = plane.GetSide(corners[0]);

        for (int i = 1; i < corners.Length; i++)
        {
            if (plane.GetSide(corners[i]) != firstSide)
            {
                return true;
            }
        }
        return false;
    }

    private void PerformSlice()
    {
        if (webSocketClientManager != null)
        {
            webSocketClientManager.SendActualCropPlane(currentPlanePoint, currentPlaneNormal);
        }

        if (crossSectionMaterial == null)
        {
            Debug.LogError("Cross Section Material is not assigned. Please assign it in the Inspector.");
            return;
        }

        UnityEngine.Plane slicePlane = new UnityEngine.Plane(currentPlaneNormal, currentPlanePoint);

        List<GameObject> partsToSlice = new List<GameObject>();
        foreach (GameObject part in activeModelParts)
        {
            Renderer partRenderer = part.GetComponent<Renderer>();
            if (part != null && partRenderer != null && DoesPlaneIntersectBounds(slicePlane, partRenderer.bounds))
            {
                partsToSlice.Add(part);
            }
        }

        if (partsToSlice.Count == 0) return;

        List<GameObject> newPartsCreated = new List<GameObject>();

        foreach (GameObject originalPart in partsToSlice)
        {
            SlicedHull sliceResult = originalPart.Slice(currentPlanePoint, currentPlaneNormal, crossSectionMaterial);
            if (sliceResult != null)
            {
                GameObject newUpperHull = sliceResult.CreateUpperHull(originalPart, crossSectionMaterial);
                GameObject newLowerHull = sliceResult.CreateLowerHull(originalPart, crossSectionMaterial);

                if (newUpperHull != null && newLowerHull != null)
                {
                    newUpperHull.transform.SetParent(modelRootTransform, false);
                    newLowerHull.transform.SetParent(modelRootTransform, false);

                    MeshCollider upperCollider = newUpperHull.AddComponent<MeshCollider>();
                    upperCollider.convex = true;
                    MeshCollider lowerCollider = newLowerHull.AddComponent<MeshCollider>();
                    lowerCollider.convex = true;

                    Bounds originalBounds = originalPart.GetComponent<Renderer>().bounds;
                    float separationDistance = originalBounds.size.magnitude * separationFactor;
                    Vector3 separationVector = currentPlaneNormal * (separationDistance * 0.5f);

                    StartCoroutine(AnimateSeparation(newUpperHull, newLowerHull, separationVector, separationAnimationDuration));

                    newPartsCreated.Add(newUpperHull);
                    newPartsCreated.Add(newLowerHull);

                    DestroyModelPart(originalPart);
                }
            }
        }
        activeModelParts.AddRange(newPartsCreated);
    }

    private IEnumerator AnimateSeparation(GameObject upperHull, GameObject lowerHull, Vector3 separationVector, float duration)
    {
        if (upperHull == null || lowerHull == null) yield break;

        Vector3 upperStartPos = upperHull.transform.position;
        Vector3 lowerStartPos = lowerHull.transform.position;

        Vector3 upperEndPos = upperStartPos + separationVector;
        Vector3 lowerEndPos = lowerStartPos - separationVector;

        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            if (upperHull == null || lowerHull == null) yield break;

            float t = elapsedTime / duration;
            float easedT = Mathf.SmoothStep(0.0f, 1.0f, t);

            upperHull.transform.position = Vector3.Lerp(upperStartPos, upperEndPos, easedT);
            lowerHull.transform.position = Vector3.Lerp(lowerStartPos, lowerEndPos, easedT);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        if (upperHull != null) upperHull.transform.position = upperEndPos;
        if (lowerHull != null) lowerHull.transform.position = lowerEndPos;
    }

    public void ResetCrop()
    {
        if (webSocketClientManager != null)
        {
            webSocketClientManager.SendResetCrop();
        }

        foreach (GameObject part in activeModelParts.ToList())
        {
            DestroyModelPart(part);
        }
        activeModelParts.Clear();

        targetModel.SetActive(true);
        activeModelParts.Add(targetModel);

        if (targetModel.GetComponent<Renderer>() != null)
        {
            modelCenterWorld = targetModel.GetComponent<Renderer>().bounds.center;
        }

        ResetClipping();
    }

    private void ProjectPlaneOriginToModelCenter(Vector3 planeDefinitionPoint)
    {
        float signedDistance = Vector3.Dot(currentPlaneNormal, modelCenterWorld - planeDefinitionPoint);
        currentPlanePoint = modelCenterWorld - currentPlaneNormal * signedDistance;
    }

    private void HideLineAndShowPlane()
    {
        ResetDrawing();
        DrawPlaneVisualizer();
    }

    private void DrawPlaneVisualizer()
    {
        if (activePlaneVisualizer == null) return;

        if (webSocketClientManager != null)
        {
            webSocketClientManager.SendVisualCropPlane(currentPlanePoint, currentPlaneNormal, planeScaleFactor);
        }

        Quaternion targetRotation = Quaternion.LookRotation(currentPlaneNormal);
        activePlaneVisualizer.transform.SetPositionAndRotation(currentPlanePoint, targetRotation);
        activePlaneVisualizer.transform.localScale = Vector3.one * planeScaleFactor;

        activePlaneVisualizer.SetActive(showPlaneVisualizer);
    }

    private void CalculatePlaneNormalByWorldPoints()
    {
        Vector3 lineVector = finalEndWorldPosRaw - startWorldPos;
        Vector3 cameraForward = mainCamera.transform.forward;
        Vector3 normal = Vector3.Cross(lineVector, cameraForward).normalized;
        currentPlaneNormal = normal;
    }

    private void ResetDrawing()
    {
        if (activeLineRenderer != null)
        {
            activeLineRenderer.positionCount = 0;
            activeLineRenderer.enabled = false;
        }
        if (webSocketClientManager != null)
        {
            webSocketClientManager.SendHideLine();
        }
    }

    public void ResetClipping()
    {
        ResetDrawing();
        if (activePlaneVisualizer != null)
        {
            activePlaneVisualizer.SetActive(false);
        }
    }

    public GameObject GetModelPartAtScreenPoint(Vector2 screenPoint)
    {
        if (mainCamera == null) return null;

        Ray ray = mainCamera.ScreenPointToRay(screenPoint);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            if (activeModelParts.Contains(hit.collider.gameObject))
            {
                return hit.collider.gameObject;
            }
        }
        return null;
    }

    public void DestroyModelPart(GameObject partToDestroy)
    {
        if (partToDestroy == null) return;

        if (activeModelParts.Contains(partToDestroy))
        {
            activeModelParts.Remove(partToDestroy);
        }

        if (partToDestroy == targetModel)
        {
            partToDestroy.SetActive(false);
        }
        else
        {
            Destroy(partToDestroy);
        }

        if (activePlaneVisualizer != null)
        {
            activePlaneVisualizer.SetActive(false);
        }
    }

    private Bounds GetCollectiveBounds()
    {
        if (activeModelParts.Count == 0) return new Bounds(transform.position, Vector3.zero);

        Bounds collectiveBounds = new Bounds();
        bool first = true;

        foreach (var part in activeModelParts)
        {
            if (part != null && part.activeInHierarchy)
            {
                Renderer r = part.GetComponent<Renderer>();
                if (r != null)
                {
                    if (first)
                    {
                        collectiveBounds = r.bounds;
                        first = false;
                    }
                    else
                    {
                        collectiveBounds.Encapsulate(r.bounds);
                    }
                }
            }
        }
        return collectiveBounds;
    }
}