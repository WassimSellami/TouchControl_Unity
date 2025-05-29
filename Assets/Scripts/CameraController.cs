using UnityEngine;
using UnityEngine.EventSystems;

public class CameraController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform orbitTarget;
    [SerializeField] private Camera mainCamera;

    [Header("Settings")]
    [SerializeField] private float orbitSensitivity = 0.1f;
    [SerializeField] private float panSensitivity = 0.003f;
    [SerializeField] private float zoomSensitivity = 0.0025f;
    [SerializeField] private float zoomMin = 0.5f;
    [SerializeField] private float zoomMax = 50.0f;
    [SerializeField] private float presetViewRotationStep = 90.0f;
    [SerializeField] private float orbitDeadZone = 0.1f;

    private Vector3 panStartWorldPosAtScreenDepth;
    private bool isPanningState = false;

    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private float initialOrthoSize;

    void Start()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null) { this.enabled = false; return; }
        }

        initialPosition = transform.position;
        initialRotation = transform.rotation;

        if (mainCamera.orthographic)
        {
            initialOrthoSize = mainCamera.orthographicSize;
        }

        if (orbitTarget == null)
        {
            Debug.LogError("CameraController: OrbitTarget not assigned! Orbit and Preset views may not function correctly.");
        }
    }

    public void StartPan(Vector2 screenPos)
    {
        float depth = (orbitTarget != null) ? mainCamera.WorldToScreenPoint(orbitTarget.position).z : mainCamera.nearClipPlane + 1f;
        panStartWorldPosAtScreenDepth = mainCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, depth));
        isPanningState = true;
    }

    public void ProcessPan(Vector2 screenPos)
    {
        if (!isPanningState || mainCamera == null) return;
        float depth = (orbitTarget != null) ? mainCamera.WorldToScreenPoint(orbitTarget.position).z : mainCamera.nearClipPlane + 1f;
        Vector3 currentMouseWorldPos = mainCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, depth));
        Vector3 panDifference = panStartWorldPosAtScreenDepth - currentMouseWorldPos;
        transform.position += panDifference;
    }

    public void ProcessPanDelta(Vector2 screenDelta)
    {
        if (mainCamera == null) return;
        Vector3 right = mainCamera.transform.right * -screenDelta.x;
        Vector3 up = mainCamera.transform.up * -screenDelta.y;
        Vector3 worldDelta = (right + up) * panSensitivity;
        transform.Translate(worldDelta, Space.World);
    }

    public void EndPan()
    {
        isPanningState = false;
    }

    public void ProcessOrbit(Vector2 screenDelta)
    {
        if (orbitTarget == null) return;

        float absDeltaX = Mathf.Abs(screenDelta.x);

        if (absDeltaX > orbitDeadZone)
        {
            float horizontalInput = screenDelta.x * orbitSensitivity;
            transform.RotateAround(orbitTarget.position, Vector3.up, horizontalInput);
        }
    }

    public void ProcessZoom(float zoomAmount, Vector2 zoomCenterScreenPos)
    {
        if (mainCamera == null || !mainCamera.orthographic || zoomAmount == 0) return;

        Vector3 worldZoomCenterScreenDepth = new Vector3(zoomCenterScreenPos.x, zoomCenterScreenPos.y, (orbitTarget != null) ? mainCamera.WorldToScreenPoint(orbitTarget.position).z : mainCamera.nearClipPlane + 10f);
        Vector3 worldZoomCenterBefore = mainCamera.ScreenToWorldPoint(worldZoomCenterScreenDepth);

        float currentOrthoSize = mainCamera.orthographicSize;
        float newOrthoSize = Mathf.Clamp(currentOrthoSize - zoomAmount * zoomSensitivity, zoomMin, zoomMax);

        if (Mathf.Approximately(newOrthoSize, currentOrthoSize)) return;

        mainCamera.orthographicSize = newOrthoSize;

        Vector3 worldZoomCenterAfter = mainCamera.ScreenToWorldPoint(worldZoomCenterScreenDepth);
        Vector3 cameraPositionAdjustment = worldZoomCenterBefore - worldZoomCenterAfter;
        transform.position += cameraPositionAdjustment;
    }

    public void ResetView()
    {
        transform.position = initialPosition;
        transform.rotation = initialRotation;
        if (mainCamera != null && mainCamera.orthographic)
        {
            mainCamera.orthographicSize = initialOrthoSize;
        }
    }

    public void CyclePresetView()
    {
        if (orbitTarget == null || mainCamera == null)
        {
            Debug.LogError("Cannot cycle preset view: OrbitTarget or MainCamera not set correctly.");
            return;
        }
        transform.RotateAround(orbitTarget.position, Vector3.up, presetViewRotationStep);
    }
}