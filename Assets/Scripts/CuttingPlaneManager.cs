using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityVolumeRendering;

public class CuttingPlaneManager : MonoBehaviour
{
    [Header("Core Components")]
    public Camera mainCamera;
    public Transform modelRootTransform;
    public WebSocketClientManager webSocketClientManager;

    [Header("Slicing Options")]
    public Material crossSectionMaterial;
    public bool showPlaneVisualizer = true;

    [Header("Visuals Prefabs")]
    public GameObject lineRendererPrefab;
    public GameObject planeVisualizerPrefab;

    [Header("Client UI Feedback (2D)")]
    [SerializeField] private RectTransform uiCanvasRect;
    [SerializeField] private Image feedbackIcon;
    [SerializeField] private Sprite trashIconSprite;
    [SerializeField] private Sprite sliceIconSprite;

    [HideInInspector]
    public List<GameObject> activeModelParts = new List<GameObject>();

    [SerializeField]
    private ModelViewportController modelViewportController;

    private GameObject targetModel;
    private GameObject activePlaneVisualizer;
    private LineRenderer activeLineRenderer;

    private Vector3 currentPlaneNormal = Vector3.up;
    private Vector3 currentPlanePoint = Vector3.zero;
    private Vector2 startPointScreen;
    private Vector3 startWorldPos;
    private Vector3 initialPlanePointForDepth;
    private Vector3 finalEndWorldPosRaw;
    private Vector3 modelCenterWorld;

    private Dictionary<GameObject, Coroutine> localShakingCoroutines = new Dictionary<GameObject, Coroutine>();
    private Dictionary<GameObject, Quaternion> localOriginalRotations = new Dictionary<GameObject, Quaternion>();

    void Start()
    {
        if (webSocketClientManager == null)
            webSocketClientManager = FindObjectOfType<WebSocketClientManager>();

        if (mainCamera == null)
            mainCamera = Camera.main;

        if (modelViewportController == null)
            modelViewportController = FindObjectOfType<ModelViewportController>();

        targetModel = modelViewportController.gameObject;

        if (targetModel.GetComponent<Collider>() == null)
            targetModel.AddComponent<MeshCollider>();

        if (modelRootTransform == null)
            modelRootTransform = targetModel.transform.parent ?? new GameObject("ModelRoot").transform;

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

        if (feedbackIcon != null)
        {
            feedbackIcon.gameObject.SetActive(false);
            feedbackIcon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            feedbackIcon.raycastTarget = false;
        }

        ResetCrop();
    }

    public void ShowSliceIconAtPosition(Vector2 screenPoint)
    {
        ShowLocalFeedbackIcon(screenPoint, sliceIconSprite);

        if (webSocketClientManager != null)
        {
            modelCenterWorld = GetCollectiveBounds().center;
            UnityEngine.Plane centerPlane = new UnityEngine.Plane(-mainCamera.transform.forward, modelCenterWorld);
            Ray rayOrigin = mainCamera.ScreenPointToRay(screenPoint);
            if (centerPlane.Raycast(rayOrigin, out float enter))
            {
                webSocketClientManager.SendShowSliceIcon(rayOrigin.GetPoint(enter));
            }
        }
    }

    public void HideSliceIcon()
    {
        HideLocalFeedbackIcon();
        if (webSocketClientManager != null) webSocketClientManager.SendHideSliceIcon();
    }

    public void StartCutDrag(Vector2 screenPoint)
    {
        modelCenterWorld = GetCollectiveBounds().center;
        startPointScreen = screenPoint;
        if (activePlaneVisualizer != null) activePlaneVisualizer.SetActive(false);
        UnityEngine.Plane centerPlane = new UnityEngine.Plane(-mainCamera.transform.forward, modelCenterWorld);
        Ray rayOrigin = mainCamera.ScreenPointToRay(screenPoint);
        if (centerPlane.Raycast(rayOrigin, out float enter)) initialPlanePointForDepth = rayOrigin.GetPoint(enter);
        else initialPlanePointForDepth = rayOrigin.GetPoint(Constants.PLANE_DEPTH);
        float distanceToPlane = Vector3.Distance(mainCamera.transform.position, initialPlanePointForDepth);
        startWorldPos = mainCamera.ScreenToWorldPoint(new Vector3(startPointScreen.x, startPointScreen.y, distanceToPlane));
        if (activeLineRenderer != null) activeLineRenderer.enabled = true;
    }

    public void EndCutGesture(Vector2 endPointScreen)
    {
        if (activeLineRenderer != null && mainCamera != null)
        {
            float distanceToPlane = Vector3.Distance(mainCamera.transform.position, initialPlanePointForDepth);
            finalEndWorldPosRaw = mainCamera.ScreenToWorldPoint(new Vector3(endPointScreen.x, endPointScreen.y, distanceToPlane));
        }

        if (Vector2.SqrMagnitude(startPointScreen - endPointScreen) > Constants.MIN_DRAG_DISTANCE_SQUARED)
        {
            Vector3 lineVector = finalEndWorldPosRaw - startWorldPos;
            currentPlaneNormal = Vector3.Cross(lineVector, mainCamera.transform.forward).normalized;
            currentPlanePoint = (startWorldPos + finalEndWorldPosRaw) * 0.5f;
            PerformSlice();
            Invoke(nameof(HideLineAndShowPlane), Constants.LINE_DURATION);
        }
        else ResetDrawing();
    }

    public void UpdateCutDrag(Vector2 screenPoint)
    {
        if (activeLineRenderer == null || mainCamera == null) return;
        float distanceToPlane = Vector3.Distance(mainCamera.transform.position, initialPlanePointForDepth);
        finalEndWorldPosRaw = mainCamera.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, distanceToPlane));
        Vector3 cameraForward = mainCamera.transform.forward;
        Vector3 startWorld = startWorldPos + cameraForward * Constants.VISUAL_DEPTH_OFFSET;
        Vector3 endWorld = finalEndWorldPosRaw + cameraForward * Constants.VISUAL_DEPTH_OFFSET;
        activeLineRenderer.positionCount = 2;
        activeLineRenderer.SetPosition(0, startWorld);
        activeLineRenderer.SetPosition(1, endWorld);
        if (webSocketClientManager != null) webSocketClientManager.SendLineData(startWorld, endWorld);
    }

    private void DrawPlaneVisualizer()
    {
        if (activePlaneVisualizer == null) return;
        if (webSocketClientManager != null) webSocketClientManager.SendVisualCropPlane(currentPlanePoint, currentPlaneNormal, Constants.PLANE_SCALE_FACTOR);
        activePlaneVisualizer.transform.SetPositionAndRotation(currentPlanePoint, Quaternion.LookRotation(currentPlaneNormal));
        activePlaneVisualizer.transform.localScale = Vector3.one * Constants.PLANE_SCALE_FACTOR;
        activePlaneVisualizer.SetActive(showPlaneVisualizer);
    }

    private void PerformSlice()
    {
        UnityEngine.Plane slicePlane = new UnityEngine.Plane(currentPlaneNormal, currentPlanePoint);
        List<GameObject> partsToSlice = new List<GameObject>();
        activeModelParts.RemoveAll(p => p == null);
        foreach (GameObject part in activeModelParts)
        {
            if (part == null || !part.activeInHierarchy) continue;
            Renderer partRenderer = part.GetComponentInChildren<Renderer>();
            if (partRenderer != null && DoesPlaneIntersectBounds(slicePlane, partRenderer.bounds)) partsToSlice.Add(part);
        }
        if (partsToSlice.Count > 0)
        {
            ICommand sliceCommand = new SliceCommand(partsToSlice, currentPlanePoint, currentPlaneNormal, this, webSocketClientManager);
            HistoryManager.Instance.ExecuteCommand(sliceCommand);
        }
    }

    public void ResetCrop()
    {
        activeModelParts.RemoveAll(x => x == null);

        for (int i = activeModelParts.Count - 1; i >= 0; i--)
        {
            GameObject part = activeModelParts[i];
            if (part != null && part.name != "RootModel")
            {
                Destroy(part);
            }
        }
        activeModelParts.Clear();

        Transform worldContainer = targetModel.transform.Find("WorldContainer");
        Transform modelContainer = worldContainer != null ? worldContainer.Find("ModelContainer") : null;

        if (modelContainer != null && modelContainer.childCount > 0)
        {
            GameObject loadedRoot = modelContainer.GetChild(0).gameObject;
            loadedRoot.SetActive(true);
            loadedRoot.name = "RootModel";

            activeModelParts.Add(loadedRoot);

            Renderer[] rends = loadedRoot.GetComponentsInChildren<Renderer>();
            foreach (var r in rends)
            {
                if (r.material.HasProperty("_PlanePos"))
                {
                    r.material.SetVector("_PlanePos", new Vector3(-10, -10, -10));
                }
            }
        }

        modelCenterWorld = GetCollectiveBounds().center;
        if (HistoryManager.Instance != null) HistoryManager.Instance.ClearHistory();
        if (webSocketClientManager != null) webSocketClientManager.SendResetAll();
        ResetClipping();
    }

    public void DestroyModelPart(GameObject partToDestroy)
    {
        if (partToDestroy == null) return;
        StopShake(partToDestroy);
        ICommand destroyCommand = new DestroyCommand(partToDestroy, activeModelParts, webSocketClientManager);
        HistoryManager.Instance.ExecuteCommand(destroyCommand);
        if (activePlaneVisualizer != null) activePlaneVisualizer.SetActive(false);
    }

    public void StartShake(GameObject partToShake, Vector2 screenPoint)
    {
        if (partToShake == null) return;

        ShowLocalFeedbackIcon(screenPoint, trashIconSprite);

        if (webSocketClientManager != null)
        {
            modelCenterWorld = GetCollectiveBounds().center;
            UnityEngine.Plane centerPlane = new UnityEngine.Plane(-mainCamera.transform.forward, modelCenterWorld);
            Ray rayOrigin = mainCamera.ScreenPointToRay(screenPoint);
            Vector3 worldPoint;
            if (centerPlane.Raycast(rayOrigin, out float enter)) worldPoint = rayOrigin.GetPoint(enter);
            else worldPoint = rayOrigin.GetPoint(Constants.PLANE_DEPTH);
            var shakeData = new DestroyActionData { targetPartID = partToShake.name, worldPosition = worldPoint };
            webSocketClientManager.SendStartShake(shakeData);
        }

        if (!localShakingCoroutines.ContainsKey(partToShake))
        {
            localOriginalRotations[partToShake] = partToShake.transform.localRotation;
            localShakingCoroutines[partToShake] = StartCoroutine(LocalShakeCoroutine(partToShake));
        }
    }

    public void StopShake(GameObject partToShake)
    {
        if (partToShake == null) return;

        if (webSocketClientManager != null)
        {
            var shakeData = new DestroyActionData { targetPartID = partToShake.name };
            webSocketClientManager.SendStopShake(shakeData);
        }

        if (localShakingCoroutines.TryGetValue(partToShake, out Coroutine coroutine))
        {
            StopCoroutine(coroutine);
            localShakingCoroutines.Remove(partToShake);

            if (localOriginalRotations.TryGetValue(partToShake, out Quaternion rot))
            {
                partToShake.transform.localRotation = rot;
                localOriginalRotations.Remove(partToShake);
            }
        }

        HideLocalFeedbackIcon();
    }

    private void ShowLocalFeedbackIcon(Vector2 screenPoint, Sprite icon)
    {
        if (feedbackIcon == null || uiCanvasRect == null) return;

        Vector2 adjustedScreenPoint = new Vector2(screenPoint.x, screenPoint.y + (Screen.height * Constants.ICON_VERTICAL_OFFSET_PERCENT));

        feedbackIcon.sprite = icon;
        feedbackIcon.gameObject.SetActive(true);

        Canvas canvas = feedbackIcon.canvas;
        if (canvas.renderMode == UnityEngine.RenderMode.ScreenSpaceOverlay)
        {
            feedbackIcon.transform.position = adjustedScreenPoint;
        }
        else
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                uiCanvasRect,
                adjustedScreenPoint,
                canvas.worldCamera,
                out Vector2 localPoint);
            feedbackIcon.rectTransform.anchoredPosition = localPoint;
        }
    }

    private void HideLocalFeedbackIcon()
    {
        if (feedbackIcon != null) feedbackIcon.gameObject.SetActive(false);
    }

    private IEnumerator LocalShakeCoroutine(GameObject target)
    {
        Transform t = target.transform;
        Quaternion startRot = t.localRotation;
        while (true)
        {
            float angle = Mathf.Sin(Time.time * Constants.WIGGLE_SPEED) * Constants.WIGGLE_ANGLE;
            t.localRotation = startRot * Quaternion.AngleAxis(angle, Vector3.up);
            yield return null;
        }
    }

    public GameObject GetModelPartAtScreenPoint(Vector2 screenPoint)
    {
        if (mainCamera == null) return null;
        Ray ray = mainCamera.ScreenPointToRay(screenPoint);
        if (Physics.Raycast(ray, out UnityEngine.RaycastHit hit))
        {
            foreach (var part in activeModelParts)
            {
                if (hit.collider.gameObject == part || hit.collider.transform.IsChildOf(part.transform)) return part;
            }
        }
        return null;
    }

    private void HideLineAndShowPlane() { ResetDrawing(); DrawPlaneVisualizer(); }
    private void ResetDrawing()
    {
        if (activeLineRenderer != null) { activeLineRenderer.positionCount = 0; activeLineRenderer.enabled = false; }
        if (webSocketClientManager != null) { webSocketClientManager.SendHideLine(); webSocketClientManager.SendHideSliceIcon(); }
        HideLocalFeedbackIcon();
    }
    public void ResetClipping() { ResetDrawing(); if (activePlaneVisualizer != null) activePlaneVisualizer.SetActive(false); }

    private bool DoesPlaneIntersectBounds(UnityEngine.Plane plane, Bounds bounds)
    {
        Vector3 center = bounds.center; Vector3 extents = bounds.extents;
        Vector3[] corners = {
            center + new Vector3(extents.x, extents.y, extents.z), center + new Vector3(extents.x, extents.y, -extents.z),
            center + new Vector3(extents.x, -extents.y, extents.z), center + new Vector3(extents.x, -extents.y, -extents.z),
            center + new Vector3(-extents.x, extents.y, extents.z), center + new Vector3(-extents.x, extents.y, -extents.z),
            center + new Vector3(-extents.x, -extents.y, extents.z), center + new Vector3(-extents.x, -extents.y, -extents.z)
        };
        bool firstSide = plane.GetSide(corners[0]);
        for (int i = 1; i < corners.Length; i++) if (plane.GetSide(corners[i]) != firstSide) return true;
        return false;
    }

    private Bounds GetCollectiveBounds()
    {
        Bounds collectiveBounds = new Bounds(targetModel.transform.position, Vector3.zero);
        bool first = true;
        foreach (var part in activeModelParts)
        {
            if (part != null && part.activeInHierarchy)
            {
                Renderer[] renderers = part.GetComponentsInChildren<Renderer>();
                foreach (var r in renderers)
                {
                    if (first) { collectiveBounds = r.bounds; first = false; }
                    else collectiveBounds.Encapsulate(r.bounds);
                }
            }
        }
        return collectiveBounds;
    }
}