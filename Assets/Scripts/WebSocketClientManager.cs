
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
    [SerializeField] private string defaultIpAddress = Constants.DEFAULT_IP_ADDRESS;
    [SerializeField] private int serverPort = Constants.DEFAULT_PORT;

    [Header("UI Elements")]
    [SerializeField] private Button connectButton;
    [SerializeField] private TMP_Text connectButtonText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Image indicatorImage;
    [SerializeField] private Sprite defaultIndicatorSprite;
    [SerializeField] private Button backButtonFromModelView;

    private WebSocket ws;
    private bool isAttemptingConnection = false;
    private bool isMockConnected = false;
    private float timeSinceLastModelUpdate = 0f;
    private float modelUpdateInterval;
    private ModelTransformStateData lastSentState;
    private Coroutine connectionTimeoutCoroutine;

    public bool IsConnected => autoConnectMode ? isMockConnected : (ws != null && ws.ReadyState == WebSocketState.Open);

    public enum ConnectionState { IdleWaiting, Connecting, Connected, Failed, Disconnected }

    void Start()
    {
        Application.targetFrameRate = Constants.MODEL_UPDATE_FPS;
        QualitySettings.vSyncCount = 0;
        if (uiManager == null || modelViewportController == null || referenceCamera == null ||
            connectButton == null || connectButtonText == null || statusText == null ||
            indicatorImage == null || defaultIndicatorSprite == null)
        {
            Debug.LogError("WSClientManager: Missing critical references!");
            return;
        }

        modelUpdateInterval = 1.0f / Mathf.Max(1f, Constants.MODEL_UPDATE_FPS);

        if (autoConnectMode)
        {
            uiManager.ShowLoadingScreenOrMinimalStatus();
            HandleMockConnect();
        }
        else
        {
            if (ipAddressInput != null) ipAddressInput.text = defaultIpAddress;
            connectButton.onClick.AddListener(AttemptConnect);
            uiManager.ShowConnectionPanel();
            UpdateConnectionUI(ConnectionState.IdleWaiting);
        }

        if (backButtonFromModelView != null)
            backButtonFromModelView.onClick.AddListener(OnBackToMainMenuPressed);
    }

    void Update()
    {
        if (IsConnected && modelViewportController != null && uiManager != null && uiManager.modelViewPanel.activeInHierarchy)
        {
            timeSinceLastModelUpdate += Time.deltaTime;

            while (timeSinceLastModelUpdate >= modelUpdateInterval)
            {
                SendModelTransformState();
                timeSinceLastModelUpdate -= modelUpdateInterval;
            }
        }
    }

    public void AttemptConnect()
    {
        if (IsConnected || isAttemptingConnection) return;
        modelUpdateInterval = 1.0f / Mathf.Max(1f, Constants.MODEL_UPDATE_FPS);
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
        ws.NoDelay = true;
        ws.OnOpen += (sender, e) => UnityMainThreadDispatcher.Instance().Enqueue(OnWebSocketOpen);
        ws.OnMessage += (sender, e) => UnityMainThreadDispatcher.Instance().Enqueue(() => OnWebSocketMessage(e.Data));
        ws.OnError += (sender, e) => UnityMainThreadDispatcher.Instance().Enqueue(() => OnWebSocketError(e.Message));
        ws.OnClose += (sender, e) => UnityMainThreadDispatcher.Instance().Enqueue(() => OnWebSocketClose(e.Reason, e.Code));

        try
        {
            ws.ConnectAsync();
            if (connectionTimeoutCoroutine != null) StopCoroutine(connectionTimeoutCoroutine);
            connectionTimeoutCoroutine = StartCoroutine(ConnectionTimeoutSequence(Constants.CONNECTION_TIMEOUT));
        }
        catch (Exception ex)
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() => OnWebSocketError(ex.Message));
        }
    }

    private System.Collections.IEnumerator ConnectionTimeoutSequence(float timeout)
    {
        yield return new WaitForSecondsRealtime(timeout);

        if (isAttemptingConnection && !IsConnected)
        {
            isAttemptingConnection = false;
            UpdateConnectionUI(ConnectionState.Failed);

            if (ws != null)
            {
                try { ws.CloseAsync(); } catch { }
                ws = null;
            }
            PerformDisconnectionCleanup();
        }
    }

    private void HandleMockConnect()
    {
        isMockConnected = true;
        isAttemptingConnection = false;
        OnWebSocketOpen();
    }

    private void OnWebSocketOpen()
    {
        if (connectionTimeoutCoroutine != null) StopCoroutine(connectionTimeoutCoroutine);
        UpdateConnectionUI(ConnectionState.Connected);
        isAttemptingConnection = false;
        if (uiManager != null) uiManager.ShowMainMenuPanel();
    }

    private void OnWebSocketError(string errorMessage)
    {
        if (connectionTimeoutCoroutine != null) StopCoroutine(connectionTimeoutCoroutine);
        isAttemptingConnection = false;
        UpdateConnectionUI(ConnectionState.Failed);
        PerformDisconnectionCleanup();
    }

    private void OnWebSocketClose(string reason, ushort code)
    {
        if (connectionTimeoutCoroutine != null) StopCoroutine(connectionTimeoutCoroutine);
        isAttemptingConnection = false;
        UpdateConnectionUI(ConnectionState.Disconnected);
        PerformDisconnectionCleanup();
    }

    private void UpdateConnectionUI(ConnectionState state)
    {
        string message = string.Empty;
        string buttonText = "Connect";
        Color indicatorColor = Color.gray;
        bool buttonInteractable = true;

        switch (state)
        {
            case ConnectionState.IdleWaiting:
                message = "Ready to connect";
                indicatorColor = Color.gray;
                break;
            case ConnectionState.Connecting:
                message = "Connecting...";
                indicatorColor = Color.yellow;
                buttonInteractable = false;
                break;
            case ConnectionState.Connected:
                message = "Connected";
                indicatorColor = Color.green;
                break;
            case ConnectionState.Failed:
                message = "Connection Timed Out";
                indicatorColor = Color.red;
                buttonText = "Reconnect";
                break;
            case ConnectionState.Disconnected:
                message = "Connection Closed";
                indicatorColor = Color.gray;
                buttonText = "Reconnect";
                break;
        }

        if (statusText != null) statusText.text = message;
        if (indicatorImage != null)
        {
            indicatorImage.color = indicatorColor;
            indicatorImage.enabled = true;
            indicatorImage.sprite = defaultIndicatorSprite;
        }
        if (connectButton != null) connectButton.interactable = buttonInteractable;
        if (connectButtonText != null) connectButtonText.text = buttonText;
    }

    private void SendModelTransformState()
    {
        if (!IsConnected || modelViewportController == null || autoConnectMode) return;
        Transform tr = modelViewportController.transform;

        if (lastSentState != null &&
            Vector3.SqrMagnitude(tr.localPosition - lastSentState.localPosition) < 0.000001f &&
            Quaternion.Angle(tr.localRotation, lastSentState.localRotation) < 0.01f &&
            Vector3.SqrMagnitude(tr.localScale - lastSentState.localScale) < 0.000001f)
            return;

        ModelTransformStateData state = new ModelTransformStateData
        {
            localPosition = tr.localPosition,
            localRotation = tr.localRotation,
            localScale = tr.localScale
        };

        lastSentState = state;
        SendMessageToServer($"{Constants.UPDATE_MODEL_TRANSFORM}:{JsonUtility.ToJson(state)}");
    }

    public void SendVolumeDensity(float min, float max)
    {
        if (!IsConnected) return;
        VolumeDensityData data = new VolumeDensityData { minVal = min, maxVal = max };
        SendMessageToServer($"{Constants.UPDATE_VOLUME_DENSITY}:{JsonUtility.ToJson(data)}");
    }

    public void SendToggleAxes(bool visible)
    {
        if (!IsConnected) return;
        SendMessageToServer($"{Constants.TOGGLE_AXES}:{visible}");
    }
    public void SendVisualCropPlane(Vector3 position, Vector3 normal, float scale)
    {
        if (!IsConnected) return;
        VisualCropPlaneData data = new VisualCropPlaneData { position = position, normal = normal, scale = scale };
        SendMessageToServer($"{Constants.UPDATE_VISUAL_CROP_PLANE}:{JsonUtility.ToJson(data)}");
    }

    public void SendExecuteSlice(SliceActionData data)
    {
        if (!IsConnected) return;
        SendMessageToServer($"{Constants.EXECUTE_SLICE_ACTION}:{JsonUtility.ToJson(data)}");
    }

    public void SendExecuteDestroy(DestroyActionData data)
    {
        if (!IsConnected) return;
        SendMessageToServer($"{Constants.EXECUTE_DESTROY_ACTION}:{JsonUtility.ToJson(data)}");
    }

    public void SendStartShake(DestroyActionData data)
    {
        if (!IsConnected) return;
        SendMessageToServer($"{Constants.START_SHAKE}:{JsonUtility.ToJson(data)}");
    }

    public void SendStopShake(DestroyActionData data)
    {
        if (!IsConnected) return;
        SendMessageToServer($"{Constants.STOP_SHAKE}:{JsonUtility.ToJson(data)}");
    }

    public void SendUndoAction(string actionID) => SendMessageToServer(Constants.UNDO_ACTION);
    public void SendRedoAction(string actionID) => SendMessageToServer(Constants.REDO_ACTION);
    public void SendResetAll() => SendMessageToServer(Constants.RESET_ALL);

    public void SendLineData(Vector3 start, Vector3 end)
    {
        if (!IsConnected) return;
        LineData data = new LineData { start = start, end = end };
        SendMessageToServer($"{Constants.UPDATE_CUT_LINE}:{JsonUtility.ToJson(data)}");
    }

    public void SendHideLine() => SendMessageToServer(Constants.HIDE_CUT_LINE);

    public void SendShowSliceIcon(Vector3 worldPosition)
    {
        if (!IsConnected) return;
        var data = new ShowSliceIconData { worldPosition = worldPosition };
        SendMessageToServer($"{Constants.SHOW_SLICE_ICON}:{JsonUtility.ToJson(data)}");
    }

    public void SendHideSliceIcon() => SendMessageToServer(Constants.HIDE_SLICE_ICON);

    private void OnWebSocketMessage(string data)
    {
        if (autoConnectMode) return;
        string[] parts = data.Split(new char[] { ':' }, 2);
        string command = parts[0].ToUpperInvariant();

        if (command == Constants.MODEL_SIZE_UPDATE && parts.Length > 1)
        {
            try
            {
                var sizeData = JsonUtility.FromJson<ModelBoundsSizeData>(parts[1]);
                if (modelViewportController != null)
                {
                    modelViewportController.UpdatePlaceholderSize(sizeData.size);
                }
            }
            catch { }
        }
        else if (command == Constants.MODELS_LIST_UPDATE && parts.Length > 1)
        {
            try
            {
                var listData = JsonUtility.FromJson<ModelMetadataList>(parts[1]);
                if (uiManager != null && listData != null && listData.models != null)
                {
                    uiManager.PopulateModelButtons(new List<ModelMetadata>(listData.models), this);
                }
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to parse model list: " + e.Message);
            }
        }
        else if (command == "LOAD_PROXY_MESH" && parts.Length > 1)
        {
            try
            {
                var meshData = JsonUtility.FromJson<MeshNetworkData>(parts[1]);
                if (modelViewportController != null)
                {
                    modelViewportController.ApplyProxyMesh(meshData);
                }
            }
            catch (Exception ex) { Debug.LogError("Proxy mesh error: " + ex.Message); }
        }

    }

    public Sprite Base64ToSprite(string base64)
    {
        if (string.IsNullOrEmpty(base64)) return null;
        try
        {
            byte[] imageBytes = Convert.FromBase64String(base64);
            Texture2D texture = new Texture2D(2, 2);
            texture.LoadImage(imageBytes);
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        }
        catch { return null; }
    }

    private void PerformDisconnectionCleanup()
    {
        if (modelViewportController != null) modelViewportController.ResetState();
        if (ws != null)
        {
            try { ws.CloseAsync(); } catch { }
            ws = null;
        }
        isMockConnected = false;
        CuttingPlaneManager cpm = FindObjectOfType<CuttingPlaneManager>();
        if (cpm != null) cpm.ResetCrop();
        if (uiManager != null) uiManager.ShowConnectionPanel();
    }

    public void OnLoadModelSelected(string modelId)
    {
        if (!IsConnected) return;
        if (!autoConnectMode) SendMessageToServer(Constants.LOAD_MODEL + ":" + modelId);

        ClientCameraStateData camState = new ClientCameraStateData { position = referenceCamera.transform.position, rotation = referenceCamera.transform.rotation };
        SendMessageToServer($"{Constants.UPDATE_CAMERA_TRANSFORM}:{JsonUtility.ToJson(camState)}");

        if (modelViewportController != null) modelViewportController.LoadNewModel(modelId);
        if (uiManager != null) uiManager.ShowModelViewPanel();
        CuttingPlaneManager cpm = FindObjectOfType<CuttingPlaneManager>();
        if (cpm != null) cpm.ResetCrop();
    }

    private void OnBackToMainMenuPressed()
    {
        if (modelViewportController != null) modelViewportController.ResetState();
        CuttingPlaneManager cpm = FindObjectOfType<CuttingPlaneManager>();
        if (cpm != null) cpm.ResetCrop();

        if (IsConnected && !autoConnectMode)
        {
            // Tell server to stop/unload regardless of current state
            SendMessageToServer(Constants.CANCEL_LOAD);
            SendMessageToServer(Constants.UNLOAD_MODEL);
        }

        if (uiManager != null) uiManager.ShowMainMenuPanel();
    }

    public void SendMessageToServer(string message)
    {
        if (!autoConnectMode && IsConnected) ws.Send(message);
    }

    void OnDestroy()
    {
        if (ws != null)
        {
            try { ws.CloseAsync(); } catch { }
            ws = null;
        }
    }
}