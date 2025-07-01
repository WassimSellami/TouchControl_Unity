// Assets/Scripts/WebSocketServerManager2.cs

using UnityEngine;
using WebSocketSharp;      // Import the websocket-sharp library
using WebSocketSharp.Server; // Import the server-specific classes
using System.Collections.Concurrent; // For thread-safe queue
using System;             // For Action event
using System.Net;         // For IPAddress
// No need for System.Globalization for this specific use case,
// but keep it if your model controller/command interpreter might use it.

public class WebSocketServerManager2 : MonoBehaviour
{
    [Header("WebSocket Server Settings")]
    [SerializeField] private int _port = 8080;
    [SerializeField] private string _servicePath = "/CubeControl"; // Good practice to make this configurable
    private WebSocketServer _wss;

    // A thread-safe queue to store commands received from WebSocket (processed by CubeController)
    private ConcurrentQueue<string> _commandQueue = new ConcurrentQueue<string>();

    // A thread-safe queue for actions that need to be executed on Unity's main thread (e.g., logging)
    private readonly ConcurrentQueue<Action> _mainThreadActions = new ConcurrentQueue<Action>();

    // Event to notify other scripts (like CubeController) about new commands
    public event Action<string> OnCommandReceived;

    void Awake()
    {
        // _commandQueue and _mainThreadActions are initialized via 'readonly' or constructor
    }

    void Start()
    {
        SetupWebSocketServer();
    }

    private void SetupWebSocketServer()
    {
        try
        {
            _wss = new WebSocketServer(IPAddress.Any, _port);

            // Add the WebSocket service for the specified path
            _wss.AddWebSocketService<CubeControlService>(_servicePath, (serviceInstance) => {
                // Pass callback methods from the manager (main thread) to the service (WebSocket thread)
                serviceInstance.LogCallback = LogOnMainThread;
                serviceInstance.ProcessCommandCallback = EnqueueCommand; // Service calls this to send command to manager's queue
            });

            // Set up server-level logging to go through our main thread logger
            _wss.Log.Level = LogLevel.Debug;
            _wss.Log.Output = (data, s) => LogOnMainThread($"[WebSocket Server Log] {data.Message}");

            // Start the server
            _wss.Start();
            if (_wss.IsListening)
            {
                LogOnMainThread($"[Server] Listening on ws://[Any]:{_port}{_servicePath}. Connect clients to ws://YOUR_MACHINE_IP:{_port}{_servicePath}");
            }
            else
            {
                LogOnMainThread("[Server] Failed to start.", true);
            }
        }
        catch (Exception ex)
        {
            LogOnMainThread($"Failed to start WebSocket server: {ex.Message}", true);
        }
    }

    // This method is called by the CubeControlService (on a separate thread) to enqueue a command.
    private void EnqueueCommand(string command)
    {
        _commandQueue.Enqueue(command);
        LogOnMainThread($"[WS Manager] Enqueued command: {command}");
    }

    // Queues an action to be executed on Unity's main thread.
    private void QueueMainThreadAction(Action action)
    {
        _mainThreadActions.Enqueue(action);
    }

    // Logs a message to Unity's console, ensuring it happens on the main thread.
    private void LogOnMainThread(string message, bool isError = false)
    {
        QueueMainThreadAction(() => {
            if (isError) Debug.LogError(message);
            else Debug.Log(message);
        });
    }

    void Update()
    {
        // Process actions from the main thread action queue first
        while (_mainThreadActions.TryDequeue(out Action action))
        {
            action?.Invoke();
        }

        // Then process commands from the command queue on Unity's main thread
        while (_commandQueue.TryDequeue(out string command))
        {
            OnCommandReceived?.Invoke(command); // Notify subscribers (like CubeController)
            // LogOnMainThread($"[WS Manager] Dequeued and processed command: {command}"); // Already logged when enqueued
        }
    }

    void OnDestroy()
    {
        // Stop the WebSocket server when the GameObject is destroyed or application quits
        if (_wss != null)
        {
            _wss.Stop();
            LogOnMainThread("WebSocket server stopped.");
        }
    }

    // --- Inner class for WebSocket Service ---
    // This class handles individual client connections and messages for a specific path.
    public class CubeControlService : WebSocketBehavior
    {
        // Callbacks provided by the WebSocketServerManager2 instance.
        // These allow the service (running on a WebSocket thread) to communicate back to the manager (main thread).
        public Action<string, bool> LogCallback;
        public Action<string> ProcessCommandCallback;

        protected override void OnOpen()
        {
            // FIX: Using ID from WebSocketBehavior as per your working example
            // This avoids the problematic 'Context' property.
            LogCallback?.Invoke($"[WS Service] Client connected: {ID}", false);
            Send("Hello from Unity WebSocket Server!"); // Send a welcome message to the client
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            LogCallback?.Invoke($"[WS Service] Received message: {e.Data}", false);
            // Pass the received message to the manager's queue for main thread processing
            ProcessCommandCallback?.Invoke(e.Data);
        }

        protected override void OnError(ErrorEventArgs e)
        {
            LogCallback?.Invoke($"[WS Service] Error for {ID}: {e.Message}. Exception: {e.Exception?.Message}", true);
        }

        protected override void OnClose(CloseEventArgs e)
        {
            LogCallback?.Invoke($"[WS Service] Client disconnected: {ID}. Code: {e.Code}, Reason: {e.Reason}", false);
        }
    }
}