// Assets/Scripts/CubeController.cs
using UnityEngine;

public class CubeController : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private WebSocketServerManager2 _webSocketManager; // Assign in Inspector

    [Header("Cube Control Settings")]
    [SerializeField] private float _rotationSpeed = 50f; // Degrees per command
    [SerializeField] private float _zoomSpeed = 0.5f;   // Units per command
    [SerializeField] private float _panSpeed = 0.2f;    // Units per command

    [Header("Camera Zoom Limits")]
    [SerializeField] private float _minCameraZoomZ = -10f; // Closer to cube
    [SerializeField] private float _maxCameraZoomZ = -50f; // Further from cube

    [Header("Camera Pan Limits (World Space)")]
    [SerializeField] private float _minPanX = -5f;
    [SerializeField] private float _maxPanX = 5f;
    [SerializeField] private float _minPanY = -5f;
    [SerializeField] private float _maxPanY = 5f;

    private Transform _cameraTransform; // Cache Camera.main.transform

    void Awake()
    {
        // Ensure Main Camera is tagged correctly or get it differently if needed
        if (Camera.main != null)
        {
            _cameraTransform = Camera.main.transform;
        }
        else
        {
            Debug.LogError("No Main Camera found! Please ensure your camera is tagged 'MainCamera'.");
        }
    }

    void OnEnable()
    {
        if (_webSocketManager != null)
        {
            // Subscribe to the command received event
            _webSocketManager.OnCommandReceived += HandleCommand;
            Debug.Log("[CubeController] Subscribed to WebSocket commands.");
        }
        else
        {
            Debug.LogError("[CubeController] WebSocketServerManager reference not set! Please assign it in the Inspector.");
        }
    }

    void OnDisable()
    {
        if (_webSocketManager != null)
        {
            // Unsubscribe to prevent memory leaks or errors
            _webSocketManager.OnCommandReceived -= HandleCommand;
            Debug.Log("[CubeController] Unsubscribed from WebSocket commands.");
        }
    }

    private void HandleCommand(string command)
    {
        string trimmedCommand = command.Trim().ToLower(); // Normalize command

        switch (trimmedCommand)
        {
            case "rotate+":
                RotateCube(1);
                break;
            case "rotate-":
                RotateCube(-1);
                break;
            case "zoom+":
                ZoomCamera(1);
                break;
            case "zoom-":
                ZoomCamera(-1);
                break;
            case "pan+": // Interpret as pan right
                PanCamera(1, 0);
                break;
            case "pan-": // Interpret as pan left
                PanCamera(-1, 0);
                break;
            // You could add panUp/panDown here if needed
            // case "panup+": PanCamera(0, 1); break;
            // case "pandown-": PanCamera(0, -1); break;
            default:
                Debug.LogWarning($"[CubeController] Unknown command received: {command}");
                break;
        }
    }

    private void RotateCube(float direction)
    {
        // Rotate around the Y-axis (up direction)
        transform.Rotate(Vector3.up, direction * _rotationSpeed, Space.Self);
        Debug.Log($"[CubeController] Rotated cube: {direction}");
    }

    private void ZoomCamera(float direction)
    {
        if (_cameraTransform == null) return;

        // Zoom by moving the camera along its local Z-axis (forward/back)
        Vector3 newPosition = _cameraTransform.position + _cameraTransform.forward * direction * _zoomSpeed;

        // Clamp the Z position to keep it within sensible limits
        // Note: For perspective, moving camera's Z towards cube (negative Z world) means zooming IN.
        // So _minCameraZoomZ (e.g., -10) is closer to the cube (more zoomed in)
        // and _maxCameraZoomZ (e.g., -50) is further from the cube (more zoomed out).
        newPosition.z = Mathf.Clamp(newPosition.z, _maxCameraZoomZ, _minCameraZoomZ); // Clamp order matters!
        _cameraTransform.position = newPosition;
        Debug.Log($"[CubeController] Zoomed camera. New Z: {_cameraTransform.position.z}");
    }

    private void PanCamera(float xDirection, float yDirection)
    {
        if (_cameraTransform == null) return;

        // Pan by moving the camera along its local X and Y axes (right/up)
        Vector3 panMovement = (_cameraTransform.right * xDirection + _cameraTransform.up * yDirection) * _panSpeed;
        Vector3 newPosition = _cameraTransform.position + panMovement;

        // Clamp pan positions to stay within bounds
        newPosition.x = Mathf.Clamp(newPosition.x, _minPanX, _maxPanX);
        newPosition.y = Mathf.Clamp(newPosition.y, _minPanY, _maxPanY);

        _cameraTransform.position = newPosition;
        Debug.Log($"[CubeController] Panned camera. New Pos: {_cameraTransform.position}");
    }
}