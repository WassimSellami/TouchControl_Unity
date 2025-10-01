using UnityEngine;

public class CuttingPlaneManager : MonoBehaviour
{
    [Header("Setup")]
    public Camera mainCamera;
    public Renderer targetModelRenderer;

    [Header("Parenting")]
    public Transform modelRootTransform;

    [Header("Cutting Logic")]
    public float planeDepth = 10f;

    [Header("Visualization")]
    public GameObject lineRendererPrefab;
    public float lineDuration = 0.5f;

    [Header("Plane Visualization")]
    public GameObject planeVisualizerPrefab;
    public float planeScaleFactor = 50f;

    private Material targetMaterial;
    private GameObject activePlaneVisualizer;
    private LineRenderer activeLineRenderer;

    private bool isClipping = false;
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
        if (mainCamera == null) mainCamera = Camera.main;

        if (targetModelRenderer == null) { Debug.LogError("TargetModelController not assigned."); return; }
        if (modelRootTransform == null) { modelRootTransform = targetModelRenderer.transform; Debug.LogWarning("Model Root not explicitly set, defaulting to Renderer Transform."); }

        targetMaterial = targetModelRenderer.material;

        modelCenterWorld = targetModelRenderer.bounds.center;

        if (planeVisualizerPrefab != null)
        {
            activePlaneVisualizer = Instantiate(planeVisualizerPrefab, modelRootTransform);
            activePlaneVisualizer.SetActive(false);
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

        ResetClipping();
    }

    public void StartCutDrag(Vector2 screenPoint)
    {
        startPointScreen = screenPoint;

        if (activePlaneVisualizer != null)
        {
            activePlaneVisualizer.SetActive(false);
        }

        Plane centerPlane = new Plane(-mainCamera.transform.forward, modelCenterWorld);

        Ray rayOrigin = mainCamera.ScreenPointToRay(screenPoint);
        float enter;

        if (centerPlane.Raycast(rayOrigin, out enter))
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
    }

    public void EndCutGesture(Vector2 endPointScreen)
    {
        if (Vector2.SqrMagnitude(startPointScreen - endPointScreen) > MIN_DRAG_DISTANCE_SQUARED)
        {
            CalculatePlaneNormalByWorldPoints();

            ProjectPlaneOriginToModelCenter(startWorldPos);

            isClipping = true;

            ApplyClippingShaderProperties();

            Invoke(nameof(HideLineAndShowPlane), lineDuration);
        }
        else
        {
            ResetDrawing();
        }
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

        activePlaneVisualizer.transform.localPosition = modelRootTransform.InverseTransformPoint(currentPlanePoint);

        Quaternion localRotation = Quaternion.Inverse(modelRootTransform.rotation) * Quaternion.LookRotation(currentPlaneNormal);
        activePlaneVisualizer.transform.localRotation = localRotation;

        activePlaneVisualizer.transform.localScale = Vector3.one * planeScaleFactor;

        activePlaneVisualizer.SetActive(true);
    }

    private void CalculatePlaneNormalByWorldPoints()
    {
        Vector3 lineVector = finalEndWorldPosRaw - startWorldPos;
        Vector3 cameraVector = mainCamera.transform.position - startWorldPos;

        Vector3 normal = Vector3.Cross(lineVector, cameraVector).normalized;

        currentPlaneNormal = normal;
    }

    private void ApplyClippingShaderProperties()
    {
        if (targetMaterial == null) return;

        targetMaterial.SetInt("_ClippingEnabled", isClipping ? 1 : 0);

        if (isClipping)
        {
            targetMaterial.SetVector("_PlaneNormal", currentPlaneNormal);
            // SENDING WORLD SPACE POINT TO SHADER
            targetMaterial.SetVector("_PlanePoint", currentPlanePoint);
        }
    }

    private void ResetDrawing()
    {
        if (activeLineRenderer != null)
        {
            activeLineRenderer.positionCount = 0;
            activeLineRenderer.enabled = false;
        }
    }

    public void ResetClipping()
    {
        isClipping = false;
        ResetDrawing();
        ApplyClippingShaderProperties();

        if (activePlaneVisualizer != null)
        {
            activePlaneVisualizer.SetActive(false);
        }
    }

    public void FlipPlaneDirection()
    {
        if (isClipping)
        {
            currentPlaneNormal *= -1f;
            ApplyClippingShaderProperties();

            DrawPlaneVisualizer();
        }
    }
}