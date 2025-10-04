using UnityEngine;
using EzySlice;

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

    private GameObject upperHullObject;
    private GameObject lowerHullObject;

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

        MeshFilter modelMeshFilter = targetModel.GetComponent<MeshFilter>();
        if (modelMeshFilter == null || modelMeshFilter.mesh == null)
        {
            Debug.LogError("Target Model must have a MeshFilter with a mesh assigned.");
            return;
        }

        Renderer modelRenderer = targetModel.GetComponent<Renderer>();
        if (modelRenderer != null)
        {
            modelCenterWorld = modelRenderer.bounds.center;
        }
        else
        {
            Debug.LogError("Target Model must have a Renderer component.");
            return;
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

        targetModel.SetActive(true);
        ResetClipping();
    }

    public void StartCutDrag(Vector2 screenPoint)
    {
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

    private void PerformSlice()
    {
        if (targetModel == null) return;

        ClearPreviousSlice();

        if (webSocketClientManager != null)
        {
            webSocketClientManager.SendActualCropPlane(currentPlanePoint, currentPlaneNormal);
        }

        if (crossSectionMaterial == null)
        {
            Debug.LogError("Cross Section Material is not assigned. Please assign it in the Inspector.");
            return;
        }

        SlicedHull sliceResult = targetModel.Slice(currentPlanePoint, currentPlaneNormal, crossSectionMaterial);
        if (sliceResult != null)
        {
            targetModel.SetActive(false);

            upperHullObject = sliceResult.CreateUpperHull(targetModel, crossSectionMaterial);
            lowerHullObject = sliceResult.CreateLowerHull(targetModel, crossSectionMaterial);

            if (upperHullObject != null && lowerHullObject != null)
            {
                // ** THE FIX IS HERE **
                // 1. Parent the new objects to the root transform first.
                upperHullObject.transform.SetParent(modelRootTransform, false);
                lowerHullObject.transform.SetParent(modelRootTransform, false);

                // 2. Now that they are parented, copy the LOCAL transform properties from the original model.
                // This ensures they correctly inherit the parent's scale (from zooming).
                upperHullObject.transform.localPosition = targetModel.transform.localPosition;
                upperHullObject.transform.localRotation = targetModel.transform.localRotation;
                upperHullObject.transform.localScale = targetModel.transform.localScale;

                lowerHullObject.transform.localPosition = targetModel.transform.localPosition;
                lowerHullObject.transform.localRotation = targetModel.transform.localRotation;
                lowerHullObject.transform.localScale = targetModel.transform.localScale;

                // 3. Now apply the separation in world space.
                // The bounds are in world space, so this calculation remains correct.
                Bounds originalBounds = targetModel.GetComponent<Renderer>().bounds;
                float separationDistance = originalBounds.size.magnitude * separationFactor;
                Vector3 separationVector = currentPlaneNormal * (separationDistance * 0.5f);

                upperHullObject.transform.position += separationVector;
                lowerHullObject.transform.position -= separationVector;
            }
            else
            {
                ClearPreviousSlice();
                targetModel.SetActive(true);
                Debug.LogWarning("Slicing failed to produce two valid hull objects.");
            }
        }
    }

    private void ClearPreviousSlice()
    {
        if (upperHullObject != null)
        {
            Destroy(upperHullObject);
            upperHullObject = null;
        }
        if (lowerHullObject != null)
        {
            Destroy(lowerHullObject);
            lowerHullObject = null;
        }
    }

    public void ResetCrop()
    {
        if (targetModel == null) return;

        if (webSocketClientManager != null)
        {
            webSocketClientManager.SendResetCrop();
        }

        ClearPreviousSlice();

        targetModel.SetActive(true);

        modelCenterWorld = targetModel.GetComponent<Renderer>().bounds.center;

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
}