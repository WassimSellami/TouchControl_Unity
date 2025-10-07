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
            case "UPDATE_CAMERA_TRANSFORM":
                ProcessUpdateCameraTransformCommand(args);
                break;
            case "LOAD_MODEL":
                ProcessLoadModelCommand(args);
                break;
            case "UPDATE_VISUAL_CROP_PLANE":
                ProcessVisualCropPlaneCommand(args);
                break;
            case "EXECUTE_SLICE_ACTION":
                ProcessExecuteSliceActionCommand(args);
                break;
            case "EXECUTE_DESTROY_ACTION":
                ProcessExecuteDestroyActionCommand(args);
                break;
            case "UNDO_ACTION":
                ProcessUndoActionCommand();
                break;
            case "REDO_ACTION":
                ProcessRedoActionCommand();
                break;
            case "RESET_ALL":
                ProcessResetAllCommand();
                break;
            case "UPDATE_CUT_LINE":
                ProcessUpdateCutLineCommand(args);
                break;
            case "HIDE_CUT_LINE":
                ProcessHideCutLineCommand();
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
            Debug.LogError($"[CommandInterpreter] Error parsing ModelTransformStateData: {ex.Message} | Args: {args}");
        }
    }

    private void ProcessUpdateCameraTransformCommand(string args)
    {
        if (WebSocketServerManager == null || string.IsNullOrEmpty(args)) return;
        try
        {
            ClientCameraStateData state = JsonUtility.FromJson<ClientCameraStateData>(args);
            WebSocketServerManager.UpdateServerCameraTransform(state.position, state.rotation);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CommandInterpreter] Error parsing ClientCameraStateData: {ex.Message} | Args: {args}");
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
    }

    private void ProcessVisualCropPlaneCommand(string args)
    {
        if (ModelController == null || string.IsNullOrEmpty(args)) return;
        try
        {
            VisualCropPlaneData data = JsonUtility.FromJson<VisualCropPlaneData>(args);
            ModelController.UpdateVisualCropPlane(data.position, data.normal, data.scale);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CommandInterpreter] Error parsing VisualCropPlaneData: {ex.Message} | Args: {args}");
        }
    }

    private void ProcessExecuteSliceActionCommand(string args)
    {
        if (ModelController == null || string.IsNullOrEmpty(args)) return;
        try
        {
            SliceActionData data = JsonUtility.FromJson<SliceActionData>(args);
            ModelController.ExecuteSlice(data);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CommandInterpreter] Error parsing SliceActionData: {ex.Message} | Args: {args}");
        }
    }

    private void ProcessExecuteDestroyActionCommand(string args)
    {
        if (ModelController == null || string.IsNullOrEmpty(args)) return;
        try
        {
            DestroyActionData data = JsonUtility.FromJson<DestroyActionData>(args);
            ModelController.ExecuteDestroy(data);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CommandInterpreter] Error parsing DestroyActionData: {ex.Message} | Args: {args}");
        }
    }

    private void ProcessUndoActionCommand()
    {
        if (ModelController != null) ModelController.UndoLastAction();
    }

    private void ProcessRedoActionCommand()
    {
        if (ModelController != null) ModelController.RedoLastAction();
    }

    private void ProcessResetAllCommand()
    {
        if (ModelController != null) ModelController.ResetCrop();
    }

    private void ProcessUpdateCutLineCommand(string args)
    {
        if (ModelController == null || string.IsNullOrEmpty(args)) return;
        try
        {
            LineData data = JsonUtility.FromJson<LineData>(args);
            ModelController.UpdateCutLine(data.start, data.end);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CommandInterpreter] Error parsing LineData: {ex.Message} | Args: {args}");
        }
    }

    private void ProcessHideCutLineCommand()
    {
        if (ModelController == null) return;
        ModelController.HideCutLine();
    }
}