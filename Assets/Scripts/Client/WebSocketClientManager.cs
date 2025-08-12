using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using WebSocketSharp;
using static JsonUtilityHelper; // To use ModelBoundsSizeData

public class WebSocketClientManager : MonoBehaviour
{
    [Header("UI Manager Reference")]
    [SerializeField] private UIManager uiManager;
    [SerializeField] private CameraController cameraController;
    [SerializeField] private MockedModelController mockedModelControllerRef;

    [Header("Networking")]
    [SerializeField] private TMP_InputField ipAddressInput;
    [SerializeField] private string defaultIpAddress = "192.168.0.83";
    [SerializeField] private int serverPort = 8070;
    [SerializeField] private string servicePath = "/Control";
    [SerializeField] private float cameraUpdateRateFPS = 60f;

    [Header("UI Elements for Connection & Control")]
    [SerializeField] private Button connectButton;
    [SerializeField] private TMP_Text connectButtonText;
    [SerializeField] private TMP_Text statusText;

    [SerializeField] private Image indicatorImage;
    [SerializeField] private Sprite defaultIndicatorSprite;

    [SerializeField] private Button loadCubeButton;
    [SerializeField] private Button loadCylinderButton;
    [SerializeField] private Button backButtonFromModelView;


    private GameObject modelViewPanelCachedRef;

    private WebSocket ws;
    private bool isAttemptingConnection = false;
    private float timeSinceLastCameraUpdate = 0f;
    private float cameraUpdateInterval;
    private Camera clientActualCamera;

    public bool IsConnected => ws != null && ws.ReadyState == WebSocketState.Open;

    public enum ConnectionState
    {
        IdleWaiting,
        Connecting,
        Connected,
        Failed,
        Disconnected
    }

    void Start()
    {
        if (uiManager == null) { Debug.LogError("[WSClientManager] UIManager not assigned! Please assign it in the Inspector."); return; }
        if (cameraController == null) { Debug.LogError("[WSClientManager] CameraController (CameraRig) not assigned! Please assign it in the Inspector."); return; }
        if (mockedModelControllerRef == null) { Debug.LogWarning("[WSClientManager] MockedModelControllerRef not assigned! Some model functionality might be limited."); }

        clientActualCamera = cameraController.GetComponentInChildren<Camera>();
        if (clientActualCamera == null) clientActualCamera = cameraController.GetComponent<Camera>();
        if (clientActualCamera == null) Debug.LogError("[WSClientManager] Actual Camera component not found on or under CameraController. OrthoSize updates will fail.");

        if (ipAddressInput == null) { Debug.LogError("[WSClientManager] IP Address InputField not assigned!"); return; }
        if (connectButton == null) { Debug.LogError("[WSClientManager] Connect Button not assigned!"); return; }
        if (connectButtonText == null) { Debug.LogError("[WSClientManager] Connect Button Text (TMP_Text) not assigned!"); return; }
        if (statusText == null) { Debug.LogError("[WSClientManager] Status Text (TMP_Text) not assigned!"); return; }

        if (indicatorImage == null)
        {
            Debug.LogError("[WSClientManager] Indicator Image not assigned! Please assign it in the Inspector.");
            return;
        }
        if (defaultIndicatorSprite == null)
        {
            Debug.LogError("[WSClientManager] Default Indicator Sprite not assigned! Please assign it in the Inspector.");
            return;
        }

        if (uiManager.modelViewPanel != null)
        {
            modelViewPanelCachedRef = uiManager.modelViewPanel;
        }
        else
        {
            Debug.LogError("[WSClientManager] UIManager's modelViewPanel is null. Camera updates won't be conditional on its visibility.");
        }

        if (ipAddressInput != null) ipAddressInput.text = defaultIpAddress;
        if (connectButton != null) connectButton.onClick.AddListener(AttemptConnect);
        else Debug.LogWarning("[WSClientManager] Connect Button not assigned. Connection functionality will not work.");

        if (loadCubeButton != null) loadCubeButton.onClick.AddListener(() => OnLoadModelSelected("CUBE"));
        else Debug.LogWarning("[WSClientManager] Load Cube Button not assigned. Model selection won't work for Cube.");

        if (loadCylinderButton != null) loadCylinderButton.onClick.AddListener(() => OnLoadModelSelected("CYLINDER"));
        else Debug.LogWarning("[WSClientManager] Load Cylinder Button not assigned. Model selection won't work for Cylinder.");

        if (backButtonFromModelView != null) backButtonFromModelView.onClick.AddListener(OnBackToMainMenuPressed);
        else Debug.LogWarning("[WSClientManager] Back Button From Model View not assigned. Navigation functionality won't work.");

        cameraUpdateInterval = 1.0f / Mathf.Max(1f, cameraUpdateRateFPS);

        uiManager.ShowConnectionPanel();
        UpdateConnectionUI(ConnectionState.IdleWaiting);
    }

    void Update()
    {
        if (IsConnected && cameraController != null && clientActualCamera != null && modelViewPanelCachedRef != null && modelViewPanelCachedRef.activeInHierarchy)
        {
            timeSinceLastCameraUpdate += Time.deltaTime;
            if (timeSinceLastCameraUpdate >= cameraUpdateInterval)
            {
                SendCameraState(false);
                timeSinceLastCameraUpdate = 0f;
            }
        }
    }

    public void AttemptConnect()
    {
        if (IsConnected)
        {
            LogStatus("Already connected to the server.", false, true);
            UpdateConnectionUI(ConnectionState.Connected);
            return;
        }
        if (isAttemptingConnection)
        {
            LogStatus("Connection attempt already in progress.", false, true);
            return;
        }

        string ip = (ipAddressInput != null && !string.IsNullOrWhiteSpace(ipAddressInput.text)) ? ipAddressInput.text : defaultIpAddress;
        string url = $"ws://{ip}:{serverPort}{servicePath}";

        UpdateConnectionUI(ConnectionState.Connecting);
        isAttemptingConnection = true;
        ws = new WebSocket(url);

        ws.OnOpen += (sender, e) => UnityMainThreadDispatcher.Instance().Enqueue(OnWebSocketOpen);
        ws.OnMessage += (sender, e) => UnityMainThreadDispatcher.Instance().Enqueue(() => OnWebSocketMessage(e.Data));
        ws.OnError += (sender, e) => UnityMainThreadDispatcher.Instance().Enqueue(() => OnWebSocketError(e.Message));
        ws.OnClose += (sender, e) => UnityMainThreadDispatcher.Instance().Enqueue(() => OnWebSocketClose(e.Reason, e.Code));

        try
        {
            ws.ConnectAsync();
        }
        catch (Exception ex)
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() => OnWebSocketError($"Failed to initiate connection: {ex.Message}"));
        }
    }

    private void SendCameraState(bool isInitial = false)
    {
        if (!IsConnected || cameraController == null || clientActualCamera == null)
        {
            LogStatus("Cannot send camera state: Not connected or camera controller/component missing.", true);
            return;
        }

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

    private void SendInitialCameraState()
    {
        if (!IsConnected || cameraController == null || clientActualCamera == null)
        {
            LogStatus("Cannot send initial camera state: Not connected or camera controller/component missing.", true);
            return;
        }
        SendCameraState(true);
        timeSinceLastCameraUpdate = 0f;
    }

    private void OnWebSocketOpen()
    {
        UpdateConnectionUI(ConnectionState.Connected);
        isAttemptingConnection = false;

        if (uiManager != null)
        {
            uiManager.ShowMainMenuPanel();
            Debug.Log("[WSClientManager] UI transitioned to Main Menu Panel for model selection.");
        }
    }

    private void OnWebSocketMessage(string data)
    {
        // Handle incoming messages from the server
        string[] parts = data.Split(new char[] { ':' }, 2);
        string command = parts[0].ToUpperInvariant();
        string args = parts.Length > 1 ? parts[1] : null;

        switch (command)
        {
            case "MODEL_SIZE_UPDATE":
                ProcessModelSizeUpdate(args);
                break;
            // Add other client-side command handling here if needed
            default:
                LogStatus($"Received unknown message from server: \"{data}\"", false, true);
                break;
        }
    }

    private void ProcessModelSizeUpdate(string args)
    {
        if (mockedModelControllerRef == null)
        {
            Debug.LogWarning("[WSClientManager] MockedModelControllerRef not assigned. Cannot apply model size update.");
            return;
        }
        if (string.IsNullOrEmpty(args))
        {
            Debug.LogWarning("[WSClientManager] MODEL_SIZE_UPDATE received with no arguments.");
            return;
        }
        try
        {
            ModelBoundsSizeData sizeData = JsonUtility.FromJson<ModelBoundsSizeData>(args);
            mockedModelControllerRef.ApplyServerModelScale(sizeData.size);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[WSClientManager] Error parsing ModelBoundsSizeData for MODEL_SIZE_UPDATE: {ex.Message} | Args: {args}");
        }
    }

    private void OnWebSocketError(string errorMessage)
    {
        LogStatus($"WebSocket Error: {errorMessage}", true);
        UpdateConnectionUI(ConnectionState.Failed);
        isAttemptingConnection = false;
        if (ws != null && ws.ReadyState != WebSocketState.Closed)
        {
            ws.Close();
        }
        ws = null;
        if (uiManager != null)
        {
            uiManager.ShowConnectionPanel();
            Debug.Log("[WSClientManager] UI transitioned back to Connection Panel due to WebSocket error.");
        }
    }

    private void OnWebSocketClose(string reason, ushort code)
    {
        LogStatus($"WebSocket Connection Closed. Reason: \"{reason}\" (Code: {code})", false);
        UpdateConnectionUI(ConnectionState.Disconnected);
        isAttemptingConnection = false;
        ws = null;
        if (uiManager != null)
        {
            uiManager.ShowConnectionPanel();
            Debug.Log("[WSClientManager] UI transitioned back to Connection Panel due to WebSocket closure.");
        }
    }

    private void OnLoadModelSelected(string modelId)
    {
        if (!IsConnected)
        {
            LogStatus($"Cannot load model '{modelId}': Not connected to server.", true, true);
            return;
        }

        SendMessageToServer($"LOAD_MODEL:{modelId.ToUpperInvariant()}");

        if (mockedModelControllerRef != null)
        {
            // Note: The client's initial transform for the *mocked model* is sent.
            // This is separate from the server's actual instantiated model's transform.
            // The server will then set its *own* model's initial transform based on this client data.
            // The server then calculates and sends the *actual server model's size* back to the client.
            Transform mockedModelRootTransform = mockedModelControllerRef.transform;
            ModelTransformStateData initialState = new ModelTransformStateData
            {
                localPosition = mockedModelRootTransform.localPosition,
                localRotation = mockedModelRootTransform.localRotation,
                localScale = mockedModelRootTransform.localScale // This scale will be the *mock* model's initial scale
            };
            string transformJson = JsonUtility.ToJson(initialState);
            SendMessageToServer($"SET_INITIAL_MODEL_TRANSFORM:{transformJson}");
        }
        else
        {
            Debug.LogWarning("[WSClientManager] MockedModelControllerRef is null, cannot send initial model transform. Please assign it in Inspector.");
        }

        if (uiManager != null)
        {
            uiManager.ShowModelViewPanel();
            LogStatus("UI transitioned to Model View Panel.", false, true);
        }

        SendInitialCameraState();
    }

    private void OnBackToMainMenuPressed()
    {
        LogStatus("Back button pressed from Model View. Returning to Main Menu.", false, true);
        if (uiManager != null) uiManager.ShowMainMenuPanel();
    }

    private void LogStatus(string message, bool isError = false, bool debugOnly = false)
    {
        if (isError) Debug.LogError($"[WSClientManager] {message}");
        else Debug.Log($"[WSClientManager] {message}");
    }

    private void UpdateConnectionUI(ConnectionState state)
    {
        string message = "";
        Color indicatorColor = Color.gray;
        string buttonText = "";
        bool buttonInteractable = true;

        switch (state)
        {
            case ConnectionState.IdleWaiting:
                message = "Ready to connect";
                indicatorColor = Color.gray;
                buttonText = "Connect";
                buttonInteractable = true;
                break;
            case ConnectionState.Connecting:
                message = $"Connecting to: ws://{ipAddressInput.text}:{serverPort}{servicePath}...";
                indicatorColor = Color.yellow;
                buttonText = "Connect";
                buttonInteractable = false;
                break;
            case ConnectionState.Connected:
                message = "Connected to server";
                indicatorColor = Color.green;
                buttonText = "Connect";
                buttonInteractable = true;
                break;
            case ConnectionState.Failed:
                message = "Connection failed. Try again.";
                indicatorColor = Color.red;
                buttonText = "Try Again";
                buttonInteractable = true;
                break;
            case ConnectionState.Disconnected:
                message = "Connection closed.";
                indicatorColor = Color.Lerp(Color.yellow, Color.red, 0.5f);
                buttonText = "Reconnect";
                buttonInteractable = true;
                break;
            default:
                Debug.LogWarning("[WSClientManager] Unknown ConnectionState: " + state);
                break;
        }

        if (statusText != null)
        {
            statusText.text = message;
        }

        if (indicatorImage != null)
        {
            indicatorImage.sprite = defaultIndicatorSprite;
            indicatorImage.color = indicatorColor;
            indicatorImage.enabled = (defaultIndicatorSprite != null);
        }

        if (connectButton != null)
        {
            connectButton.interactable = buttonInteractable;
        }
        if (connectButtonText != null)
        {
            connectButtonText.text = buttonText;
        }
    }

    public void SendMessageToServer(string message)
    {
        if (IsConnected)
        {
            ws.Send(message);
        }
        else
        {
            LogStatus("Not connected to server. Cannot send message.", true);
        }
    }

    void OnDestroy()
    {
        if (ws != null && ws.ReadyState != WebSocketState.Closed)
        {
            LogStatus("Closing WebSocket connection OnDestroy.", false, true);
            ws.CloseAsync();
        }
        ws = null;
    }
}