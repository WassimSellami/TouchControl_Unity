using UnityEngine;
using WebSocketSharp;
using WebSocketSharp.Server;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Globalization;

public class WebSocketServerManager : MonoBehaviour
{
    [Header("Server Configuration")]
    [SerializeField] private int serverPort;
    [SerializeField] private string servicePath;
    [SerializeField] private float updateRateFPS;

    [Header("Object Control")]
    [SerializeField] private ModelController modelController;
    [SerializeField] private CommandInterpreter commandInterpreter;

    private WebSocketServer wsServer;
    private readonly ConcurrentQueue<Action> mainThreadActions = new ConcurrentQueue<Action>();

    private Vector3 _latestModelRotation;
    private Vector3 _latestModelScale;
    private bool _isModelActive = false;

    private float _updateInterval;
    private float _timeSinceLastUpdate = 0f;

    public class ModelControlService : WebSocketBehavior
    {
        public Action<string, bool> LogCallback;
        public Action<string> ProcessCommandCallback;
        public Func<(Vector3 rotation, Vector3 scale, bool available)> GetInitialStateCallback;
        public Func<string> GetCurrentModelIdCallback;

        protected override void OnOpen()
        {
            LogCallback?.Invoke($"[Server] Client connected: {ID}", false);

            string currentModelId = GetCurrentModelIdCallback?.Invoke();
            if (!string.IsNullOrEmpty(currentModelId))
            {
                Send($"MODEL_LOADED:{currentModelId}");
                LogCallback?.Invoke($"[Server] Sent current model ID ({currentModelId}) to {ID}", false);
            }

            if (GetInitialStateCallback != null)
            {
                var initialState = GetInitialStateCallback();
                if (initialState.available)
                {
                    string rotMsg = $"INITIAL_STATE_ROT:{initialState.rotation.x.ToString(CultureInfo.InvariantCulture)},{initialState.rotation.y.ToString(CultureInfo.InvariantCulture)},{initialState.rotation.z.ToString(CultureInfo.InvariantCulture)}";
                    Send(rotMsg);

                    string scaleMsg = $"INITIAL_STATE_SCALE:{initialState.scale.x.ToString(CultureInfo.InvariantCulture)},{initialState.scale.y.ToString(CultureInfo.InvariantCulture)},{initialState.scale.z.ToString(CultureInfo.InvariantCulture)}";
                    Send(scaleMsg);

                    LogCallback?.Invoke($"[Server] Sent initial transform state to {ID}", false);
                }
                else
                {
                    LogCallback?.Invoke($"[Server] No model currently active or ready. Not sending initial transform state.", false);
                }
            }
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            ProcessCommandCallback?.Invoke(e.Data);
        }

        protected override void OnClose(CloseEventArgs e)
        {
            LogCallback?.Invoke($"[Server] Client disconnected: {ID}. Code: {e.Code}, Reason: {e.Reason}", false);
        }

        protected override void OnError(ErrorEventArgs e)
        {
            LogCallback?.Invoke($"[Server] Error for {ID}: {e.Message}", true);
        }
    }

    void Start()
    {
        if (updateRateFPS <= 0) updateRateFPS = 30;
        _updateInterval = 1.0f / updateRateFPS;

        if (modelController == null)
        {
            Debug.LogError("[Server] ModelController reference not set in the Inspector!");
        }
        if (commandInterpreter == null)
        {
            Debug.LogError("[Server] CommandInterpreter reference not set in the Inspector!");
        }
        else
        {
            if (commandInterpreter.WebSocketServerManager == null)
            {
                commandInterpreter.WebSocketServerManager = this;
            }
        }
        StartWebSocketServer();
    }

    void Update()
    {
        while (mainThreadActions.TryDequeue(out Action action))
        {
            action?.Invoke();
        }

        Transform currentModelTransform = modelController?.GetCurrentModelTransform();
        if (currentModelTransform != null)
        {
            _latestModelRotation = currentModelTransform.eulerAngles;
            _latestModelScale = currentModelTransform.localScale;
            _isModelActive = true;
        }
        else
        {
            _isModelActive = false;
        }

        _timeSinceLastUpdate += Time.deltaTime;
        if (_timeSinceLastUpdate >= _updateInterval)
        {
            _timeSinceLastUpdate -= _updateInterval;
            if (_timeSinceLastUpdate < 0) _timeSinceLastUpdate = 0;

            if (_isModelActive)
            {
                SendPeriodicUpdates();
            }
        }
    }

    void OnDestroy()
    {
        StopWebSocketServer();
    }

    private void LogOnMainThread(string message, bool isError = false)
    {
        QueueMainThreadAction(() => {
            if (isError) Debug.LogError(message);
            else Debug.Log(message);
        });
    }

    private void QueueMainThreadAction(Action action)
    {
        mainThreadActions.Enqueue(action);
    }

    private string GetCurrentModelId()
    {
        return modelController?.CurrentModelID;
    }

    private void StartWebSocketServer()
    {
        wsServer = new WebSocketServer(IPAddress.Any, serverPort);
        wsServer.AddWebSocketService<ModelControlService>(servicePath, (serviceInstance) => {
            serviceInstance.LogCallback = LogOnMainThread;
            serviceInstance.ProcessCommandCallback = ProcessReceivedCommand;
            serviceInstance.GetInitialStateCallback = () => (_latestModelRotation, _latestModelScale, _isModelActive);
            serviceInstance.GetCurrentModelIdCallback = GetCurrentModelId;
        });

        try
        {
            wsServer.Start();
            if (wsServer.IsListening)
            {
                LogOnMainThread($"[Server] Listening on port {wsServer.Port}{servicePath}. Connect clients to ws://YOUR_MACHINE_IP:{wsServer.Port}{servicePath}");
            }
            else
            {
                LogOnMainThread("[Server] Failed to start.", true);
            }
        }
        catch (Exception ex)
        {
            LogOnMainThread($"[Server] Exception on start: {ex.Message}", true);
        }
    }

    private void StopWebSocketServer()
    {
        if (wsServer != null)
        {
            wsServer.Stop();
            LogOnMainThread("[Server] Stopped.");
            wsServer = null;
        }
    }

    private void ProcessReceivedCommand(string command)
    {
        if (commandInterpreter == null || modelController == null)
        {
            LogOnMainThread("[Server] Dependencies not assigned. Cannot process command.", true);
            return;
        }
        QueueMainThreadAction(() => {
            commandInterpreter.InterpretAndExecute(command);
        });
    }

    public void BroadcastModelChangeAndInitialState(string modelId)
    {
        if (wsServer == null || !wsServer.IsListening) return;

        string modelChangedMsg = $"MODEL_LOADED:{modelId.ToUpperInvariant()}";
        wsServer.WebSocketServices[servicePath]?.Sessions.Broadcast(modelChangedMsg);
        LogOnMainThread($"[Server] Broadcasted model change: {modelId}", false);

        Transform currentModelTransform = modelController?.GetCurrentModelTransform();
        if (currentModelTransform != null)
        {
            _latestModelRotation = currentModelTransform.eulerAngles;
            _latestModelScale = currentModelTransform.localScale;

            string rotMsg = $"INITIAL_STATE_ROT:{_latestModelRotation.x.ToString(CultureInfo.InvariantCulture)},{_latestModelRotation.y.ToString(CultureInfo.InvariantCulture)},{_latestModelRotation.z.ToString(CultureInfo.InvariantCulture)}";
            string scaleMsg = $"INITIAL_STATE_SCALE:{_latestModelScale.x.ToString(CultureInfo.InvariantCulture)},{_latestModelScale.y.ToString(CultureInfo.InvariantCulture)},{_latestModelScale.z.ToString(CultureInfo.InvariantCulture)}";

            wsServer.WebSocketServices[servicePath]?.Sessions.Broadcast(rotMsg);
            wsServer.WebSocketServices[servicePath]?.Sessions.Broadcast(scaleMsg);
            LogOnMainThread($"[Server] Broadcasted new initial state for model {modelId}", false);
        }
        else
        {
            LogOnMainThread("[Server] No active model after change, cannot send new initial state.", true);
        }
    }

    private void SendPeriodicUpdates()
    {
        if (wsServer == null || !wsServer.IsListening || !_isModelActive)
        {
            return;
        }

        string rotMsg = $"UPDATE_ROT:{_latestModelRotation.x.ToString(CultureInfo.InvariantCulture)},{_latestModelRotation.y.ToString(CultureInfo.InvariantCulture)},{_latestModelRotation.z.ToString(CultureInfo.InvariantCulture)}";
        string scaleMsg = $"UPDATE_SCALE:{_latestModelScale.x.ToString(CultureInfo.InvariantCulture)},{_latestModelScale.y.ToString(CultureInfo.InvariantCulture)},{_latestModelScale.z.ToString(CultureInfo.InvariantCulture)}";

        wsServer.WebSocketServices[servicePath]?.Sessions.Broadcast(rotMsg);
        wsServer.WebSocketServices[servicePath]?.Sessions.Broadcast(scaleMsg);
    }
}