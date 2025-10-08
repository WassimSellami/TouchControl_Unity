using UnityEngine;
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

    [HideInInspector]
    public List<GameObject> activeModelParts = new List<GameObject>();

    private GameObject activePlaneVisualizer;
    private LineRenderer activeLineRenderer;

    private Vector3 currentPlaneNormal = Vector3.up;
    private Vector3 currentPlanePoint = Vector3.zero;
    private Vector2 startPointScreen;
    private Vector3 startWorldPos;
    private Vector3 initialPlanePointForDepth;
    private Vector3 finalEndWorldPosRaw;
    private Vector3 modelCenterWorld;

    private const float VISUAL_DEPTH_OFFSET = 0.005f;
    private const float MIN_DRAG_DISTANCE_SQUARED = 4f;

    void Start()
    {
        if (webSocketClientManager == null) webSocketClientManager = FindObjectOfType<WebSocketClientManager>();
        if (mainCamera == null) mainCamera = Camera.main;
        if (targetModel == null) { Debug.LogError("Target Model GameObject not assigned."); return; }
        if (targetModel.GetComponent<Collider>() == null) targetModel.AddComponent<MeshCollider>();
        if (modelRootTransform == null) modelRootTransform = targetModel.transform.parent ?? new GameObject("ModelRoot").transform;

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

    public void ShowSliceIconAtPosition(Vector2 screenPoint)
    {
        if (webSocketClientManager == null) return;

        modelCenterWorld = GetCollectiveBounds().center;
        UnityEngine.Plane centerPlane = new UnityEngine.Plane(-mainCamera.transform.forward, modelCenterWorld);
        Ray rayOrigin = mainCamera.ScreenPointToRay(screenPoint);
        Vector3 worldPoint;

        if (centerPlane.Raycast(rayOrigin, out float enter))
        {
            worldPoint = rayOrigin.GetPoint(enter);
        }
        else
        {
            worldPoint = rayOrigin.GetPoint(planeDepth);
        }

        webSocketClientManager.SendShowSliceIcon(worldPoint);
    }

    public void HideSliceIcon()
    {
        if (webSocketClientManager != null)
        {
            webSocketClientManager.SendHideSliceIcon();
        }
    }

    public void StartCutDrag(Vector2 screenPoint)
    {
        modelCenterWorld = GetCollectiveBounds().center;
        startPointScreen = screenPoint;

        if (activePlaneVisualizer != null) activePlaneVisualizer.SetActive(false);

        UnityEngine.Plane centerPlane = new UnityEngine.Plane(-mainCamera.transform.forward, modelCenterWorld);
        Ray rayOrigin = mainCamera.ScreenPointToRay(screenPoint);

        if (centerPlane.Raycast(rayOrigin, out float enter)) initialPlanePointForDepth = rayOrigin.GetPoint(enter);
        else initialPlanePointForDepth = rayOrigin.GetPoint(planeDepth);

        float distanceToPlane = Vector3.Distance(mainCamera.transform.position, initialPlanePointForDepth);
        startWorldPos = mainCamera.ScreenToWorldPoint(new Vector3(startPointScreen.x, startPointScreen.y, distanceToPlane));

        if (activeLineRenderer != null) activeLineRenderer.enabled = true;
    }

    public void UpdateCutDrag(Vector2 screenPoint)
    {
        if (activeLineRenderer == null || mainCamera == null) return;

        Vector3 depthReference = initialPlanePointForDepth;
        float distanceToPlane = Vector3.Distance(mainCamera.transform.position, depthReference);

        finalEndWorldPosRaw = mainCamera.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, distanceToPlane));

        Vector3 cameraForward = mainCamera.transform.forward;
        Vector3 startWorld = startWorldPos + cameraForward * VISUAL_DEPTH_OFFSET;
        Vector3 endWorld = finalEndWorldPosRaw + cameraForward * VISUAL_DEPTH_OFFSET;

        activeLineRenderer.positionCount = 2;
        activeLineRenderer.SetPosition(0, startWorld);
        activeLineRenderer.SetPosition(1, endWorld);

        if (webSocketClientManager != null) webSocketClientManager.SendLineData(startWorld, endWorld);
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

    private void PerformSlice()
    {
        UnityEngine.Plane slicePlane = new UnityEngine.Plane(currentPlaneNormal, currentPlanePoint);
        List<GameObject> partsToSlice = new List<GameObject>();
        foreach (GameObject part in activeModelParts)
        {
            Renderer partRenderer = part.GetComponent<Renderer>();
            if (part != null && part.activeInHierarchy && partRenderer != null && DoesPlaneIntersectBounds(slicePlane, partRenderer.bounds))
            {
                partsToSlice.Add(part);
            }
        }

        if (partsToSlice.Count > 0)
        {
            ICommand sliceCommand = new SliceCommand(partsToSlice, currentPlanePoint, currentPlaneNormal, this, webSocketClientManager);
            HistoryManager.Instance.ExecuteCommand(sliceCommand);
        }
    }

    public void ResetCrop()
    {
        foreach (GameObject part in activeModelParts.ToList())
        {
            if (part != targetModel)
            {
                Destroy(part);
            }
        }
        activeModelParts.Clear();

        targetModel.SetActive(true);
        targetModel.name = "RootModel";
        activeModelParts.Add(targetModel);

        if (targetModel.GetComponent<Renderer>() != null) modelCenterWorld = targetModel.GetComponent<Renderer>().bounds.center;

        if (HistoryManager.Instance != null) HistoryManager.Instance.ClearHistory();
        if (webSocketClientManager != null) webSocketClientManager.SendResetAll();

        ResetClipping();
    }

    public void DestroyModelPart(GameObject partToDestroy)
    {
        if (partToDestroy == null) return;
        ICommand destroyCommand = new DestroyCommand(partToDestroy, activeModelParts, webSocketClientManager);
        HistoryManager.Instance.ExecuteCommand(destroyCommand);
        if (activePlaneVisualizer != null) activePlaneVisualizer.SetActive(false);
    }

    public void StartShake(GameObject partToShake, Vector2 screenPoint)
    {
        if (partToShake != null && webSocketClientManager != null)
        {
            modelCenterWorld = GetCollectiveBounds().center;
            UnityEngine.Plane centerPlane = new UnityEngine.Plane(-mainCamera.transform.forward, modelCenterWorld);
            Ray rayOrigin = mainCamera.ScreenPointToRay(screenPoint);
            Vector3 worldPoint;

            if (centerPlane.Raycast(rayOrigin, out float enter))
            {
                worldPoint = rayOrigin.GetPoint(enter);
            }
            else
            {
                worldPoint = rayOrigin.GetPoint(planeDepth);
            }

            var shakeData = new DestroyActionData
            {
                targetPartID = partToShake.name,
                worldPosition = worldPoint
            };
            webSocketClientManager.SendStartShake(shakeData);
        }
    }

    public void StopShake(GameObject partToShake)
    {
        if (partToShake != null && webSocketClientManager != null)
        {
            var shakeData = new DestroyActionData { targetPartID = partToShake.name };
            webSocketClientManager.SendStopShake(shakeData);
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
        if (webSocketClientManager != null) webSocketClientManager.SendVisualCropPlane(currentPlaneNormal, currentPlaneNormal, planeScaleFactor);

        activePlaneVisualizer.transform.SetPositionAndRotation(currentPlanePoint, Quaternion.LookRotation(currentPlaneNormal));
        activePlaneVisualizer.transform.localScale = Vector3.one * planeScaleFactor;
        activePlaneVisualizer.SetActive(showPlaneVisualizer);
    }

    private void CalculatePlaneNormalByWorldPoints()
    {
        Vector3 lineVector = finalEndWorldPosRaw - startWorldPos;
        currentPlaneNormal = Vector3.Cross(lineVector, mainCamera.transform.forward).normalized;
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
            webSocketClientManager.SendHideSliceIcon();
        }
    }

    public void ResetClipping()
    {
        ResetDrawing();
        if (activePlaneVisualizer != null) activePlaneVisualizer.SetActive(false);
    }

    private bool DoesPlaneIntersectBounds(UnityEngine.Plane plane, Bounds bounds)
    {
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;

        Vector3[] corners = {
            center + new Vector3(extents.x, extents.y, extents.z), center + new Vector3(extents.x, extents.y, -extents.z),
            center + new Vector3(extents.x, -extents.y, extents.z), center + new Vector3(extents.x, -extents.y, -extents.z),
            center + new Vector3(-extents.x, extents.y, extents.z), center + new Vector3(-extents.x, extents.y, -extents.z),
            center + new Vector3(-extents.x, -extents.y, extents.z), center + new Vector3(-extents.x, -extents.y, -extents.z)
        };

        bool firstSide = plane.GetSide(corners[0]);
        for (int i = 1; i < corners.Length; i++)
        {
            if (plane.GetSide(corners[i]) != firstSide) return true;
        }
        return false;
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
                    else collectiveBounds.Encapsulate(r.bounds);
                }
            }
        }
        return collectiveBounds;
    }
}