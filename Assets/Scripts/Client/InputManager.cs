using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.EventSystems;

public class InputManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private MockedModelController targetModelController;
    [SerializeField]
    private EventSystem eventSystem;

    private int orbitTouchCount = 1;
    private int zoomTouchCount = 2;
    private int minPanTouchCount = 3;

    private int smoothSamplesCount = 5;

    private Vector2 previousOrbitPosition;
    private Vector2 previousPanCentroid;
    private float previousZoomDistance;

    private bool isPanning = false;
    private bool isOrbiting = false;
    private bool isZooming = false;

    private Queue<Vector2> orbitPositionHistory = new Queue<Vector2>();
    private Queue<Vector2> panCentroidHistory = new Queue<Vector2>();
    private Queue<float> zoomDistanceHistory = new Queue<float>();

    void Start()
    {
        if (targetModelController == null) { this.enabled = false; Debug.LogError("InputManager: TargetModelController not assigned!"); }
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
        isPanning = false;
        isOrbiting = false;
        isZooming = false;
        orbitPositionHistory.Clear();
        panCentroidHistory.Clear();
        zoomDistanceHistory.Clear();
    }

    private void HandleOrbitGesture()
    {
        if (targetModelController == null) return;
        Vector2 currentRawPosition = (GetTouchOrMouseCount() > 0 && Input.touchCount > 0) ? Input.GetTouch(0).position : (Vector2)Input.mousePosition;
        TouchPhase phase = (GetTouchOrMouseCount() > 0 && Input.touchCount > 0) ? Input.GetTouch(0).phase : GetMousePhase();

        if (phase == TouchPhase.Began)
        {
            isOrbiting = true;
            orbitPositionHistory.Clear();
            previousOrbitPosition = currentRawPosition;
        }
        else if (phase == TouchPhase.Moved && isOrbiting)
        {
            Vector2 smoothedPosition = GetSmoothedVector2(currentRawPosition, orbitPositionHistory);
            Vector2 orbitDelta = smoothedPosition - previousOrbitPosition;
            targetModelController.ProcessOrbit(orbitDelta);
            previousOrbitPosition = smoothedPosition;
        }
        else if (phase == TouchPhase.Ended || phase == TouchPhase.Canceled)
        {
            isOrbiting = false;
        }
    }

    private void HandlePanGesture()
    {
        if (targetModelController == null || GetTouchOrMouseCount() < minPanTouchCount)
        {
            isPanning = false;
            return;
        }

        Vector2 rawCentroid = GetCentroid(GetActiveTouchPositions());
        Vector2 smoothedCentroid = GetSmoothedVector2(rawCentroid, panCentroidHistory);

        if (!isPanning)
        {
            isPanning = true;
            previousPanCentroid = smoothedCentroid;
        }
        else
        {
            Vector2 panDelta = smoothedCentroid - previousPanCentroid;
            if (panDelta.sqrMagnitude > 0.001f)
            {
                targetModelController.ProcessPan(panDelta);
            }
            previousPanCentroid = smoothedCentroid;
        }
    }

    private void HandleZoomGesture()
    {
        if (targetModelController == null || GetTouchOrMouseCount() != zoomTouchCount)
        {
            isZooming = false;
            return;
        }
        Touch touchZero = Input.GetTouch(0);
        Touch touchOne = Input.GetTouch(1);
        float rawPinchDistance = Vector2.Distance(touchZero.position, touchOne.position);
        float smoothedPinchDistance = GetSmoothedFloat(rawPinchDistance, zoomDistanceHistory);

        if (touchZero.phase == TouchPhase.Began || touchOne.phase == TouchPhase.Began)
        {
            isZooming = true;
            zoomDistanceHistory.Clear();
            previousZoomDistance = smoothedPinchDistance;
        }
        else if ((touchZero.phase == TouchPhase.Moved || touchOne.phase == TouchPhase.Moved) && isZooming)
        {
            float deltaDistance = smoothedPinchDistance - previousZoomDistance;
            targetModelController.ProcessZoom(deltaDistance);
            previousZoomDistance = smoothedPinchDistance;
        }
    }

    private List<Vector2> GetActiveTouchPositions()
    {
        List<Vector2> positions = new List<Vector2>();
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled)
            {
                positions.Add(touch.position);
            }
        }
        return positions;
    }

    private TouchPhase GetMousePhase()
    {
        if (Input.GetMouseButtonDown(0)) return TouchPhase.Began;
        if (Input.GetMouseButton(0)) return TouchPhase.Moved;
        if (Input.GetMouseButtonUp(0)) return TouchPhase.Ended;
        return TouchPhase.Canceled;
    }

    private Vector2 GetSmoothedVector2(Vector2 newSample, Queue<Vector2> historyQueue)
    {
        historyQueue.Enqueue(newSample);
        while (historyQueue.Count > smoothSamplesCount) { historyQueue.Dequeue(); }
        Vector2 sum = Vector2.zero;
        foreach (Vector2 sample in historyQueue) { sum += sample; }
        return sum / Mathf.Max(1, historyQueue.Count);
    }

    private float GetSmoothedFloat(float newSample, Queue<float> historyQueue)
    {
        historyQueue.Enqueue(newSample);
        while (historyQueue.Count > smoothSamplesCount) { historyQueue.Dequeue(); }
        float sum = 0f;
        foreach (float sample in historyQueue) { sum += sample; }
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