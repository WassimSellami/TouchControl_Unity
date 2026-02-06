using UnityEngine;
using System.Collections.Generic;
using UnityEngine.EventSystems;

public class InputManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private ModelViewportController targetModelController;
    [SerializeField]
    private EventSystem eventSystem;
    [SerializeField]
    private CuttingPlaneManager cuttingPlaneManager;

    private bool isHolding = false;
    private bool longPressAchieved = false;
    private bool isCutActive = false;
    private float longPressTimer = 0f;
    private Vector2 startPressPosition;
    private GameObject potentialInteractionTarget = null;
    private bool isShakingSent = false;

    private Vector2 singleTouchVelocity;
    private Vector2 previousOrbitPosition;
    private Vector2 previousPanCentroid;
    private float previousZoomDistance;
    private float previousRotationAngle;
    private bool isPanning = false;
    private bool isOrbiting = false;
    private bool isZooming = false;
    private bool isRotating = false;
    private bool isEvaluatingTwoFingerGesture = false;

    private float lastTapTime = -1f;
    private int tapCount = 0;
    private Vector2 firstTapPosition;

    private Vector2 twoFingerStartCentroid;
    private float twoFingerStartDistance;

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

        if (currentTouchCount == Constants.ROTATE_TOUCH_COUNT) HandleRotateGesture();
        else if (currentTouchCount == Constants.ZOOM_TOUCH_COUNT) HandleTwoFingerGesture();
        else if (!isZooming && !isRotating && currentTouchCount >= Constants.MIN_PAN_TOUCH_COUNT) HandlePanGesture();
        else if (currentTouchCount == Constants.ORBIT_TOUCH_COUNT) HandleSingleTouchInput();
        else
        {
            if (isPanning || isOrbiting || isZooming || isCutActive || isRotating)
            {
                if ((isRotating && currentTouchCount != Constants.ROTATE_TOUCH_COUNT) ||
                    (isZooming && currentTouchCount != Constants.ZOOM_TOUCH_COUNT) ||
                    (isPanning && currentTouchCount < Constants.MIN_PAN_TOUCH_COUNT) ||
                    ((isOrbiting || isCutActive) && currentTouchCount != Constants.ORBIT_TOUCH_COUNT))
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
            isShakingSent = false;
            startPressPosition = currentRawPosition;
            previousOrbitPosition = currentRawPosition;
            singleTouchVelocity = Vector2.zero;
            if (cuttingPlaneManager != null) potentialInteractionTarget = cuttingPlaneManager.GetModelPartAtScreenPoint(currentRawPosition);
        }

        if (isHolding)
        {
            longPressTimer += Time.deltaTime;

            if (!longPressAchieved)
            {
                if (longPressTimer >= Constants.LONG_PRESS_THRESHOLD)
                {
                    longPressAchieved = true;
                    if (cuttingPlaneManager != null)
                    {
                        if (potentialInteractionTarget != null)
                        {
                            cuttingPlaneManager.StartShake(potentialInteractionTarget, startPressPosition);
                            isShakingSent = true;
                        }
                        else
                        {
                            cuttingPlaneManager.ShowSliceIconAtPosition(startPressPosition);
                        }
                    }
                }
                else if (Vector2.Distance(startPressPosition, currentRawPosition) > Constants.MAX_HOLD_MOVEMENT_PIXELS)
                {
                    isHolding = false;
                    isOrbiting = true;
                    HandleOrbitGestureInternal(currentRawPosition, TouchPhase.Began);
                }
            }
            else
            {
                if (potentialInteractionTarget == null && !isCutActive && Vector2.Distance(startPressPosition, currentRawPosition) > Constants.MAX_HOLD_MOVEMENT_PIXELS)
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
            else if (isOrbiting)
            {
                Vector2 frameVelocity = (previousOrbitPosition - currentRawPosition) / Time.deltaTime;

                if (frameVelocity.sqrMagnitude > 0.1f)
                {
                    singleTouchVelocity = Vector2.Lerp(singleTouchVelocity, frameVelocity, 0.5f);
                }

                HandleOrbitGestureInternal(currentRawPosition, phase);
            }
        }

        if (phase == TouchPhase.Ended || phase == TouchPhase.Canceled)
        {
            if (isHolding && !longPressAchieved && !isOrbiting)
            {
                if (targetModelController != null && targetModelController.IsAutoRotating)
                {
                    targetModelController.StopContinuousRotation();
                    tapCount = 0;
                }
                else
                {
                    HandleTap(startPressPosition);
                }
            }
            else if (isOrbiting)
            {
                if (Mathf.Abs(singleTouchVelocity.x) > Constants.FLICK_THRESHOLD && Mathf.Abs(singleTouchVelocity.x) > Mathf.Abs(singleTouchVelocity.y))
                {
                    targetModelController.StartContinuousRotation(Mathf.Sign(singleTouchVelocity.x));
                }
                HandleOrbitGestureInternal(currentRawPosition, phase);
            }
            else if (isHolding && longPressAchieved && potentialInteractionTarget != null)
            {
                cuttingPlaneManager.DestroyModelPart(potentialInteractionTarget);
            }
            else if (isCutActive)
            {
                cuttingPlaneManager.EndCutGesture(currentRawPosition);
            }
            ResetGestureStates();
        }
    }

    private void HandleTwoFingerGesture()
    {
        if (targetModelController == null) return;

        Touch touchZero = Input.GetTouch(0);
        Touch touchOne = Input.GetTouch(1);

        if (touchZero.phase == TouchPhase.Began || touchOne.phase == TouchPhase.Began)
        {
            isEvaluatingTwoFingerGesture = true;
            twoFingerStartDistance = Vector2.Distance(touchZero.position, touchOne.position);
            twoFingerStartCentroid = (touchZero.position + touchOne.position) / 2f;
        }

        if ((touchZero.phase == TouchPhase.Moved || touchOne.phase == TouchPhase.Moved) && isEvaluatingTwoFingerGesture)
        {
            float currentDistance = Vector2.Distance(touchZero.position, touchOne.position);
            float pinchAmount = Mathf.Abs(currentDistance - twoFingerStartDistance);

            if (pinchAmount > Constants.PINCH_REGISTER_THRESHOLD)
            {
                isEvaluatingTwoFingerGesture = false;
                isZooming = true;
                zoomDistanceHistory.Clear();
                previousZoomDistance = GetSmoothedFloat(currentDistance, zoomDistanceHistory);
            }
        }

        if (isZooming)
        {
            HandleZoomGestureInternal();
        }
    }

    private void ResetGestureStates()
    {
        if (isShakingSent && potentialInteractionTarget != null && cuttingPlaneManager != null)
        {
            cuttingPlaneManager.StopShake(potentialInteractionTarget);
        }
        if (cuttingPlaneManager != null)
        {
            cuttingPlaneManager.HideSliceIcon();
        }

        isShakingSent = false;
        isPanning = false; isOrbiting = false; isZooming = false; isHolding = false;
        isCutActive = false; longPressAchieved = false; longPressTimer = 0f;
        isRotating = false; isEvaluatingTwoFingerGesture = false;
        potentialInteractionTarget = null;
        singleTouchVelocity = Vector2.zero;
        orbitPositionHistory.Clear(); panCentroidHistory.Clear(); zoomDistanceHistory.Clear();
        rotationAngleHistory.Clear();
        if (targetModelController != null) targetModelController.ResetOrbitLock();
    }

    private void HandleTap(Vector2 tapPosition)
    {
        if (Time.time > lastTapTime + Constants.DOUBLE_TAP_TIME_THRESHOLD) tapCount = 0;
        tapCount++;
        lastTapTime = Time.time;

        if (tapCount == 1) firstTapPosition = tapPosition;
        else if (tapCount >= 2)
        {
            if (targetModelController != null)
            {
                float direction = (firstTapPosition.x < Screen.width / 2f) ? -1f : 1f;
                targetModelController.TriggerPresetViewRotation(direction);
            }
            tapCount = 0;
        }
    }

    private void HandleZoomGestureInternal()
    {
        float smoothedPinchDistance = GetSmoothedFloat(Vector2.Distance(Input.GetTouch(0).position, Input.GetTouch(1).position), zoomDistanceHistory);
        float deltaDistance = smoothedPinchDistance - previousZoomDistance;
        targetModelController.ProcessZoom(deltaDistance);
        previousZoomDistance = smoothedPinchDistance;
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

    private void HandlePanGesture()
    {
        if (targetModelController == null) { isPanning = false; return; }
        List<Vector2> activeTouches = GetActiveTouchPositions();
        if (activeTouches.Count < Constants.MIN_PAN_TOUCH_COUNT) { isPanning = false; return; }

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

    private void HandleRotateGesture()
    {
        if (targetModelController == null || GetTouchOrMouseCount() != Constants.ROTATE_TOUCH_COUNT) { isRotating = false; return; }
        List<Vector2> activeTouches = GetActiveTouchPositions();
        if (activeTouches.Count < Constants.ROTATE_TOUCH_COUNT) { isRotating = false; return; }

        Vector2 centroid = GetCentroid(activeTouches);
        Vector2 referenceVector = activeTouches[0] - centroid;
        float currentAngle = Mathf.Atan2(referenceVector.y, referenceVector.x) * Mathf.Rad2Deg;
        float smoothedAngle = GetSmoothedFloat(currentAngle, rotationAngleHistory);
        bool isNewGesture = false;
        for (int i = 0; i < Constants.ROTATE_TOUCH_COUNT; i++) if (Input.GetTouch(i).phase == TouchPhase.Began) { isNewGesture = true; break; }

        if (!isRotating || isNewGesture)
        {
            isRotating = true;
            rotationAngleHistory.Clear();
            previousRotationAngle = smoothedAngle;
        }
        else
        {
            float angleDelta = Mathf.DeltaAngle(previousRotationAngle, smoothedAngle);
            if (Mathf.Abs(angleDelta) > 0.01f) targetModelController.ProcessRoll(angleDelta);
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
        while (historyQueue.Count > Constants.SMOOTH_SAMPLES_COUNT) { historyQueue.Dequeue(); }
        Vector2 sum = Vector2.zero;
        foreach (Vector2 sample in historyQueue) { sum += sample; }
        return sum / Mathf.Max(1, historyQueue.Count);
    }

    private float GetSmoothedFloat(float newSample, Queue<float> historyQueue)
    {
        historyQueue.Enqueue(newSample);
        while (historyQueue.Count > Constants.SMOOTH_SAMPLES_COUNT) { historyQueue.Dequeue(); }
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