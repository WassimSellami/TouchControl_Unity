using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.EventSystems;

public class InputManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private CameraController cameraController;
    [SerializeField]
    private EventSystem eventSystem;

    private int orbitTouchCount = 1;
    private int zoomTouchCount = 2;
    private int minPanTouchCount = 3;

    private int smoothSamplesCount = 5;

    private Vector2 previousOrbitPosition;
    private Vector2 previousPanCentroid;
    private float previousZoomDistance;
    private Vector2 previousZoomCenter;

    private bool isPanning = false;
    private bool isOrbiting = false;
    private bool isZooming = false;

    private Queue<Vector2> orbitPositionHistory = new Queue<Vector2>();
    private Queue<Vector2> panCentroidHistory = new Queue<Vector2>();
    private Queue<float> zoomDistanceHistory = new Queue<float>();
    private Queue<Vector2> zoomCenterHistory = new Queue<Vector2>();

    void Start()
    {
        if (cameraController == null) { this.enabled = false; Debug.LogError("InputManager: CameraController not assigned!"); }
        if (eventSystem == null) { eventSystem = EventSystem.current; }
        if (eventSystem == null) { this.enabled = false; Debug.LogError("InputManager: EventSystem not found!"); }
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
        if (Input.mousePresent && Input.GetMouseButton(0) && eventSystem.IsPointerOverGameObject())
        {
            pointerOverUI = true;
        }
#endif

        if (pointerOverUI)
        {
            ResetGestureStates();
            return;
        }

        int currentTouchCount = GetTouchOrMouseCount();
        if (currentTouchCount == 0)
        {
            ResetGestureStates();
            return;
        }

        if (currentTouchCount == zoomTouchCount)
        {
            HandleZoomGesture();
        }
        else if (!isZooming && currentTouchCount >= minPanTouchCount)
        {
            HandlePanGesture();
        }
        else if (!isZooming && !isPanning && currentTouchCount == orbitTouchCount)
        {
            HandleOrbitGesture();
        }
        else
        {
            if (isPanning || isOrbiting || isZooming)
            {
                if ((isZooming && currentTouchCount != zoomTouchCount) ||
                    (isPanning && currentTouchCount < minPanTouchCount) ||
                    (isOrbiting && currentTouchCount != orbitTouchCount))
                {
                    ResetGestureStates();
                }
            }
        }
    }

    private int GetTouchOrMouseCount()
    {
#if UNITY_EDITOR
        if (Input.touchCount == 0 && Input.mousePresent)
        {
            if (Input.GetMouseButton(0)) return 1;
            return 0;
        }
#endif
        return Input.touchCount;
    }

    private void ResetGestureStates()
    {
        if (isPanning && cameraController != null)
        {
            cameraController.EndPan();
        }
        isPanning = false;
        isOrbiting = false;
        isZooming = false;

        orbitPositionHistory.Clear();
        panCentroidHistory.Clear();
        zoomDistanceHistory.Clear();
        zoomCenterHistory.Clear();
    }

    private void HandleOrbitGesture()
    {
        if (cameraController == null) return;

#if UNITY_EDITOR
        if (GetTouchOrMouseCount() == 1 && Input.touchCount == 0)
        {
            Vector2 currentRawMousePosition = (Vector2)Input.mousePosition;

            if (Input.GetMouseButtonDown(0))
            {
                isOrbiting = true;
                orbitPositionHistory.Clear();
                previousOrbitPosition = currentRawMousePosition;
            }
            if (Input.GetMouseButton(0) && isOrbiting)
            {
                Vector2 smoothedPosition = GetSmoothedVector2(currentRawMousePosition, orbitPositionHistory);
                Vector2 orbitDelta = smoothedPosition - previousOrbitPosition;
                cameraController.ProcessOrbit(orbitDelta);
                previousOrbitPosition = smoothedPosition;
            }
            if (Input.GetMouseButtonUp(0))
            {
                isOrbiting = false;
            }
            return;
        }
#endif

        Touch touch = Input.GetTouch(0);
        Vector2 currentRawTouchPosition = touch.position;

        if (touch.phase == TouchPhase.Began)
        {
            isOrbiting = true;
            orbitPositionHistory.Clear();
            previousOrbitPosition = currentRawTouchPosition;
        }
        else if (touch.phase == TouchPhase.Moved && isOrbiting)
        {
            Vector2 smoothedPosition = GetSmoothedVector2(currentRawTouchPosition, orbitPositionHistory);
            Vector2 orbitDelta = smoothedPosition - previousOrbitPosition;
            cameraController.ProcessOrbit(orbitDelta);
            previousOrbitPosition = smoothedPosition;
        }
        else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
        {
            isOrbiting = false;
        }
    }

    private void HandlePanGesture()
    {
        if (cameraController == null || GetTouchOrMouseCount() < minPanTouchCount)
        {
            isPanning = false;
            return;
        }

        Dictionary<int, Vector2> currentActiveTouches = new Dictionary<int, Vector2>();
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled)
            {
                currentActiveTouches[touch.fingerId] = touch.position;
            }
        }

        if (currentActiveTouches.Count < minPanTouchCount)
        {
            isPanning = false;
            return;
        }

        Vector2 rawCentroid = GetCentroid(currentActiveTouches.Values.ToList());
        Vector2 smoothedCentroid = GetSmoothedVector2(rawCentroid, panCentroidHistory);

        if (!isPanning)
        {
            isPanning = true;
            previousPanCentroid = smoothedCentroid;
        }
        else if (isPanning && (smoothedCentroid - previousPanCentroid).sqrMagnitude > 0.001f)
        {
            cameraController.ProcessPanDelta(smoothedCentroid - previousPanCentroid);
        }
        previousPanCentroid = smoothedCentroid;

        if (Input.touchCount < minPanTouchCount)
        {
            isPanning = false;
        }
    }

    private void HandleZoomGesture()
    {
        if (cameraController == null || GetTouchOrMouseCount() != zoomTouchCount)
        {
            isZooming = false;
            return;
        }

        Touch touchZero = Input.GetTouch(0);
        Touch touchOne = Input.GetTouch(1);

        Vector2 rawPinchCenter = (touchZero.position + touchOne.position) / 2f;
        float rawPinchDistance = Vector2.Distance(touchZero.position, touchOne.position);

        Vector2 smoothedPinchCenter = GetSmoothedVector2(rawPinchCenter, zoomCenterHistory);
        float smoothedPinchDistance = GetSmoothedFloat(rawPinchDistance, zoomDistanceHistory);

        if (touchZero.phase == TouchPhase.Began || touchOne.phase == TouchPhase.Began)
        {
            isZooming = true;
            zoomDistanceHistory.Clear();
            zoomCenterHistory.Clear();
            previousZoomDistance = smoothedPinchDistance;
            previousZoomCenter = smoothedPinchCenter;
        }
        else if ((touchZero.phase == TouchPhase.Moved || touchOne.phase == TouchPhase.Moved) && isZooming)
        {
            float deltaDistance = smoothedPinchDistance - previousZoomDistance;
            cameraController.ProcessZoom(deltaDistance, smoothedPinchCenter);
            previousZoomDistance = smoothedPinchDistance;
            previousZoomCenter = smoothedPinchCenter;
        }

        if (touchZero.phase == TouchPhase.Ended || touchOne.phase == TouchPhase.Ended || Input.touchCount != zoomTouchCount)
        {
            isZooming = false;
        }
    }

    private Vector2 GetSmoothedVector2(Vector2 newSample, Queue<Vector2> historyQueue)
    {
        historyQueue.Enqueue(newSample);
        while (historyQueue.Count > smoothSamplesCount)
        {
            historyQueue.Dequeue();
        }

        Vector2 sum = Vector2.zero;
        foreach (Vector2 sample in historyQueue)
        {
            sum += sample;
        }
        return sum / Mathf.Max(1, historyQueue.Count);
    }

    private float GetSmoothedFloat(float newSample, Queue<float> historyQueue)
    {
        historyQueue.Enqueue(newSample);
        while (historyQueue.Count > smoothSamplesCount)
        {
            historyQueue.Dequeue();
        }

        float sum = 0f;
        foreach (float sample in historyQueue)
        {
            sum += sample;
        }
        return sum / Mathf.Max(1, historyQueue.Count);
    }

    private Vector2 GetCentroid(List<Vector2> points)
    {
        if (points == null || points.Count == 0) return Vector2.zero;
        Vector2 sum = Vector2.zero;
        foreach (Vector2 p in points) sum += p;
        return sum / points.Count;
    }
}