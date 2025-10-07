using UnityEngine;
using WebSocketSharp;
using WebSocketSharp.Server;
using System;
using System.Net;
using static JsonUtilityHelper;

public class WebSocketServerManager : MonoBehaviour
{
    [Header("Server Configuration")]
    [SerializeField] private int serverPort = 8070;
    [SerializeField] private string servicePath = "/Control";

    [Header("Object Control")]
    [SerializeField] private ModelController modelController;
    [SerializeField] private CommandInterpreter commandInterpreter;
    [SerializeField] private Camera serverCamera;

    private WebSocketServer wsServer;

    public class ModelControlService : WebSocketBehavior
    {
        public Action<string, bool> LogCallback;
        public Action<string> ProcessCommandCallback;

        protected override void OnOpen()
        {
            LogCallback?.Invoke($"[Server] Client connected: {ID}", false);
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
        if (modelController == null) Debug.LogError("[Server] ModelController reference not set!");
        if (serverCamera == null) Debug.LogError("[Server] Server Camera reference not set!");
        if (commandInterpreter == null) Debug.LogError("[Server] CommandInterpreter reference not set!");
        else
        {
            commandInterpreter.WebSocketServerManager = this;
            commandInterpreter.ModelController = modelController;
        }

        UnityMainThreadDispatcher.Instance();

        StartWebSocketServer();
    }

    void OnDestroy()
    {
        StopWebSocketServer();
    }

    public void UpdateServerCameraTransform(Vector3 position, Quaternion rotation)
    {
        if (serverCamera != null)
        {
            serverCamera.transform.position = position;
            serverCamera.transform.rotation = rotation;
        }
    }

    private void StartWebSocketServer()
    {
        wsServer = new WebSocketServer(IPAddress.Any, serverPort);
        wsServer.AddWebSocketService<ModelControlService>(servicePath, (serviceInstance) => {
            serviceInstance.LogCallback = LogOnMainThread;
            serviceInstance.ProcessCommandCallback = ProcessReceivedCommand;
        });

        try
        {
            wsServer.Start();
            if (wsServer.IsListening)
            {
                LogOnMainThread($"[Server] Listening on ws://YOUR_IP:{wsServer.Port}{servicePath}");
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
            wsServer = null;
        }
    }

    private void ProcessReceivedCommand(string command)
    {
        if (commandInterpreter == null) return;
        UnityMainThreadDispatcher.Instance().Enqueue(() => commandInterpreter.InterpretAndExecute(command));
    }

    private void LogOnMainThread(string message, bool isError = false)
    {
        UnityMainThreadDispatcher.Instance().Enqueue(() => {
            if (isError) Debug.LogError(message);
            else Debug.Log(message);
        });
    }

    public void SendModelSizeUpdate(Vector3 modelSize)
    {
        if (wsServer != null && wsServer.IsListening)
        {
            ModelBoundsSizeData sizeData = new ModelBoundsSizeData { size = modelSize };
            string jsonData = JsonUtility.ToJson(sizeData);
            string message = $"MODEL_SIZE_UPDATE:{jsonData}";

            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                foreach (var serviceHost in wsServer.WebSocketServices.Hosts)
                {
                    if (serviceHost.Sessions.Count > 0)
                    {
                        serviceHost.Sessions.Broadcast(message);
                    }
                }
            });
        }
        else
        {
            LogOnMainThread("[Server] Not listening to broadcast model size update.", true);
        }
    }
}