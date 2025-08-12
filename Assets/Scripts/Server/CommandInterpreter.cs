using UnityEngine;
using System;
using static JsonUtilityHelper;

public class CommandInterpreter : MonoBehaviour
{
    public ModelController ModelController;
    public WebSocketServerManager WebSocketServerManager;

    void Start()
    {
        if (ModelController == null) Debug.LogWarning("[CommandInterpreter] ModelController not assigned.");
        if (WebSocketServerManager == null) Debug.LogError("[CommandInterpreter] WebSocketServerManager not assigned.");
    }

    public void InterpretAndExecute(string commandData)
    {
        string[] parts = commandData.Split(new char[] { ':' }, 2);
        string command = parts[0].ToUpperInvariant();
        string args = parts.Length > 1 ? parts[1] : null;

        switch (command)
        {
            case "UPDATE_MODEL_TRANSFORM":
                ProcessUpdateModelTransformCommand(args);
                break;

            case "LOAD_MODEL":
                ProcessLoadModelCommand(args);
                break;

            default:
                Debug.LogWarning($"[CommandInterpreter] Unknown command received: {commandData}");
                break;
        }
    }

    private void ProcessUpdateModelTransformCommand(string args)
    {
        if (ModelController == null || string.IsNullOrEmpty(args)) return;
        try
        {
            ModelTransformStateData state = JsonUtility.FromJson<ModelTransformStateData>(args);
            ModelController.ApplyWorldTransform(state.localPosition, state.localRotation, state.localScale);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CommandInterpreter] Error parsing ModelTransformStateData for UPDATE_MODEL_TRANSFORM: {ex.Message} | Args: {args}");
        }
    }

    private void ProcessLoadModelCommand(string args)
    {
        if (ModelController == null || string.IsNullOrEmpty(args)) return;

        ModelController.LoadNewModel(args);

        if (WebSocketServerManager != null)
        {
            WebSocketServerManager.SendModelSizeUpdate(ModelController.CurrentModelBoundsSize);
        }
        else
        {
            Debug.LogWarning("[CommandInterpreter] WebSocketServerManager not assigned, cannot send model size update.");
        }
    }
}