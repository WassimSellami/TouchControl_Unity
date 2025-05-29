using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.EventSystems;

public class InputManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CameraController cameraController;
    [SerializeField] private EventSystem eventSystem;
    [SerializeField] private CropBoxController cropBoxController;
    [SerializeField] private Camera mainCameraForRaycasting;

    [Header("Input Settings")]
    private int orbitTouchCount = 1;
    private int zoomTouchCount = 2;
    private int minPanTouchCount = 3;
    [SerializeField] private LayerMask cropHandleLayerMask;

    private Dictionary<int, Vector2> activeTouches = new Dictionary<int, Vector2>();
    private Vector2 previousCentroidScreen;
    private Vector2 lastSingleTouchPositionScreen;
    private float previousPinchDistance;
    private Vector2 currentPinchCenterScreen;

    private bool isPanning = false;
    private bool isOrbiting = false;
    private bool isZooming = false;

    private bool isCroppingModeActive = false;
    private bool isDraggingCropHandle = false;
    private Transform draggedHandleTransform; // Changed name for clarity
    private CropBoxController.HandleDirection draggedHandleDirection;
    private Plane dragPlane;
    private Vector3 lastDragPointOnPlaneWorld; // Store world position

    void Start()
    {
        if (cameraController == null) { this.enabled = false; Debug.LogError("InputManager: CameraController not assigned!"); return; }
        if (eventSystem == null) { eventSystem = EventSystem.current; if (eventSystem == null) { this.enabled = false; Debug.LogError("InputManager: EventSystem not found!"); return; } }
        if (cropBoxController == null) Debug.LogError("InputManager: CropBoxController not assigned!");
        if (mainCameraForRaycasting == null) Debug.LogError("InputManager: MainCameraForRaycasting not assigned!");
        if (cropHandleLayerMask.value == 0 && cropBoxController != null) Debug.LogWarning("InputManager: CropHandleLayerMask not set. Handle dragging might not work. Ensure handles are on a specific layer and that layer is selected here.");
    }

    public void SetCroppingModeActive(bool isActive)
    {
        isCroppingModeActive = isActive;
        if (!isActive && isDraggingCropHandle)
        {
            EndCropHandleDrag();
        }
    }

    void Update()
    {
        bool pointerOverUI = false;
        if (Input.touchCount > 0 && eventSystem.IsPointerOverGameObject(Input.GetTouch(0).fingerId)) pointerOverUI = true;
#if UNITY_EDITOR
        if (Input.mousePresent && Input.GetMouseButton(0) && eventSystem.IsPointerOverGameObject()) pointerOverUI = true;
#endif

        if (pointerOverUI && !isDraggingCropHandle) { ResetGestureStates(); return; }

        int currentTouchCount = Input.touchCount;
        Vector2 primaryTouchPos = Vector2.zero;
        TouchPhase primaryTouchPhase = TouchPhase.Canceled;

        if (currentTouchCount > 0)
        {
            primaryTouchPos = Input.GetTouch(0).position;
            primaryTouchPhase = Input.GetTouch(0).phase;
        }
#if UNITY_EDITOR
        if (Input.mousePresent && Input.touchCount == 0)
        {
            currentTouchCount = Input.GetMouseButton(0) || Input.GetMouseButtonDown(0) || Input.GetMouseButtonUp(0) ? 1 : 0;
            if (currentTouchCount > 0) primaryTouchPos = Input.mousePosition;
            if (Input.GetMouseButtonDown(0)) primaryTouchPhase = TouchPhase.Began;
            else if (Input.GetMouseButton(0)) primaryTouchPhase = TouchPhase.Moved;
            else if (Input.GetMouseButtonUp(0)) primaryTouchPhase = TouchPhase.Ended;
        }
#endif


        if (isDraggingCropHandle)
        {
            if (currentTouchCount == 1 && primaryTouchPhase == TouchPhase.Moved) ProcessCropHandleDrag(primaryTouchPos);
            else if (currentTouchCount == 0 || (currentTouchCount == 1 && (primaryTouchPhase == TouchPhase.Ended || primaryTouchPhase == TouchPhase.Canceled))) EndCropHandleDrag();
            return;
        }

        if (currentTouchCount == 0) { ResetGestureStates(); return; }

        if (isCroppingModeActive && currentTouchCount == 1 && primaryTouchPhase == TouchPhase.Began)
        {
            TryStartCropHandleDrag(primaryTouchPos);
            if (isDraggingCropHandle) return;
        }

        if (currentTouchCount == zoomTouchCount) HandleZoomGesture(); // Assumes multi-touch for zoom
        else if (!isZooming && currentTouchCount >= minPanTouchCount) HandlePanGesture();
        else if (!isCroppingModeActive && !isZooming && !isPanning && currentTouchCount == orbitTouchCount && primaryTouchPhase != TouchPhase.Stationary) HandleOrbitGesture();

        if (isZooming && currentTouchCount != zoomTouchCount) isZooming = false;
        if (isPanning && currentTouchCount < minPanTouchCount) isPanning = false;
        if (isOrbiting && (isCroppingModeActive || currentTouchCount != orbitTouchCount || primaryTouchPhase == TouchPhase.Ended || primaryTouchPhase == TouchPhase.Canceled)) isOrbiting = false;


        if (!isZooming && !isPanning && !isOrbiting && !isDraggingCropHandle && currentTouchCount > 0)
        {
            if (currentTouchCount == 1) lastSingleTouchPositionScreen = primaryTouchPos;
            if (currentTouchCount == 2) previousPinchDistance = Vector2.Distance(Input.GetTouch(0).position, Input.GetTouch(1).position);
            if (currentTouchCount >= minPanTouchCount) { UpdateActiveTouches(); previousCentroidScreen = GetCentroid(activeTouches.Values.ToList()); }
        }
    }

    private void ResetGestureStates()
    {
        if (isPanning) cameraController.EndPan();
        isPanning = false; isOrbiting = false; isZooming = false;
        activeTouches.Clear();
    }

    private void TryStartCropHandleDrag(Vector2 screenPos)
    {
        if (mainCameraForRaycasting == null || cropBoxController == null) return;
        Ray ray = mainCameraForRaycasting.ScreenPointToRay(screenPos);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, Mathf.Infinity, cropHandleLayerMask))
        {
            HandleIdentifier handleId = hit.collider.GetComponent<HandleIdentifier>();
            if (handleId != null)
            {
                isDraggingCropHandle = true;
                draggedHandleTransform = hit.transform;
                draggedHandleDirection = handleId.direction;

                Vector3 planeNormalForDrag = mainCameraForRaycasting.transform.forward;
                dragPlane = new Plane(planeNormalForDrag, hit.point); // Plane facing the camera, at the hit point depth

                float distance;
                if (dragPlane.Raycast(ray, out distance))
                {
                    lastDragPointOnPlaneWorld = ray.GetPoint(distance);
                }
                isOrbiting = false; isPanning = false; isZooming = false;
            }
        }
    }

    private void ProcessCropHandleDrag(Vector2 screenPos)
    {
        if (mainCameraForRaycasting == null || cropBoxController == null || draggedHandleTransform == null) return;
        Ray ray = mainCameraForRaycasting.ScreenPointToRay(screenPos);
        float distance;
        if (dragPlane.Raycast(ray, out distance))
        {
            Vector3 currentDragPointOnPlaneWorld = ray.GetPoint(distance);
            Vector3 worldSpaceMovementDelta = currentDragPointOnPlaneWorld - lastDragPointOnPlaneWorld;

            Transform modelTransform = cropBoxController.targetModel.transform;
            Vector3 localSpaceMovementDelta = modelTransform.InverseTransformVector(worldSpaceMovementDelta);

            Vector3 currentMin = cropBoxController.GetMinBounds();
            Vector3 currentMax = cropBoxController.GetMaxBounds();

            switch (draggedHandleDirection)
            {
                case CropBoxController.HandleDirection.MinX: currentMin.x += localSpaceMovementDelta.x; break;
                case CropBoxController.HandleDirection.MaxX: currentMax.x += localSpaceMovementDelta.x; break;
                case CropBoxController.HandleDirection.MinY: currentMin.y += localSpaceMovementDelta.y; break;
                case CropBoxController.HandleDirection.MaxY: currentMax.y += localSpaceMovementDelta.y; break;
                case CropBoxController.HandleDirection.MinZ: currentMin.z += localSpaceMovementDelta.z; break;
                case CropBoxController.HandleDirection.MaxZ: currentMax.z += localSpaceMovementDelta.z; break;
            }

            float minSeparation = 0.05f; // Minimum size of the crop box dimension
            currentMin.x = Mathf.Min(currentMin.x, currentMax.x - minSeparation);
            currentMax.x = Mathf.Max(currentMax.x, currentMin.x + minSeparation);
            currentMin.y = Mathf.Min(currentMin.y, currentMax.y - minSeparation);
            currentMax.y = Mathf.Max(currentMax.y, currentMin.y + minSeparation);
            currentMin.z = Mathf.Min(currentMin.z, currentMax.z - minSeparation);
            currentMax.z = Mathf.Max(currentMax.z, currentMin.z + minSeparation);

            cropBoxController.SetBounds(currentMin, currentMax);
            lastDragPointOnPlaneWorld = currentDragPointOnPlaneWorld;
        }
    }

    private void EndCropHandleDrag() { isDraggingCropHandle = false; draggedHandleTransform = null; }
    private void HandleOrbitGesture() { if (cameraController == null || Input.touchCount != orbitTouchCount) { isOrbiting = false; return; } Touch touch = Input.GetTouch(0); switch (touch.phase) { case TouchPhase.Began: lastSingleTouchPositionScreen = touch.position; isOrbiting = true; break; case TouchPhase.Moved: if (isOrbiting) { Vector2 delta = touch.position - lastSingleTouchPositionScreen; if (delta.sqrMagnitude > 0.01f) cameraController.ProcessOrbit(delta * Time.deltaTime); lastSingleTouchPositionScreen = touch.position; } break; case TouchPhase.Ended: case TouchPhase.Canceled: isOrbiting = false; break; } }
    private void HandlePanGesture() { if (cameraController == null || Input.touchCount < minPanTouchCount) { isPanning = false; return; } UpdateActiveTouches(); if (activeTouches.Count < minPanTouchCount) { isPanning = false; return; } Vector2 currentCentroid = GetCentroid(activeTouches.Values.ToList()); if (!isPanning) { isPanning = true; previousCentroidScreen = currentCentroid; } else { Vector2 deltaCentroid = currentCentroid - previousCentroidScreen; if (deltaCentroid.sqrMagnitude > 0.01f) cameraController.ProcessPanDelta(deltaCentroid); previousCentroidScreen = currentCentroid; } bool panEnded = false; for (int i = 0; i < Input.touchCount; ++i) { if (activeTouches.ContainsKey(Input.GetTouch(i).fingerId) && (Input.GetTouch(i).phase == TouchPhase.Ended || Input.GetTouch(i).phase == TouchPhase.Canceled)) { panEnded = true; break; } } if (panEnded && Input.touchCount < minPanTouchCount) isPanning = false; }
    private void HandleZoomGesture() { if (cameraController == null || Input.touchCount != zoomTouchCount) { isZooming = false; return; } Touch touchZero = Input.GetTouch(0); Touch touchOne = Input.GetTouch(1); currentPinchCenterScreen = (touchZero.position + touchOne.position) / 2f; float currentPinchDistance = Vector2.Distance(touchZero.position, touchOne.position); if (touchZero.phase == TouchPhase.Began || touchOne.phase == TouchPhase.Began) { previousPinchDistance = currentPinchDistance; isZooming = true; } else if ((touchZero.phase == TouchPhase.Moved || touchOne.phase == TouchPhase.Moved) && isZooming) { float deltaDistance = currentPinchDistance - previousPinchDistance; if (Mathf.Abs(deltaDistance) > 0.1f) cameraController.ProcessZoom(deltaDistance, currentPinchCenterScreen); previousPinchDistance = currentPinchDistance; } else if (touchZero.phase == TouchPhase.Ended || touchOne.phase == TouchPhase.Ended || touchZero.phase == TouchPhase.Canceled || touchOne.phase == TouchPhase.Canceled) isZooming = false; }
    private void UpdateActiveTouches() { activeTouches.Clear(); for (int i = 0; i < Input.touchCount; i++) { Touch touch = Input.GetTouch(i); if (touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled) activeTouches[touch.fingerId] = touch.position; } }
    private Vector2 GetCentroid(List<Vector2> points) { if (points == null || points.Count == 0) return Vector2.zero; Vector2 sum = Vector2.zero; foreach (Vector2 p in points) sum += p; return sum / points.Count; }
}