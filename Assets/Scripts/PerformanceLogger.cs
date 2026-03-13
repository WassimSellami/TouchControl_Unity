using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PerformanceLogger : MonoBehaviour
{
    public static PerformanceLogger Instance;

    [Header("Configuration")]
    public bool isServer = false; // Check ON for Server (PC), OFF for Client (Android)

    private List<float> fpsRecords = new List<float>();
    private List<float> latencyRecords = new List<float>();

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
    }

    private void Update()
    {
        if (isServer)
        {
            ModelController mc = FindObjectOfType<ModelController>();
            if (mc != null && mc.CurrentModelID != null)
            {
                float currentFps = 1.0f / Time.unscaledDeltaTime;
                if (currentFps > 2.0f) // Ignore heavy loading freezes
                {
                    fpsRecords.Add(currentFps);
                }
            }
        }
    }

    // Called by the Client when an ACK is received from the Server
    public void LogLatency(float latencyMs)
    {
        if (!isServer)
        {
            latencyRecords.Add(latencyMs);

            // Send the data to the Windows PC every 20 gestures!
            if (latencyRecords.Count % 20 == 0)
            {
                SendLatencyReportToServer();
            }
        }
    }

    private void SendLatencyReportToServer()
    {
        if (latencyRecords.Count == 0)
            return;

        float mean = latencyRecords.Average();
        float min = latencyRecords.Min();
        float max = latencyRecords.Max();

        float sumOfSquares = latencyRecords.Select(val => (val - mean) * (val - mean)).Sum();
        float sd = (float)Math.Sqrt(sumOfSquares / latencyRecords.Count);

        // Format the data into a single line to safely send over WebSocket
        string reportData = $"{latencyRecords.Count}|{mean:F2}|{sd:F2}|{min:F2}|{max:F2}";

        WebSocketClientManager ws = FindObjectOfType<WebSocketClientManager>();
        if (ws != null && ws.IsConnected)
        {
            ws.SendMessageToServer($"{Constants.LATENCY_REPORT}:{reportData}");
        }
    }

    private void OnApplicationQuit()
    {
        if (!isServer)
            SendLatencyReportToServer();
    }
}