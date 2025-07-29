using UnityEngine;
using System;
using static JsonUtilityHelper;

public class CommandInterpreter : MonoBehaviour
{
    public ModelController ModelController;
    public WebSocketServerManager WebSocketServerManager;
    public CaveCameraController CaveCamera;

    void Start()
    {
        if (ModelController == null) Debug.LogWarning("[CommandInterpreter] ModelController not assigned.");
        if (WebSocketServerManager == null) Debug.LogError("[CommandInterpreter] WebSocketServerManager not assigned.");
        if (CaveCamera == null) Debug.LogError("[CommandInterpreter] CaveCamera not assigned.");
    }

    public void InterpretAndExecute(string commandData)
    {
        string[] parts = commandData.Split(new char[] { ':' }, 2);
        string command = parts[0].ToUpperInvariant();
        string args = parts.Length > 1 ? parts[1] : null;

        switch (command)
        {
            case "INITIAL_CAMERA_STATE":
            case "UPDATE_CAMERA_STATE":
                ProcessCameraStateCommand(command, args);
                break;

            case "SET_INITIAL_MODEL_TRANSFORM":
                ProcessSetInitialModelTransformCommand(args);
                break;

            case "LOAD_MODEL":
                ProcessLoadModelCommand(args);
                break;

            default:
                Debug.LogWarning($"[CommandInterpreter] Unknown command received: {commandData}");
                break;
        }
    }

    private void ProcessCameraStateCommand(string command, string args)
    {
        if (CaveCamera == null)
        {
            Debug.LogWarning($"[CommandInterpreter] CaveCamera not assigned. Cannot process {command}.");
            return;
        }
        if (string.IsNullOrEmpty(args))
        {
            Debug.LogWarning($"[CommandInterpreter] {command} received with no arguments.");
            return;
        }
        try
        {
            CameraStateData state = JsonUtility.FromJson<CameraStateData>(args);
            CaveCamera.ApplyState(state);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CommandInterpreter] Error parsing CameraStateData for {command}: {ex.Message} | Args: {args}");
        }
    }

    private void ProcessSetInitialModelTransformCommand(string args)
    {
        if (ModelController == null)
        {
            Debug.LogWarning("[CommandInterpreter] ModelController not assigned. Cannot set initial model transform.");
            return;
        }
        if (string.IsNullOrEmpty(args))
        {
            Debug.LogWarning($"[CommandInterpreter] SET_INITIAL_MODEL_TRANSFORM received with no arguments.");
            return;
        }
        try
        {
            ModelTransformStateData state = JsonUtility.FromJson<ModelTransformStateData>(args);
            ModelController.SetInitialTransform(state.localPosition, state.localRotation, state.localScale);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CommandInterpreter] Error parsing ModelTransformStateData for SET_INITIAL_MODEL_TRANSFORM: {ex.Message} | Args: {args}");
        }
    }

    private void ProcessLoadModelCommand(string args)
    {
        if (ModelController == null)
        {
            Debug.LogError("[CommandInterpreter] ModelController not assigned. Cannot execute LOAD_MODEL command.");
            return;
        }
        if (!string.IsNullOrEmpty(args))
        {
            ModelController.LoadNewModel(args);
        }
        else
        {
            Debug.LogWarning($"[CommandInterpreter] LOAD_MODEL command missing arguments.");
        }
    }
}