using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using WebSocketSharp;
using System.Collections.Generic;

public class WebSocketClientManager : MonoBehaviour
{
    [Header("Connection Settings")]
    [SerializeField] private bool autoConnectMode = false;
    [SerializeField] private UIManager uiManager;
    [SerializeField] private ModelViewportController modelViewportController;
    [SerializeField] private Camera referenceCamera;
    [SerializeField] private TMP_InputField ipAddressInput;
    [SerializeField] private string defaultIpAddress = "192.168.0.35";
    [SerializeField] private int serverPort = 8070;

    [Header("UI Elements")]
    [SerializeField] private Button connectButton;
    [SerializeField] private TMP_Text connectButtonText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Image indicatorImage;
    [SerializeField] private Sprite defaultIndicatorSprite;
    [SerializeField] private Button backButtonFromModelView;

    [Header("Available Models")]
    [SerializeField] private ModelData[] availableModelDataList;

    private WebSocket ws;
    private bool isAttemptingConnection = false;
    private bool isMockConnected = false;
    private float timeSinceLastModelUpdate = 0f;
    private float modelUpdateInterval;


    private ModelTransformStateData lastSentState;


    public bool IsConnected =>
        autoConnectMode ? isMockConnected : (ws != null && ws.ReadyState == WebSocketState.Open);

    public enum ConnectionState { IdleWaiting, Connecting, Connected, Failed, Disconnected }

    void Start()
    {
        if (uiManager == null)
        {
            Debug.LogError("WSClientManager: UIManager not assigned!");
            return;
        }

        if (modelViewportController == null)
        {
            Debug.LogError("WSClientManager: MockedModelControllerRef not assigned!");
            return;
        }

        if (referenceCamera == null)
        {
            Debug.LogError("WSClientManager: Reference Camera not assigned!");
            return;
        }

        if (connectButton == null)
        {
            Debug.LogError("WSClientManager: Connect Button not assigned!");
            return;
        }

        if (connectButtonText == null)
        {
            Debug.LogError("WSClientManager: Connect Button Text not assigned!");
            return;
        }

        if (statusText == null)
        {
            Debug.LogError("WSClientManager: Status Text not assigned!");
            return;
        }

        if (indicatorImage == null)
        {
            Debug.LogError("WSClientManager: Indicator Image not assigned!");
            return;
        }

        if (defaultIndicatorSprite == null)
        {
            Debug.LogError("WSClientManager: Default Indicator Sprite not assigned!");
            return;
        }

        modelUpdateInterval = 1.0f / Mathf.Max(1f, Constants.MODEL_UPDATE_FPS);

        InitializeLocalModelList();

        if (autoConnectMode)
        {
            uiManager.ShowLoadingScreenOrMinimalStatus();
            HandleMockConnect();
        }
        else
        {
            if (ipAddressInput != null)
                ipAddressInput.text = defaultIpAddress;
            else
            {
                Debug.LogError("WSClientManager: IP Address InputField required for Manual Mode.");
                return;
            }

            connectButton.onClick.AddListener(AttemptConnect);
            uiManager.ShowConnectionPanel();
            UpdateConnectionUI(ConnectionState.IdleWaiting);
        }

        if (backButtonFromModelView != null)
            backButtonFromModelView.onClick.AddListener(OnBackToMainMenuPressed);
    }

    /// <summary>
    /// Initialize model list from local ModelData ScriptableObjects (no server needed)
    /// </summary>
    private void InitializeLocalModelList()
    {
        if (availableModelDataList == null || availableModelDataList.Length == 0)
        {
            Debug.LogWarning("WebSocketClientManager: availableModelDataList not assigned!");
            return;
        }

        var metadataList = new List<ModelMetadata>();

        foreach (var modelData in availableModelDataList)
        {
            if (modelData == null)
            {
                Debug.LogWarning("ModelData entry is null!");
                continue;
            }

            if (modelData.thumbnail == null)
            {
                Debug.LogWarning($"Model {modelData.modelID} has no thumbnail!");
                continue;
            }

            try
            {
                byte[] imageData = modelData.thumbnail.texture.EncodeToPNG();
                string base64Thumbnail = Convert.ToBase64String(imageData);

                metadataList.Add(new ModelMetadata
                {
                    modelID = modelData.modelID,
                    displayName = modelData.displayName,
                    description = modelData.description,
                    thumbnailBase64 = base64Thumbnail
                });
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to process thumbnail for {modelData.modelID}: {ex.Message}");
            }
        }

        if (uiManager != null && metadataList.Count > 0)
        {
            uiManager.PopulateModelButtons(metadataList, this);
            Debug.Log($"Loaded {metadataList.Count} models from local ModelData list.");
        }
    }


    void Update()
    {
        if (IsConnected && modelViewportController != null && uiManager != null && uiManager.modelViewPanel.activeInHierarchy)
        {
            timeSinceLastModelUpdate += Time.deltaTime;
            if (timeSinceLastModelUpdate >= modelUpdateInterval)
            {
                SendModelTransformState();
                timeSinceLastModelUpdate = 0f;
            }
        }
    }

    public void AttemptConnect()
    {
        if (IsConnected || isAttemptingConnection)
            return;

        if (autoConnectMode)
        {
            HandleMockConnect();
            return;
        }

        string ip = ipAddressInput != null && !string.IsNullOrWhiteSpace(ipAddressInput.text) ? ipAddressInput.text : defaultIpAddress;
        string url = $"ws://{ip}:{serverPort}{Constants.SERVICE_PATH}";

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

    private void HandleMockConnect()
    {
        isMockConnected = true;
        isAttemptingConnection = false;
        OnWebSocketOpen();
    }

    private void SendModelTransformState()
    {
        if (!IsConnected || modelViewportController == null || autoConnectMode) return;

        Transform tr = modelViewportController.transform;

        if (lastSentState != null &&
            Vector3.SqrMagnitude(tr.localPosition - lastSentState.localPosition) < 0.0001f &&
            Quaternion.Angle(tr.localRotation, lastSentState.localRotation) < 0.1f &&
            Vector3.SqrMagnitude(tr.localScale - lastSentState.localScale) < 0.0001f)
        {
            return;
        }

        ModelTransformStateData state = new ModelTransformStateData
        {
            localPosition = tr.localPosition,
            localRotation = tr.localRotation,
            localScale = tr.localScale
        };

        lastSentState = state;
        string jsonData = JsonUtility.ToJson(state);
        SendMessageToServer($"{Constants.UPDATE_MODEL_TRANSFORM}:{jsonData}");
    }


    private void SendCameraTransformState()
    {
        if (!IsConnected || referenceCamera == null)
            return;

        if (autoConnectMode)
            return;

        ClientCameraStateData state = new ClientCameraStateData
        {
            position = referenceCamera.transform.position,
            rotation = referenceCamera.transform.rotation
        };

        string jsonData = JsonUtility.ToJson(state);
        SendMessageToServer($"{Constants.UPDATE_CAMERA_TRANSFORM}:{jsonData}");
    }

    public void SendVisualCropPlane(Vector3 position, Vector3 normal, float scale)
    {
        if (!IsConnected)
            return;

        VisualCropPlaneData data = new VisualCropPlaneData
        {
            position = position,
            normal = normal,
            scale = scale
        };

        string jsonData = JsonUtility.ToJson(data);
        SendMessageToServer($"{Constants.UPDATE_VISUAL_CROP_PLANE}:{jsonData}");
    }

    public void SendExecuteSlice(SliceActionData data)
    {
        if (!IsConnected)
            return;

        string jsonData = JsonUtility.ToJson(data);
        SendMessageToServer($"{Constants.EXECUTE_SLICE_ACTION}:{jsonData}");
    }

    public void SendExecuteDestroy(DestroyActionData data)
    {
        if (!IsConnected)
            return;

        string jsonData = JsonUtility.ToJson(data);
        SendMessageToServer($"{Constants.EXECUTE_DESTROY_ACTION}:{jsonData}");
    }

    public void SendStartShake(DestroyActionData data)
    {
        if (!IsConnected)
            return;

        string jsonData = JsonUtility.ToJson(data);
        SendMessageToServer($"{Constants.START_SHAKE}:{jsonData}");
    }

    public void SendStopShake(DestroyActionData data)
    {
        if (!IsConnected)
            return;

        string jsonData = JsonUtility.ToJson(data);
        SendMessageToServer($"{Constants.STOP_SHAKE}:{jsonData}");
    }

    public void SendUndoAction(string actionID)
    {
        if (!IsConnected)
            return;

        SendMessageToServer(Constants.UNDO_ACTION);
    }

    public void SendRedoAction(string actionID)
    {
        if (!IsConnected)
            return;

        SendMessageToServer(Constants.REDO_ACTION);
    }

    public void SendResetAll()
    {
        if (!IsConnected)
            return;

        SendMessageToServer(Constants.RESET_ALL);
    }

    public void SendLineData(Vector3 start, Vector3 end)
    {
        if (!IsConnected)
            return;

        LineData data = new LineData
        {
            start = start,
            end = end
        };

        string jsonData = JsonUtility.ToJson(data);
        SendMessageToServer($"{Constants.UPDATE_CUT_LINE}:{jsonData}");
    }

    public void SendHideLine()
    {
        if (!IsConnected)
            return;

        SendMessageToServer(Constants.HIDE_CUT_LINE);
    }

    public void SendShowSliceIcon(Vector3 worldPosition)
    {
        if (!IsConnected)
            return;

        var data = new ShowSliceIconData
        {
            worldPosition = worldPosition
        };

        string jsonData = JsonUtility.ToJson(data);
        SendMessageToServer($"{Constants.SHOW_SLICE_ICON}:{jsonData}");
    }

    public void SendHideSliceIcon()
    {
        if (!IsConnected)
            return;

        SendMessageToServer(Constants.HIDE_SLICE_ICON);
    }

    private void OnWebSocketOpen()
    {
        UpdateConnectionUI(ConnectionState.Connected);
        isAttemptingConnection = false;

        // Models are already loaded from local database in Initialize()
        // No need to request from server

        if (uiManager != null)
            uiManager.ShowMainMenuPanel();
    }

    private void OnWebSocketMessage(string data)
    {
        if (autoConnectMode)
            return;

        string[] parts = data.Split(new char[] { ',' }, 2);
        string command = parts[0].ToUpperInvariant();
        string args = parts.Length > 1 ? parts[1] : null;

        if (command == Constants.MODEL_SIZE_UPDATE)
            ProcessModelSizeUpdate(args);
        else
            Debug.Log($"Received unknown message from server: {data}");

    }

    private void ProcessModelSizeUpdate(string args)
    {
        if (modelViewportController == null || string.IsNullOrEmpty(args))
            return;

        try
        {
            ModelBoundsSizeData sizeData = JsonUtility.FromJson<ModelBoundsSizeData>(args);
            // Apply server's model scale if needed
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error parsing ModelBoundsSizeData: {ex.Message}");
        }
    }

    public Sprite Base64ToSprite(string base64)
    {
        if (string.IsNullOrEmpty(base64))
            return null;

        try
        {
            byte[] imageBytes = Convert.FromBase64String(base64);
            Texture2D texture = new Texture2D(2, 2);
            texture.LoadImage(imageBytes);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            return sprite;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error converting Base64 to Sprite: {ex.Message}");
            return null;
        }
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
        if (modelViewportController != null)
            modelViewportController.ResetState();

        if (ws != null)
        {
            if (ws.ReadyState != WebSocketState.Closed)
                ws.Close();
            ws = null;
        }

        isMockConnected = false;

        CuttingPlaneManager cuttingPlaneManager = FindObjectOfType<CuttingPlaneManager>();
        if (cuttingPlaneManager != null)
            cuttingPlaneManager.ResetCrop();

        if (uiManager != null)
            uiManager.ShowConnectionPanel();
    }

    public void OnLoadModelSelected(string modelId)
    {
        if (!IsConnected)
            return;

        if (!autoConnectMode)
            SendMessageToServer(Constants.LOAD_MODEL + ":" + modelId);
        else
            Debug.Log($"MOCK: Simulating model load {modelId}");

        SendCameraTransformState();

        if (modelViewportController != null)
            modelViewportController.LoadNewModel(modelId);

        if (uiManager != null)
            uiManager.ShowModelViewPanel();

        CuttingPlaneManager cpm = FindObjectOfType<CuttingPlaneManager>();
        if (cpm != null) cpm.ResetCrop();
    }

    private void OnBackToMainMenuPressed()
    {
        if (modelViewportController != null)
            modelViewportController.ResetState();

        CuttingPlaneManager cuttingPlaneManager = UnityEngine.Object.FindFirstObjectByType<CuttingPlaneManager>();
        if (cuttingPlaneManager != null)
            cuttingPlaneManager.ResetCrop();

        if (IsConnected && !autoConnectMode)
            SendMessageToServer(Constants.UNLOAD_MODEL);

        if (uiManager != null)
            uiManager.ShowMainMenuPanel();
    }

    private void UpdateConnectionUI(ConnectionState state)
    {
        string message = string.Empty;
        string buttonText = string.Empty;
        Color indicatorColor = Color.gray;
        bool buttonInteractable = true;

        switch (state)
        {
            case ConnectionState.IdleWaiting:
                message = "Ready to connect";
                indicatorColor = Color.gray;
                buttonText = "Connect";
                break;

            case ConnectionState.Connecting:
                message = "Connecting...";
                indicatorColor = Color.yellow;
                buttonText = "Connect";
                buttonInteractable = false;
                break;

            case ConnectionState.Connected:
                message = "Connected to Server (Active/Simulated)";
                indicatorColor = Color.green;
                buttonText = "Connect";
                break;

            case ConnectionState.Failed:
                message = "Connection failed. Try again.";
                indicatorColor = Color.red;
                buttonText = "Try Again";
                break;

            case ConnectionState.Disconnected:
                message = "Connection closed.";
                indicatorColor = Color.Lerp(Color.yellow, Color.red, 0.5f);
                buttonText = "Reconnect";
                break;
        }

        if (statusText != null)
            statusText.text = message;

        if (indicatorImage != null)
        {
            indicatorImage.sprite = defaultIndicatorSprite;
            indicatorImage.color = indicatorColor;
            indicatorImage.enabled = defaultIndicatorSprite != null;
        }

        if (connectButton != null)
            connectButton.interactable = buttonInteractable;

        if (connectButtonText != null)
            connectButtonText.text = buttonText;
    }

    public void SendMessageToServer(string message)
    {
        if (autoConnectMode)
            return;

        if (IsConnected)
            ws.Send(message);
    }

    void OnDestroy()
    {
        if (ws != null)
        {
            if (ws.ReadyState != WebSocketState.Closed)
                ws.CloseAsync();
            ws = null;
        }
    }
}
