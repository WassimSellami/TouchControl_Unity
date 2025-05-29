using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.EventSystems;

public class InputManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CameraController cameraController;
    [SerializeField] private EventSystem eventSystem;

    [Header("Input Settings")]
    private int orbitTouchCount = 1;
    private int zoomTouchCount = 2;
    private int minPanTouchCount = 3;

    private Dictionary<int, Vector2> activeTouches = new Dictionary<int, Vector2>();
    private Vector2 previousCentroidScreen;
    private Vector2 lastSingleTouchPositionScreen;
    private float previousPinchDistance;
    private Vector2 currentPinchCenterScreen;

    private bool isPanning = false;
    private bool isOrbiting = false;
    private bool isZooming = false;

    void Start()
    {
        if (cameraController == null) { Debug.LogError("InputManager: cameraController not assigned!"); this.enabled = false; return; }
        if (eventSystem == null) { eventSystem = EventSystem.current; }
        if (eventSystem == null) { Debug.LogError("InputManager: EventSystem not found or assigned!"); this.enabled = false; return; }
    }

    void Update()
    {
        bool pointerOverUI = false;
        for (int i = 0; i < Input.touchCount; i++)
        {
            if (eventSystem.IsPointerOverGameObject(Input.GetTouch(i).fingerId))
            {
                pointerOverUI = true;
                break;
            }
        }
#if UNITY_EDITOR
        if (Input.mousePresent && Input.touchCount == 0 && eventSystem.IsPointerOverGameObject())
        {
            pointerOverUI = true;
        }
#endif

        if (pointerOverUI)
        {
            ResetGestureStates();
            return;
        }

        int currentTouchCount = Input.touchCount;

        if (currentTouchCount == 0)
        {
            ResetGestureStates();
            return;
        }

        if (currentTouchCount == zoomTouchCount) HandleZoomGesture();
        if (!isZooming && currentTouchCount >= minPanTouchCount) HandlePanGesture();
        if (!isZooming && !isPanning && currentTouchCount == orbitTouchCount) HandleOrbitGesture();

        if (isZooming && currentTouchCount != zoomTouchCount) isZooming = false;
        if (isPanning && currentTouchCount < minPanTouchCount) isPanning = false;
        if (isOrbiting && currentTouchCount != orbitTouchCount) isOrbiting = false;

        if (!isZooming && !isPanning && !isOrbiting && currentTouchCount > 0)
        {
            if (currentTouchCount == 1) lastSingleTouchPositionScreen = Input.GetTouch(0).position;
            if (currentTouchCount == 2) previousPinchDistance = Vector2.Distance(Input.GetTouch(0).position, Input.GetTouch(1).position);
            if (currentTouchCount >= minPanTouchCount) { UpdateActiveTouches(); previousCentroidScreen = GetCentroid(activeTouches.Values.ToList()); }
        }
    }

    private void ResetGestureStates()
    {
        if (isPanning) cameraController.EndPan();
        isPanning = false;
        isOrbiting = false;
        isZooming = false;
        activeTouches.Clear();
    }

    private void HandleOrbitGesture()
    {
        if (cameraController == null || Input.touchCount != orbitTouchCount) { isOrbiting = false; return; }
        Touch touch = Input.GetTouch(0);
        switch (touch.phase)
        {
            case TouchPhase.Began:
                lastSingleTouchPositionScreen = touch.position;
                isOrbiting = true;
                break;
            case TouchPhase.Moved:
                if (isOrbiting)
                {
                    Vector2 delta = touch.position - lastSingleTouchPositionScreen;
                    if (delta.sqrMagnitude > 0.01f)
                    {
                        cameraController.ProcessOrbit(delta); // REMOVED Time.deltaTime
                    }
                    lastSingleTouchPositionScreen = touch.position;
                }
                break;
            case TouchPhase.Ended: case TouchPhase.Canceled: isOrbiting = false; break;
        }
    }

    private void HandlePanGesture()
    {
        if (cameraController == null || Input.touchCount < minPanTouchCount) { isPanning = false; return; }
        UpdateActiveTouches();
        if (activeTouches.Count < minPanTouchCount) { isPanning = false; return; }
        Vector2 currentCentroid = GetCentroid(activeTouches.Values.ToList());
        if (!isPanning)
        {
            isPanning = true;
            previousCentroidScreen = currentCentroid;
            // cameraController.StartPan(currentCentroid); // If StartPan needs current screen pos for target setup
        }
        else
        {
            Vector2 deltaCentroid = currentCentroid - previousCentroidScreen;
            if (deltaCentroid.sqrMagnitude > 0.01f) cameraController.ProcessPanDelta(deltaCentroid);
            previousCentroidScreen = currentCentroid;
        }
        bool panEndedDueToTouchEnd = false;
        for (int i = 0; i < Input.touchCount; ++i)
        {
            Touch t = Input.GetTouch(i);
            if (activeTouches.ContainsKey(t.fingerId) && (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled))
            {
                panEndedDueToTouchEnd = true; break;
            }
        }
        if (panEndedDueToTouchEnd && Input.touchCount < minPanTouchCount) isPanning = false;
    }

    private void HandleZoomGesture()
    {
        if (cameraController == null || Input.touchCount != zoomTouchCount) { isZooming = false; return; }
        Touch touchZero = Input.GetTouch(0); Touch touchOne = Input.GetTouch(1);
        currentPinchCenterScreen = (touchZero.position + touchOne.position) / 2f;
        float currentPinchDistance = Vector2.Distance(touchZero.position, touchOne.position);
        if (touchZero.phase == TouchPhase.Began || touchOne.phase == TouchPhase.Began)
        {
            previousPinchDistance = currentPinchDistance;
            isZooming = true;
        }
        else if ((touchZero.phase == TouchPhase.Moved || touchOne.phase == TouchPhase.Moved) && isZooming)
        {
            float deltaDistance = currentPinchDistance - previousPinchDistance;
            if (Mathf.Abs(deltaDistance) > 0.1f) cameraController.ProcessZoom(deltaDistance, currentPinchCenterScreen);
            previousPinchDistance = currentPinchDistance;
        }
        else if (touchZero.phase == TouchPhase.Ended || touchOne.phase == TouchPhase.Ended ||
                 touchZero.phase == TouchPhase.Canceled || touchOne.phase == TouchPhase.Canceled) { isZooming = false; }
    }

    private void UpdateActiveTouches()
    {
        activeTouches.Clear();
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled) activeTouches[touch.fingerId] = touch.position;
        }
    }

    private Vector2 GetCentroid(List<Vector2> points)
    {
        if (points == null || points.Count == 0) return Vector2.zero;
        Vector2 sum = Vector2.zero;
        foreach (Vector2 p in points) sum += p;
        return sum / points.Count;
    }
}