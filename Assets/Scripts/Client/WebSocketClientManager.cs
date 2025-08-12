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
    [SerializeField] private MockedModelController mockedModelControllerRef;

    [Header("Networking")]
    [SerializeField] private TMP_InputField ipAddressInput;
    [SerializeField] private string defaultIpAddress = "192.168.0.35";
    [SerializeField] private int serverPort = 8070;
    [SerializeField] private string servicePath = "/Control";
    [SerializeField] private float modelUpdateRateFPS = 60f;

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
    private float timeSinceLastModelUpdate = 0f;
    private float modelUpdateInterval;

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
        if (uiManager == null) { Debug.LogError("[WSClientManager] UIManager not assigned!"); return; }
        if (mockedModelControllerRef == null) { Debug.LogError("[WSClientManager] MockedModelControllerRef not assigned!"); return; }
        if (ipAddressInput == null) { Debug.LogError("[WSClientManager] IP Address InputField not assigned!"); return; }
        if (connectButton == null) { Debug.LogError("[WSClientManager] Connect Button not assigned!"); return; }
        if (connectButtonText == null) { Debug.LogError("[WSClientManager] Connect Button Text not assigned!"); return; }
        if (statusText == null) { Debug.LogError("[WSClientManager] Status Text not assigned!"); return; }
        if (indicatorImage == null) { Debug.LogError("[WSClientManager] Indicator Image not assigned!"); return; }
        if (defaultIndicatorSprite == null) { Debug.LogError("[WSClientManager] Default Indicator Sprite not assigned!"); return; }

        if (uiManager.modelViewPanel != null) { modelViewPanelCachedRef = uiManager.modelViewPanel; }
        else { Debug.LogError("[WSClientManager] UIManager's modelViewPanel is null."); }

        if (ipAddressInput != null) ipAddressInput.text = defaultIpAddress;
        if (connectButton != null) connectButton.onClick.AddListener(AttemptConnect);
        if (loadCubeButton != null) loadCubeButton.onClick.AddListener(() => OnLoadModelSelected("CUBE"));
        if (loadCylinderButton != null) loadCylinderButton.onClick.AddListener(() => OnLoadModelSelected("CYLINDER"));
        if (backButtonFromModelView != null) backButtonFromModelView.onClick.AddListener(OnBackToMainMenuPressed);

        modelUpdateInterval = 1.0f / Mathf.Max(1f, modelUpdateRateFPS);
        uiManager.ShowConnectionPanel();
        UpdateConnectionUI(ConnectionState.IdleWaiting);
    }

    void Update()
    {
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

    public void AttemptConnect()
    {
        if (IsConnected || isAttemptingConnection) return;

        string ip = (ipAddressInput != null && !string.IsNullOrWhiteSpace(ipAddressInput.text)) ? ipAddressInput.text : defaultIpAddress;
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

    private void SendModelTransformState()
    {
        if (!IsConnected || mockedModelControllerRef == null) return;
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

    private void OnWebSocketOpen()
    {
        UpdateConnectionUI(ConnectionState.Connected);
        isAttemptingConnection = false;
        if (uiManager != null) uiManager.ShowMainMenuPanel();
    }

    private void OnWebSocketMessage(string data)
    {
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
        if (ws != null && ws.ReadyState != WebSocketState.Closed) ws.Close();
        ws = null;
        if (uiManager != null) uiManager.ShowConnectionPanel();
    }

    private void OnWebSocketClose(string reason, ushort code)
    {
        UpdateConnectionUI(ConnectionState.Disconnected);
        isAttemptingConnection = false;
        ws = null;
        if (uiManager != null) uiManager.ShowConnectionPanel();
    }

    private void OnLoadModelSelected(string modelId)
    {
        if (!IsConnected) return;
        SendMessageToServer($"LOAD_MODEL:{modelId.ToUpperInvariant()}");
        if (uiManager != null) uiManager.ShowModelViewPanel();
    }

    private void OnBackToMainMenuPressed()
    {
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
            case ConnectionState.Connected: message = "Connected to server"; indicatorColor = Color.green; buttonText = "Connect"; break;
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
        if (IsConnected) ws.Send(message);
    }

    void OnDestroy()
    {
        if (ws != null && ws.ReadyState != WebSocketState.Closed) ws.CloseAsync();
        ws = null;
    }
}