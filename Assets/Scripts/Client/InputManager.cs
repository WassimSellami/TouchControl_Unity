using UnityEngine;
using System.Collections.Generic;
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
    private int rotateTouchCount = 5;
    private int smoothSamplesCount = 5;

    [Header("Cut Gesture")]
    [SerializeField] private float longPressThreshold = 0.5f;
    [SerializeField] private float maxHoldMovementPixels = 500f;

    [Header("Double Tap Gesture")]
    [SerializeField] private float doubleTapTimeThreshold = 0.3f;

    private bool isHolding = false;
    private bool longPressAchieved = false;
    private bool isCutActive = false;
    private float longPressTimer = 0f;
    private Vector2 startPressPosition;
    private GameObject potentialInteractionTarget = null;

    private Vector2 previousOrbitPosition;
    private Vector2 previousPanCentroid;
    private float previousZoomDistance;
    private float previousRotationAngle;
    private bool isPanning = false;
    private bool isOrbiting = false;
    private bool isZooming = false;
    private bool isRotating = false;

    private float lastTapTime = -1f;
    private int tapCount = 0;
    private Vector2 firstTapPosition;

    private Queue<Vector2> orbitPositionHistory = new Queue<Vector2>();
    private Queue<Vector2> panCentroidHistory = new Queue<Vector2>();
    private Queue<float> zoomDistanceHistory = new Queue<float>();
    private Queue<float> rotationAngleHistory = new Queue<float>();

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

        if (currentTouchCount == rotateTouchCount) HandleRotateGesture();
        else if (currentTouchCount == zoomTouchCount) HandleZoomGesture();
        else if (!isZooming && !isRotating && currentTouchCount >= minPanTouchCount) HandlePanGesture();
        else if (currentTouchCount == orbitTouchCount) HandleSingleTouchInput();
        else
        {
            if (isPanning || isOrbiting || isZooming || isCutActive || isRotating)
            {
                if ((isRotating && currentTouchCount != rotateTouchCount) ||
                    (isZooming && currentTouchCount != zoomTouchCount) ||
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
                if (eventSystem.IsPointerOverGameObject(Input.GetTouch(i).fingerId)) return true;
            }
        }
        if (Input.mousePresent && eventSystem.IsPointerOverGameObject()) return true;
        return false;
    }

    private void HandleSingleTouchInput()
    {
        Vector2 currentRawPosition = (GetTouchOrMouseCount() > 0 && Input.touchCount > 0) ? Input.GetTouch(0).position : (Vector2)Input.mousePosition;
        TouchPhase phase = (GetTouchOrMouseCount() > 0 && Input.touchCount > 0) ? Input.GetTouch(0).phase : GetMousePhase();

        if (phase == TouchPhase.Began)
        {
            if (IsPointerOverUI()) return;

            isPanning = false; isOrbiting = false; isZooming = false; isHolding = true;
            isCutActive = false; longPressAchieved = false; longPressTimer = 0f;

            startPressPosition = currentRawPosition;
            previousOrbitPosition = currentRawPosition;

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
                    if (cuttingPlaneManager != null) cuttingPlaneManager.StartCutDrag(startPressPosition);
                }
            }
        }

        if (phase == TouchPhase.Moved)
        {
            if (isCutActive) cuttingPlaneManager.UpdateCutDrag(currentRawPosition);
            else if (isOrbiting) HandleOrbitGestureInternal(currentRawPosition, phase);
        }

        if (phase == TouchPhase.Ended || phase == TouchPhase.Canceled)
        {
            if (isHolding && !longPressAchieved && !isOrbiting)
            {
                HandleTap(startPressPosition);
            }
            else if (isHolding && longPressAchieved && potentialInteractionTarget != null)
            {
                cuttingPlaneManager.DestroyModelPart(potentialInteractionTarget);
            }
            else if (isCutActive) cuttingPlaneManager.EndCutGesture(currentRawPosition);
            else if (isOrbiting) HandleOrbitGestureInternal(currentRawPosition, phase);

            ResetGestureStates();
        }
    }

    private void HandleTap(Vector2 tapPosition)
    {
        if (Time.time > lastTapTime + doubleTapTimeThreshold)
        {
            tapCount = 0;
        }

        tapCount++;
        lastTapTime = Time.time;

        if (tapCount == 1)
        {
            firstTapPosition = tapPosition;
        }
        else if (tapCount >= 2)
        {
            if (targetModelController != null)
            {
                // REVERSED LOGIC: Left tap rotates right (1f), right tap rotates left (-1f)
                float direction = (firstTapPosition.x < Screen.width / 2f) ? 1f : -1f;
                targetModelController.TriggerPresetViewRotation(direction);
            }
            tapCount = 0;
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
        else if (phase == TouchPhase.Ended || phase == TouchPhase.Canceled) isOrbiting = false;
    }

    private int GetTouchOrMouseCount()
    {
        if (Input.touchCount > 0) return Input.touchCount;
        if (Input.mousePresent && Input.GetMouseButton(0)) return 1;
        return 0;
    }

    private void ResetGestureStates()
    {
        isPanning = false; isOrbiting = false; isZooming = false; isHolding = false;
        isCutActive = false; longPressAchieved = false; longPressTimer = 0f;
        isRotating = false;
        potentialInteractionTarget = null;

        orbitPositionHistory.Clear(); panCentroidHistory.Clear(); zoomDistanceHistory.Clear();
        rotationAngleHistory.Clear();

        if (targetModelController != null) targetModelController.ResetOrbitLock();
    }

    private void HandlePanGesture()
    {
        if (targetModelController == null) { isPanning = false; return; }
        List<Vector2> activeTouches = GetActiveTouchPositions();
        if (activeTouches.Count < minPanTouchCount) { isPanning = false; return; }

        Vector2 smoothedCentroid = GetSmoothedVector2(GetCentroid(activeTouches), panCentroidHistory);
        if (!isPanning)
        {
            isPanning = true;
            panCentroidHistory.Clear();
            previousPanCentroid = smoothedCentroid;
        }
        else
        {
            Vector2 panDelta = previousPanCentroid - smoothedCentroid;
            if (panDelta.sqrMagnitude > 0.001f) targetModelController.ProcessPan(panDelta);
            previousPanCentroid = smoothedCentroid;
        }
    }

    private void HandleZoomGesture()
    {
        if (targetModelController == null || GetTouchOrMouseCount() != zoomTouchCount) { isZooming = false; return; }

        Touch touchZero = Input.GetTouch(0);
        Touch touchOne = Input.GetTouch(1);
        float smoothedPinchDistance = GetSmoothedFloat(Vector2.Distance(touchZero.position, touchOne.position), zoomDistanceHistory);

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

    private void HandleRotateGesture()
    {
        if (targetModelController == null || GetTouchOrMouseCount() != rotateTouchCount) { isRotating = false; return; }

        List<Vector2> activeTouches = GetActiveTouchPositions();
        if (activeTouches.Count < rotateTouchCount) { isRotating = false; return; }

        Vector2 centroid = GetCentroid(activeTouches);
        Vector2 referenceVector = activeTouches[0] - centroid;
        float currentAngle = Mathf.Atan2(referenceVector.y, referenceVector.x) * Mathf.Rad2Deg;
        float smoothedAngle = GetSmoothedFloat(currentAngle, rotationAngleHistory);

        bool isNewGesture = false;
        for (int i = 0; i < rotateTouchCount; i++)
        {
            if (Input.GetTouch(i).phase == TouchPhase.Began)
            {
                isNewGesture = true;
                break;
            }
        }

        if (!isRotating || isNewGesture)
        {
            isRotating = true;
            rotationAngleHistory.Clear();
            previousRotationAngle = smoothedAngle;
        }
        else
        {
            float angleDelta = Mathf.DeltaAngle(previousRotationAngle, smoothedAngle);
            if (Mathf.Abs(angleDelta) > 0.01f)
            {
                targetModelController.ProcessRoll(angleDelta);
            }
            previousRotationAngle = smoothedAngle;
        }
    }

    private List<Vector2> GetActiveTouchPositions()
    {
        List<Vector2> positions = new List<Vector2>();
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled) positions.Add(touch.position);
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