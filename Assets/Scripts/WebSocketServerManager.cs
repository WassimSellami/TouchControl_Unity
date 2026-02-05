using UnityEngine;
using WebSocketSharp;
using WebSocketSharp.Server;
using System;
using System.Net;

public class WebSocketServerManager : MonoBehaviour
{
    [Header("Server Configuration")]
    [SerializeField] private int serverPort = Constants.DEFAULT_PORT;

    [Header("Object Control")]
    [SerializeField] private ModelController modelController;
    [SerializeField] private CommandInterpreter commandInterpreter;
    [SerializeField] private Camera serverCamera;

    private WebSocketServer wsServer;

    public class ModelControlService : WebSocketBehavior
    {
        public Action<string, bool> LogCallback;
        public Action<string> ProcessCommandCallback;
        public Action<string> SendInitialDataCallback;

        protected override void OnOpen()
        {
            LogCallback?.Invoke($"[Server] Client connected: {ID}", false);
            SendInitialDataCallback?.Invoke(ID);
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
    public void BroadcastModelLoaded(string modelId)
    {
        if (wsServer == null || !wsServer.IsListening)
        {
            LogOnMainThread("Server not listening to broadcast model loaded.", true);
            return;
        }

        string message = "MODELID," + modelId;
        BroadcastToAll(message);
    }


    private void StartWebSocketServer()
    {
        wsServer = new WebSocketServer(IPAddress.Any, serverPort);
        wsServer.AddWebSocketService<ModelControlService>(Constants.SERVICE_PATH, (serviceInstance) => {
            serviceInstance.LogCallback = LogOnMainThread;
            serviceInstance.ProcessCommandCallback = ProcessReceivedCommand;
            serviceInstance.SendInitialDataCallback = SendInitialModelData;
        });

        try
        {
            wsServer.Start();
            if (wsServer.IsListening)
            {
                LogOnMainThread($"[Server] Listening on ws://{Constants.DEFAULT_IP_ADDRESS}:{wsServer.Port}{Constants.SERVICE_PATH}");
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

    private void SendInitialModelData(string clientID)
    {
        if (modelController == null) return;

        UnityMainThreadDispatcher.Instance().Enqueue(() =>
        {
            ModelMetadataList metadataList = modelController.GetAllModelsMetadata();
            string jsonData = JsonUtility.ToJson(metadataList);
            string message = $"{Constants.MODELS_LIST_UPDATE}:{jsonData}";

            SendToClient(clientID, message);
        });
    }

    private void SendToClient(string clientID, string message)
    {
        if (wsServer != null && wsServer.IsListening)
        {
            var service = wsServer.WebSocketServices[Constants.SERVICE_PATH];
            if (service != null)
            {
                service.Sessions.SendTo(message, clientID);
                LogOnMainThread($"[Server] Sent to {clientID}: {message.Substring(0, Math.Min(50, message.Length))}...");
            }
        }
    }

    public void SendModelSizeUpdate(Vector3 modelSize)
    {
        if (wsServer != null && wsServer.IsListening)
        {
            ModelBoundsSizeData sizeData = new ModelBoundsSizeData { size = modelSize };
            string jsonData = JsonUtility.ToJson(sizeData);
            string message = $"{Constants.MODEL_SIZE_UPDATE}:{jsonData}";

            BroadcastToAll(message);
        }
        else
        {
            LogOnMainThread("[Server] Not listening to broadcast model size update.", true);
        }
    }

    private void BroadcastToAll(string message)
    {
        if (wsServer != null && wsServer.IsListening)
        {
            foreach (var serviceHost in wsServer.WebSocketServices.Hosts)
            {
                if (serviceHost.Sessions.Count > 0)
                {
                    serviceHost.Sessions.Broadcast(message);
                }
            }
        }
    }
}
