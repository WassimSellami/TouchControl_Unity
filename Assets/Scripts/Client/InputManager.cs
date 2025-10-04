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
    [SerializeField]
    private CuttingPlaneManager cuttingPlaneManager;

    [Header("Gesture Thresholds")]
    private int orbitTouchCount = 1;
    private int zoomTouchCount = 2;
    private int minPanTouchCount = 3;
    private int smoothSamplesCount = 5;

    [Header("Cut Gesture")]
    [SerializeField] private float longPressThreshold = 0.5f;
    [SerializeField] private float maxHoldMovementPixels = 500f;

    private bool isHolding = false;
    private bool longPressAchieved = false;
    private bool isCutActive = false;
    private float longPressTimer = 0f;
    private Vector2 startPressPosition;
    private GameObject potentialInteractionTarget = null;

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
        if (targetModelController == null) { this.enabled = false; Debug.LogError("InputManager: TargetModelController not assigned!"); return; }
        if (cuttingPlaneManager == null) { Debug.LogWarning("InputManager: CuttingPlaneManager not assigned. Cutting feature disabled."); }

        if (eventSystem == null) { eventSystem = EventSystem.current; }
        if (eventSystem == null) { this.enabled = false; Debug.LogError("InputManager: EventSystem not found!"); }
    }

    void Update()
    {
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
        else if (currentTouchCount == orbitTouchCount)
        {
            HandleSingleTouchInput();
        }
        else
        {
            if (isPanning || isOrbiting || isZooming || isCutActive)
            {
                if ((isZooming && currentTouchCount != zoomTouchCount) ||
                    (isPanning && currentTouchCount < minPanTouchCount) ||
                    ((isOrbiting || isCutActive) && currentTouchCount != orbitTouchCount))
                {
                    ResetGestureStates();
                }
            }
        }
    }

    private bool IsPointerOverUI()
    {
        if (Input.touchCount > 0)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                if (eventSystem.IsPointerOverGameObject(Input.GetTouch(i).fingerId))
                {
                    return true;
                }
            }
        }

        if (Input.mousePresent)
        {
            if (eventSystem.IsPointerOverGameObject())
            {
                return true;
            }
        }

        return false;
    }

    private void HandleSingleTouchInput()
    {
        Vector2 currentRawPosition = (GetTouchOrMouseCount() > 0 && Input.touchCount > 0) ? Input.GetTouch(0).position : (Vector2)Input.mousePosition;
        TouchPhase phase = (GetTouchOrMouseCount() > 0 && Input.touchCount > 0) ? Input.GetTouch(0).phase : GetMousePhase();

        if (phase == TouchPhase.Began)
        {
            if (IsPointerOverUI())
            {
                return;
            }
            ResetGestureStates();

            startPressPosition = currentRawPosition;
            previousOrbitPosition = currentRawPosition;
            isHolding = true;

            if (cuttingPlaneManager != null)
            {
                potentialInteractionTarget = cuttingPlaneManager.GetModelPartAtScreenPoint(currentRawPosition);
            }
        }

        if (isHolding)
        {
            longPressTimer += Time.deltaTime;

            if (!longPressAchieved)
            {
                if (longPressTimer >= longPressThreshold)
                {
                    longPressAchieved = true;
                }
                else if (Vector2.Distance(startPressPosition, currentRawPosition) > maxHoldMovementPixels)
                {
                    isHolding = false;
                    isOrbiting = true;
                    HandleOrbitGestureInternal(currentRawPosition, TouchPhase.Began);
                }
            }
            else
            {
                if (potentialInteractionTarget == null && !isCutActive && Vector2.Distance(startPressPosition, currentRawPosition) > maxHoldMovementPixels)
                {
                    isHolding = false;
                    isCutActive = true;
                    if (cuttingPlaneManager != null)
                    {
                        cuttingPlaneManager.StartCutDrag(startPressPosition);
                    }
                }
            }
        }

        if (phase == TouchPhase.Moved)
        {
            if (isCutActive)
            {
                cuttingPlaneManager.UpdateCutDrag(currentRawPosition);
            }
            else if (isOrbiting)
            {
                HandleOrbitGestureInternal(currentRawPosition, phase);
            }
        }

        if (phase == TouchPhase.Ended || phase == TouchPhase.Canceled)
        {
            if (isHolding && longPressAchieved && potentialInteractionTarget != null)
            {
                cuttingPlaneManager.DestroyModelPart(potentialInteractionTarget);
            }
            else if (isCutActive)
            {
                cuttingPlaneManager.EndCutGesture(currentRawPosition);
            }
            else if (isOrbiting)
            {
                HandleOrbitGestureInternal(currentRawPosition, phase);
            }
            ResetGestureStates();
        }
    }

    private void HandleOrbitGestureInternal(Vector2 currentRawPosition, TouchPhase phase)
    {
        if (targetModelController == null) return;

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

    private int GetTouchOrMouseCount()
    {
        if (Input.touchCount > 0)
        {
            return Input.touchCount;
        }
        if (Input.mousePresent && Input.GetMouseButton(0))
        {
            return 1;
        }
        return 0;
    }

    private void ResetGestureStates()
    {
        isPanning = false;
        isOrbiting = false;
        isZooming = false;
        isHolding = false;
        isCutActive = false;
        longPressAchieved = false;
        longPressTimer = 0f;
        potentialInteractionTarget = null;

        orbitPositionHistory.Clear();
        panCentroidHistory.Clear();
        zoomDistanceHistory.Clear();

        if (targetModelController != null)
        {
            targetModelController.ResetOrbitLock();
        }
    }

    private void HandlePanGesture()
    {
        if (targetModelController == null)
        {
            isPanning = false;
            return;
        }

        List<Vector2> activeTouches = GetActiveTouchPositions();

        if (activeTouches.Count < minPanTouchCount)
        {
            isPanning = false;
            return;
        }

        Vector2 rawCentroid = GetCentroid(activeTouches);
        Vector2 smoothedCentroid = GetSmoothedVector2(rawCentroid, panCentroidHistory);

        if (!isPanning)
        {
            isPanning = true;
            panCentroidHistory.Clear();
            previousPanCentroid = smoothedCentroid;
        }
        else
        {
            Vector2 panDelta = previousPanCentroid - smoothedCentroid;
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