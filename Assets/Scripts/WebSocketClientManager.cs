using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using WebSocketSharp;
using static JsonUtilityHelper;

public class WebSocketClientManager : MonoBehaviour
{
    [Header("UI Manager Reference")]
    [SerializeField] private UIManager uiManager;
    [SerializeField] private CameraController cameraController;
    [SerializeField] private MockedModelController mockedModelControllerRef;

    [Header("Networking")]
    [SerializeField] private TMP_InputField ipAddressInput;
    [SerializeField] private string defaultIpAddress = "127.0.0.1";
    [SerializeField] private int serverPort = 8080;
    [SerializeField] private string servicePath = "/control";
    [SerializeField] private float cameraUpdateRateFPS = 15f;

    [Header("UI Elements for Connection & Control")]
    [SerializeField] private Button connectButton;
    [SerializeField] private Button loadCubeButton;
    [SerializeField] private Button loadCylinderButton;
    [SerializeField] private Button backButtonFromModelView;
    [SerializeField] private Button toggleRotationGizmoButton_UI;
    [SerializeField] private Button toggleCropBoxButton_UI;
    [SerializeField] private TMP_Text statusText;

    private WebSocket ws;
    private bool isAttemptingConnection = false;
    private float timeSinceLastCameraUpdate = 0f;
    private float cameraUpdateInterval;
    private Camera clientActualCamera;

    public bool IsConnected => ws != null && ws.ReadyState == WebSocketState.Open;

    void Start()
    {
        if (uiManager == null) Debug.LogError("[Client WSManager] UIManager not assigned!");
        if (cameraController == null) Debug.LogError("[Client WSManager] CameraController (CameraRig) not assigned!");
        else
        {
            clientActualCamera = cameraController.GetComponentInChildren<Camera>();
            if (clientActualCamera == null) clientActualCamera = cameraController.GetComponent<Camera>();
            if (clientActualCamera == null) Debug.LogError("[Client WSManager] Actual Camera component not found on or under CameraController (CameraRig). OrthoSize updates will fail.");
        }
        if (mockedModelControllerRef == null) Debug.LogError("[Client WSManager] MockedModelControllerRef not assigned!");

        if (ipAddressInput == null) Debug.LogError("[Client WSManager] IP Address InputField not assigned!");
        if (connectButton == null) Debug.LogError("[Client WSManager] Connect Button not assigned!");
        if (statusText == null) Debug.LogError("[Client WSManager] Status Text not assigned!");

        if (ipAddressInput != null) ipAddressInput.text = defaultIpAddress;
        if (connectButton != null) connectButton.onClick.AddListener(AttemptConnect);

        if (loadCubeButton != null) loadCubeButton.onClick.AddListener(() => OnLoadModelSelected("CUBE"));
        if (loadCylinderButton != null) loadCylinderButton.onClick.AddListener(() => OnLoadModelSelected("CYLINDER"));
        if (backButtonFromModelView != null) backButtonFromModelView.onClick.AddListener(OnBackToMainMenuPressed);
        if (toggleRotationGizmoButton_UI != null) toggleRotationGizmoButton_UI.onClick.AddListener(OnToggleRotationGizmoPressed);
        if (toggleCropBoxButton_UI != null) toggleCropBoxButton_UI.onClick.AddListener(OnToggleCropBoxPressed);

        cameraUpdateInterval = 1.0f / Mathf.Max(1f, cameraUpdateRateFPS);

        if (uiManager != null) uiManager.ShowConnectionPanel();
        LogStatus("Ready to connect.");
    }

    void Update()
    {
        if (IsConnected && cameraController != null && clientActualCamera != null)
        {
            timeSinceLastCameraUpdate += Time.deltaTime;
            if (timeSinceLastCameraUpdate >= cameraUpdateInterval)
            {
                SendCameraState(false);
                timeSinceLastCameraUpdate = 0f;
            }
        }
    }

    void AttemptConnect()
    {
        if (IsConnected || isAttemptingConnection) return;
        string ip = (ipAddressInput != null && !string.IsNullOrWhiteSpace(ipAddressInput.text)) ? ipAddressInput.text : defaultIpAddress;
        string url = $"ws://{ip}:{serverPort}{servicePath}";
        LogStatus($"Attempting to connect to {url}...");
        isAttemptingConnection = true;
        ws = new WebSocket(url);
        ws.OnOpen += (sender, e) => UnityMainThreadDispatcher.Instance().Enqueue(OnWebSocketOpen);
        ws.OnMessage += (sender, e) => UnityMainThreadDispatcher.Instance().Enqueue(() => OnWebSocketMessage(e.Data));
        ws.OnError += (sender, e) => UnityMainThreadDispatcher.Instance().Enqueue(() => OnWebSocketError(e.Message));
        ws.OnClose += (sender, e) => UnityMainThreadDispatcher.Instance().Enqueue(() => OnWebSocketClose(e.Reason, e.Code));
        try { ws.ConnectAsync(); }
        catch (Exception ex) { UnityMainThreadDispatcher.Instance().Enqueue(() => OnWebSocketError($"Connection Exception: {ex.Message}")); }
    }

    private void OnWebSocketOpen()
    {
        LogStatus("Connection established!");
        isAttemptingConnection = false;
        if (uiManager != null) uiManager.ShowMainMenuPanel();
        SendInitialCameraState();
    }

    private void OnWebSocketMessage(string data) { LogStatus($"Server: {data}"); }
    private void OnWebSocketError(string errorMessage)
    {
        LogStatus($"Connection Error: {errorMessage}", true);
        isAttemptingConnection = false;
        if (ws != null && ws.ReadyState != WebSocketState.Closed) ws.Close();
        ws = null;
        if (uiManager != null) uiManager.ShowConnectionPanel();
    }
    private void OnWebSocketClose(string reason, ushort code)
    {
        LogStatus($"Connection Closed: {reason} (Code: {code})");
        isAttemptingConnection = false;
        ws = null;
        if (uiManager != null) uiManager.ShowConnectionPanel();
    }

    private void SendInitialCameraState()
    {
        if (!IsConnected || cameraController == null || clientActualCamera == null) return;
        SendCameraState(true);
        timeSinceLastCameraUpdate = 0f;
    }

    private void SendCameraState(bool isInitial = false)
    {
        if (!IsConnected || cameraController == null || clientActualCamera == null) return;
        CameraStateData state = new CameraStateData
        {
            position = cameraController.transform.position,
            rotation = cameraController.transform.rotation,
            orthoSize = clientActualCamera.orthographic ? clientActualCamera.orthographicSize : -1f
        };
        string jsonData = JsonUtility.ToJson(state);
        string messageType = isInitial ? "INITIAL_CAMERA_STATE" : "UPDATE_CAMERA_STATE";
        SendMessageToServer($"{messageType}:{jsonData}");
    }

    private void OnLoadModelSelected(string modelId)
    {
        if (!IsConnected) { LogStatus("Not connected.", true); return; }
        LogStatus($"Model selected: {modelId}. Sending to server...");
        SendMessageToServer($"LOAD_MODEL:{modelId.ToUpperInvariant()}");

        if (mockedModelControllerRef != null)
        {
            // Send the MockedModel's root transform. Its "ref" child is assumed to be at local (0,0,0) with identity rotation.
            Transform mockedModelTransform = mockedModelControllerRef.transform;
            ModelTransformStateData initialState = new ModelTransformStateData
            {
                localPosition = mockedModelTransform.localPosition,
                localRotation = mockedModelTransform.localRotation,
                localScale = mockedModelTransform.localScale
            };
            string transformJson = JsonUtility.ToJson(initialState);
            SendMessageToServer($"SET_INITIAL_MODEL_TRANSFORM:{transformJson}");
        }

        if (uiManager != null) uiManager.ShowModelViewPanel();
    }

    private void OnBackToMainMenuPressed() { LogStatus("Returning to Main Menu."); if (uiManager != null) uiManager.ShowMainMenuPanel(); }
    private void OnToggleRotationGizmoPressed() { if (uiManager != null) uiManager.ToggleRotationGizmoAndAxesVisibility(); }
    private void OnToggleCropBoxPressed() { if (uiManager != null) uiManager.ToggleCropBoxVisibility(); }

    private void LogStatus(string message, bool isError = false)
    {
        if (isError) Debug.LogError($"[Client WSManager] {message}"); else Debug.Log($"[Client WSManager] {message}");
        if (statusText != null) statusText.text = message;
    }

    public void SendMessageToServer(string message)
    {
        if (IsConnected) { ws.Send(message); }
        else { LogStatus("Not connected. Cannot send message.", true); }
    }

    void OnDestroy() { if (ws != null && ws.ReadyState != WebSocketState.Closed) ws.CloseAsync(); ws = null; }
}