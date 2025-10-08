using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using WebSocketSharp;
public class WebSocketClientManager : MonoBehaviour
{
    [SerializeField] private bool autoConnectMode = false;
    [SerializeField] private UIManager uiManager;
    [SerializeField] private MockedModelController mockedModelControllerRef;
    [SerializeField] private Camera referenceCamera;

    [SerializeField] private TMP_InputField ipAddressInput;
    [SerializeField] private string defaultIpAddress = "192.168.0.83";
    [SerializeField] private int serverPort = 8070;
    [SerializeField] private string servicePath = "/Control";
    [SerializeField] private float modelUpdateRateFPS = 60f;

    [SerializeField] private Button connectButton;
    [SerializeField] private TMP_Text connectButtonText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Image indicatorImage;
    [SerializeField] private Sprite defaultIndicatorSprite;
    [SerializeField] private Button loadModel1Button;
    [SerializeField] private Button loadModel2Button;
    [SerializeField] private Button backButtonFromModelView;

    private GameObject modelViewPanelCachedRef;
    private WebSocket ws;
    private bool isAttemptingConnection = false;
    private bool isMockConnected = false;
    private float timeSinceLastModelUpdate = 0f;
    private float modelUpdateInterval;

    public bool IsConnected => autoConnectMode ? isMockConnected : (ws != null && ws.ReadyState == WebSocketState.Open);

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
        if (uiManager == null) { Debug.LogError("[WSClientManager] UIManager not assigned!"); return; }
        if (mockedModelControllerRef == null) { Debug.LogError("[WSClientManager] MockedModelControllerRef not assigned!"); return; }
        if (referenceCamera == null) { Debug.LogError("[WSClientManager] Reference Camera not assigned!"); return; }
        if (connectButton == null) { Debug.LogError("[WSClientManager] Connect Button not assigned!"); return; }
        if (connectButtonText == null) { Debug.LogError("[WSClientManager] Connect Button Text not assigned!"); return; }
        if (statusText == null) { Debug.LogError("[WSClientManager] Status Text not assigned!"); return; }
        if (indicatorImage == null) { Debug.LogError("[WSClientManager] Indicator Image not assigned!"); return; }
        if (defaultIndicatorSprite == null) { Debug.LogError("[WSClientManager] Default Indicator Sprite not assigned!"); return; }

        if (uiManager.modelViewPanel != null) { modelViewPanelCachedRef = uiManager.modelViewPanel; }
        else { Debug.LogError("[WSClientManager] UIManager's modelViewPanel is null."); }

        modelUpdateInterval = 1.0f / Mathf.Max(1f, modelUpdateRateFPS);

        if (autoConnectMode)
        {
            uiManager.ShowLoadingScreenOrMinimalStatus();
            HandleMockConnect();
        }
        else
        {
            if (ipAddressInput != null)
            {
                ipAddressInput.text = defaultIpAddress;
                connectButton.onClick.AddListener(AttemptConnect);
            }
            else
            {
                Debug.LogError("[WSClientManager] IP Address InputField required for Manual Mode.");
                return;
            }

            uiManager.ShowConnectionPanel();
            UpdateConnectionUI(ConnectionState.IdleWaiting);
        }

        if (loadModel1Button != null) loadModel1Button.onClick.AddListener(() => OnLoadModelSelected("1"));
        if (loadModel2Button != null) loadModel2Button.onClick.AddListener(() => OnLoadModelSelected("2"));
        if (backButtonFromModelView != null) backButtonFromModelView.onClick.AddListener(OnBackToMainMenuPressed);
    }

    void Update()
    {
        if (IsConnected && mockedModelControllerRef != null && modelViewPanelCachedRef != null && modelViewPanelCachedRef.activeInHierarchy)
        {
            timeSinceLastModelUpdate += Time.deltaTime;
            if (timeSinceLastModelUpdate >= modelUpdateInterval)
            {
                SendModelTransformState();
                SendCameraTransformState();
                timeSinceLastModelUpdate = 0f;
            }
        }
    }

    public void AttemptConnect()
    {
        if (IsConnected || isAttemptingConnection) return;

        if (autoConnectMode)
        {
            HandleMockConnect();
            return;
        }

        string ip = (ipAddressInput != null && !string.IsNullOrWhiteSpace(ipAddressInput.text))
            ? ipAddressInput.text
            : defaultIpAddress;

        string url = $"ws://{ip}:{serverPort}{servicePath}";

        UpdateConnectionUI(ConnectionState.Connecting);
        isAttemptingConnection = true;
        ws = new WebSocket(url);

        ws.OnOpen += (sender, e) => UnityMainThreadDispatcher.Instance().Enqueue(OnWebSocketOpen);
        ws.OnMessage += (sender, e) => UnityMainThreadDispatcher.Instance().Enqueue(() => OnWebSocketMessage(e.Data));
        ws.OnError += (sender, e) => UnityMainThreadDispatcher.Instance().Enqueue(() => OnWebSocketError(e.Message));
        ws.OnClose += (sender, e) => UnityMainThreadDispatcher.Instance().Enqueue(() => OnWebSocketClose(e.Reason, e.Code));

        try { ws.ConnectAsync(); }
        catch (Exception ex) { UnityMainThreadDispatcher.Instance().Enqueue(() => OnWebSocketError($"Failed to initiate connection: {ex.Message}")); }
    }

    private void HandleMockConnect()
    {
        isMockConnected = true;
        isAttemptingConnection = false;
        OnWebSocketOpen();
    }

    private void SendModelTransformState()
    {
        if (!IsConnected || mockedModelControllerRef == null) return;
        if (autoConnectMode) return;

        Transform modelTransform = mockedModelControllerRef.transform;
        ModelTransformStateData state = new ModelTransformStateData
        {
            localPosition = modelTransform.localPosition,
            localRotation = modelTransform.localRotation,
            localScale = modelTransform.localScale
        };
        string jsonData = JsonUtility.ToJson(state);
        SendMessageToServer($"UPDATE_MODEL_TRANSFORM:{jsonData}");
    }

    private void SendCameraTransformState()
    {
        if (!IsConnected || referenceCamera == null) return;
        if (autoConnectMode) return;

        ClientCameraStateData state = new ClientCameraStateData
        {
            position = referenceCamera.transform.position,
            rotation = referenceCamera.transform.rotation
        };
        string jsonData = JsonUtility.ToJson(state);
        SendMessageToServer($"UPDATE_CAMERA_TRANSFORM:{jsonData}");
    }

    public void SendVisualCropPlane(Vector3 position, Vector3 normal, float scale)
    {
        if (!IsConnected) return;
        VisualCropPlaneData data = new VisualCropPlaneData { position = position, normal = normal, scale = scale };
        string jsonData = JsonUtility.ToJson(data);
        SendMessageToServer($"UPDATE_VISUAL_CROP_PLANE:{jsonData}");
    }

    public void SendExecuteSlice(SliceActionData data)
    {
        if (!IsConnected) return;
        string jsonData = JsonUtility.ToJson(data);
        SendMessageToServer($"EXECUTE_SLICE_ACTION:{jsonData}");
    }

    public void SendExecuteDestroy(DestroyActionData data)
    {
        if (!IsConnected) return;
        string jsonData = JsonUtility.ToJson(data);
        SendMessageToServer($"EXECUTE_DESTROY_ACTION:{jsonData}");
    }

    public void SendStartShake(DestroyActionData data)
    {
        if (!IsConnected) return;
        string jsonData = JsonUtility.ToJson(data);
        SendMessageToServer($"START_SHAKE:{jsonData}");
    }

    public void SendStopShake(DestroyActionData data)
    {
        if (!IsConnected) return;
        string jsonData = JsonUtility.ToJson(data);
        SendMessageToServer($"STOP_SHAKE:{jsonData}");
    }

    public void SendUndoAction(string actionID)
    {
        if (!IsConnected) return;
        SendMessageToServer($"UNDO_ACTION");
    }

    public void SendRedoAction(string actionID)
    {
        if (!IsConnected) return;
        SendMessageToServer($"REDO_ACTION");
    }

    public void SendResetAll()
    {
        if (!IsConnected) return;
        SendMessageToServer("RESET_ALL");
    }

    public void SendLineData(Vector3 start, Vector3 end)
    {
        if (!IsConnected) return;
        LineData data = new LineData { start = start, end = end };
        string jsonData = JsonUtility.ToJson(data);
        SendMessageToServer($"UPDATE_CUT_LINE:{jsonData}");
    }

    public void SendHideLine()
    {
        if (!IsConnected) return;
        SendMessageToServer("HIDE_CUT_LINE");
    }

    private void OnWebSocketOpen()
    {
        UpdateConnectionUI(ConnectionState.Connected);
        isAttemptingConnection = false;
        if (uiManager != null) uiManager.ShowMainMenuPanel();
    }

    private void OnWebSocketMessage(string data)
    {
        if (autoConnectMode) return;

        string[] parts = data.Split(new char[] { ':' }, 2);
        string command = parts[0].ToUpperInvariant();
        string args = parts.Length > 1 ? parts[1] : null;

        if (command == "MODEL_SIZE_UPDATE") ProcessModelSizeUpdate(args);
        else Debug.Log($"Received unknown message from server: \"{data}\"");
    }

    private void ProcessModelSizeUpdate(string args)
    {
        if (mockedModelControllerRef == null || string.IsNullOrEmpty(args)) return;
        try
        {
            ModelBoundsSizeData sizeData = JsonUtility.FromJson<ModelBoundsSizeData>(args);
            mockedModelControllerRef.ApplyServerModelScale(sizeData.size);
        }
        catch (Exception ex) { Debug.LogError($"Error parsing ModelBoundsSizeData: {ex.Message} | Args: {args}"); }
    }

    private void OnWebSocketError(string errorMessage)
    {
        UpdateConnectionUI(ConnectionState.Failed);
        isAttemptingConnection = false;
        PerformDisconnectionCleanup();
    }

    private void OnWebSocketClose(string reason, ushort code)
    {
        UpdateConnectionUI(ConnectionState.Disconnected);
        isAttemptingConnection = false;
        PerformDisconnectionCleanup();
    }

    private void PerformDisconnectionCleanup()
    {
        if (mockedModelControllerRef != null) mockedModelControllerRef.ResetState();

        if (ws != null && ws.ReadyState != WebSocketState.Closed) ws.Close();
        ws = null;
        isMockConnected = false;

        CuttingPlaneManager cuttingPlaneManager = FindObjectOfType<CuttingPlaneManager>();
        if (cuttingPlaneManager != null)
        {
            cuttingPlaneManager.ResetCrop();
        }

        if (uiManager != null) uiManager.ShowConnectionPanel();
    }

    private void OnLoadModelSelected(string modelId)
    {
        if (!IsConnected) return;

        if (!autoConnectMode)
        {
            SendMessageToServer($"LOAD_MODEL:{modelId.ToUpperInvariant()}");
            SendCameraTransformState();
        }
        else
        {
            Debug.Log($"[MOCK] Simulating model load: {modelId}");
        }

        if (uiManager != null) uiManager.ShowModelViewPanel();
    }

    private void OnBackToMainMenuPressed()
    {
        if (mockedModelControllerRef != null)
        {
            mockedModelControllerRef.ResetState();
        }

        CuttingPlaneManager cuttingPlaneManager = FindObjectOfType<CuttingPlaneManager>();
        if (cuttingPlaneManager != null)
        {
            cuttingPlaneManager.ResetCrop();
        }

        if (IsConnected && !autoConnectMode)
        {
            SendMessageToServer("UNLOAD_MODEL");
        }

        if (uiManager != null) uiManager.ShowMainMenuPanel();
    }

    private void UpdateConnectionUI(ConnectionState state)
    {
        string message = "", buttonText = "";
        Color indicatorColor = Color.gray;
        bool buttonInteractable = true;

        switch (state)
        {
            case ConnectionState.IdleWaiting: message = "Ready to connect"; indicatorColor = Color.gray; buttonText = "Connect"; break;
            case ConnectionState.Connecting: message = $"Connecting..."; indicatorColor = Color.yellow; buttonText = "Connect"; buttonInteractable = false; break;
            case ConnectionState.Connected: message = "Connected (Server Active/Simulated)"; indicatorColor = Color.green; buttonText = "Connect"; break;
            case ConnectionState.Failed: message = "Connection failed. Try again."; indicatorColor = Color.red; buttonText = "Try Again"; break;
            case ConnectionState.Disconnected: message = "Connection closed."; indicatorColor = Color.Lerp(Color.yellow, Color.red, 0.5f); buttonText = "Reconnect"; break;
        }

        if (statusText != null) statusText.text = message;
        if (indicatorImage != null) { indicatorImage.sprite = defaultIndicatorSprite; indicatorImage.color = indicatorColor; indicatorImage.enabled = (defaultIndicatorSprite != null); }

        if (connectButton != null) connectButton.interactable = buttonInteractable;
        if (connectButtonText != null) connectButtonText.text = buttonText;
    }

    public void SendMessageToServer(string message)
    {
        if (autoConnectMode)
        {
            Debug.Log($"[MOCK] Suppressing server message: {message}");
            return;
        }

        if (IsConnected) ws.Send(message);
    }

    void OnDestroy()
    {
        if (ws != null && ws.ReadyState != WebSocketState.Closed) ws.CloseAsync();
        ws = null;
    }
}