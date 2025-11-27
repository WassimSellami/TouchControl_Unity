using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using WebSocketSharp;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class WebSocketClientManager : MonoBehaviour
{
    [SerializeField] private bool autoConnectMode = false;
    [SerializeField] private UIManager uiManager;
    [SerializeField] private MockedModelController mockedModelControllerRef;
    [SerializeField] private Camera referenceCamera;

    [SerializeField] private TMP_InputField ipAddressInput;
    [SerializeField] private string defaultIpAddress = "192.168.0.83";
    [SerializeField] private int serverPort = 8070;

    [SerializeField] private Button connectButton;
    [SerializeField] private TMP_Text connectButtonText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Image indicatorImage;
    [SerializeField] private Sprite defaultIndicatorSprite;
    [SerializeField] private Button[] loadModelButtons;
    [SerializeField] private Button backButtonFromModelView;

    private GameObject modelViewPanelCachedRef;
    private WebSocket ws;
    private bool isAttemptingConnection = false;
    private bool isMockConnected = false;
    private float timeSinceLastModelUpdate = 0f;
    private float modelUpdateInterval;

    public Dictionary<GameObject, Vector3> buttonOriginalPositions = new Dictionary<GameObject, Vector3>();
    private GameObject currentDraggedButton = null;

    private GameObject glidingButton = null;
    private Vector2 buttonVelocity = Vector2.zero;
    private Vector2 lastButtonPosition;

    private List<ModelMetadata> availableModels = new List<ModelMetadata>();

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

        modelUpdateInterval = 1.0f / Mathf.Max(1f, Constants.MODEL_UPDATE_FPS);

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

        foreach (Button loadModelButton in loadModelButtons)
        {
            if (loadModelButton != null)
            {
                if (string.IsNullOrEmpty(loadModelButton.gameObject.name))
                {
                    Debug.LogWarning($"Button '{loadModelButton.name}' does not have a tag. It will not function as a model loader.");
                    continue;
                }
                SetupButtonDrag(loadModelButton.gameObject);
            }
        }
        if (backButtonFromModelView != null) backButtonFromModelView.onClick.AddListener(OnBackToMainMenuPressed);
    }

    private void SetupButtonDrag(GameObject buttonObj)
    {
        EventTrigger trigger = buttonObj.GetComponent<EventTrigger>();
        if (trigger == null) trigger = buttonObj.AddComponent<EventTrigger>();

        EventTrigger.Entry entryBeginDrag = new EventTrigger.Entry();
        entryBeginDrag.eventID = EventTriggerType.BeginDrag;
        entryBeginDrag.callback.AddListener((data) => { OnButtonDragStart((PointerEventData)data); });
        trigger.triggers.Add(entryBeginDrag);

        EventTrigger.Entry entryDrag = new EventTrigger.Entry();
        entryDrag.eventID = EventTriggerType.Drag;
        entryDrag.callback.AddListener((data) => { OnButtonDrag((PointerEventData)data); });
        trigger.triggers.Add(entryDrag);

        EventTrigger.Entry entryEndDrag = new EventTrigger.Entry();
        entryEndDrag.eventID = EventTriggerType.EndDrag;
        entryEndDrag.callback.AddListener((data) => { OnButtonDragEnd((PointerEventData)data); });
        trigger.triggers.Add(entryEndDrag);
    }

    public void OnButtonDragStart(PointerEventData eventData)
    {
        glidingButton = null;
        currentDraggedButton = eventData.pointerPress;
        lastButtonPosition = eventData.position;
        buttonVelocity = Vector2.zero;

        if (!buttonOriginalPositions.ContainsKey(currentDraggedButton))
        {
            buttonOriginalPositions[currentDraggedButton] = currentDraggedButton.transform.position;
        }
    }

    public void OnButtonDrag(PointerEventData eventData)
    {
        if (currentDraggedButton != null)
        {
            currentDraggedButton.transform.position = eventData.position;
            buttonVelocity = (eventData.position - lastButtonPosition) / Time.deltaTime;
            lastButtonPosition = eventData.position;
        }
    }

    public void OnButtonDragEnd(PointerEventData eventData)
    {
        if (currentDraggedButton != null)
        {
            glidingButton = currentDraggedButton;
            currentDraggedButton = null;
        }
    }

    void Update()
    {
        if (glidingButton != null)
        {
            HandleGlidingButton();
        }

        if (IsConnected && mockedModelControllerRef != null && modelViewPanelCachedRef != null && modelViewPanelCachedRef.activeInHierarchy)
        {
            timeSinceLastModelUpdate += Time.deltaTime;
            if (timeSinceLastModelUpdate >= modelUpdateInterval)
            {
                SendModelTransformState();
                timeSinceLastModelUpdate = 0f;
            }
        }
    }

    private void HandleGlidingButton()
    {
        glidingButton.transform.position += (Vector3)buttonVelocity * Time.deltaTime;
        buttonVelocity = Vector2.Lerp(buttonVelocity, Vector2.zero, Constants.MODEL_THUMBNAIL_GLIDE_FRICTION * Time.deltaTime);

        RectTransform buttonRect = glidingButton.GetComponent<RectTransform>();
        Vector3[] corners = new Vector3[4];
        buttonRect.GetWorldCorners(corners);

        float minX = corners[0].x;
        float maxX = corners[2].x;
        float minY = corners[0].y;
        float maxY = corners[1].y;

        bool isOffScreen = maxX < 0 || minX > Screen.width || maxY < 0 || minY > Screen.height;

        if (isOffScreen)
        {
            OnLoadModelSelected(glidingButton.name);
            ResetAndStopGlidingButton();
            return;
        }
        buttonVelocity = Vector2.ClampMagnitude(buttonVelocity, Constants.MODEL_THUMBNAIL_MAX_VELOCITY);

        if (buttonVelocity.sqrMagnitude < Constants.MODEL_THUMBNAIL_RESET_VELOCITY)
        {
            ResetAndStopGlidingButton();
        }
    }

    private void ResetAndStopGlidingButton()
    {
        if (glidingButton == null) return;
        glidingButton.transform.position = buttonOriginalPositions[glidingButton];
        glidingButton = null;
        buttonVelocity = Vector2.zero;
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

        string url = $"ws://{ip}:{serverPort}{Constants.SERVICE_PATH}";

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

    public void SendShowSliceIcon(Vector3 worldPosition)
    {
        if (!IsConnected) return;
        var data = new ShowSliceIconData { worldPosition = worldPosition };
        string jsonData = JsonUtility.ToJson(data);
        SendMessageToServer($"SHOW_SLICE_ICON:{jsonData}");
    }

    public void SendHideSliceIcon()
    {
        if (!IsConnected) return;
        SendMessageToServer("HIDE_SLICE_ICON");
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
        else if (command == "MODELS_LIST_UPDATE") ProcessModelsListUpdate(args);
        else Debug.Log($"Received unknown message from server: \"{data}\"");
    }

    private void ProcessModelsListUpdate(string args)
    {
        if (string.IsNullOrEmpty(args)) return;

        try
        {
            ModelMetadataList metadataList = JsonUtility.FromJson<ModelMetadataList>(args);
            availableModels.Clear();

            if (metadataList != null && metadataList.models != null)
            {
                foreach (var metadata in metadataList.models)
                {
                    availableModels.Add(metadata);
                }

                if (uiManager != null)
                {
                    uiManager.PopulateModelButtons(availableModels, this);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error parsing ModelMetadataList: {ex.Message} | Args: {args}");
        }
    }

    private void ProcessModelSizeUpdate(string args)
    {

        if (mockedModelControllerRef == null || string.IsNullOrEmpty(args)) return;
        try
        {
            ModelBoundsSizeData sizeData = JsonUtility.FromJson<ModelBoundsSizeData>(args);
            mockedModelControllerRef.ApplyServerModelScale(sizeData.size);
        }
        catch (Exception ex) {}
    }

    public Sprite Base64ToSprite(string base64)
    {
        if (string.IsNullOrEmpty(base64)) return null;

        try
        {
            byte[] imageBytes = Convert.FromBase64String(base64);
            Texture2D texture = new Texture2D(2, 2);
            texture.LoadImage(imageBytes);

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f)
            );

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
