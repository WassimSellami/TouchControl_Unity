using UnityEngine;
using System;

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
            case Constants.UPDATE_MODEL_TRANSFORM:
                ProcessUpdateModelTransformCommand(args);
                break;
            case Constants.UPDATE_CAMERA_TRANSFORM:
                ProcessUpdateCameraTransformCommand(args);
                break;
            case Constants.LOAD_MODEL:
                ProcessLoadModelCommand(args);
                break;
            case Constants.UNLOAD_MODEL:
                ProcessUnloadModelCommand();
                break;
            case Constants.UPDATE_VISUAL_CROP_PLANE:
                ProcessVisualCropPlaneCommand(args);
                break;
            case Constants.EXECUTE_SLICE_ACTION:
                ProcessExecuteSliceActionCommand(args);
                break;
            case Constants.EXECUTE_DESTROY_ACTION:
                ProcessExecuteDestroyActionCommand(args);
                break;
            case Constants.START_SHAKE:
                ProcessStartShakeCommand(args);
                break;
            case Constants.STOP_SHAKE:
                ProcessStopShakeCommand(args);
                break;
            case Constants.UNDO_ACTION:
                ProcessUndoActionCommand();
                break;
            case Constants.REDO_ACTION:
                ProcessRedoActionCommand();
                break;
            case Constants.RESET_ALL:
                ProcessResetAllCommand();
                break;
            case Constants.UPDATE_CUT_LINE:
                ProcessUpdateCutLineCommand(args);
                break;
            case Constants.HIDE_CUT_LINE:
                ProcessHideCutLineCommand();
                break;
            case Constants.SHOW_SLICE_ICON:
                ProcessShowSliceIconCommand(args);
                break;
            case Constants.HIDE_SLICE_ICON:
                ProcessHideSliceIconCommand();
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
        if (ModelController == null || string.IsNullOrEmpty(args))
            return;

        ModelController.LoadNewModel(args);

        if (WebSocketServerManager != null)
        {
            WebSocketServerManager.BroadcastModelLoaded(args);
            WebSocketServerManager.SendModelSizeUpdate(ModelController.CurrentModelBoundsSize);
        }
    }


    private void ProcessUnloadModelCommand()
    {
        if (ModelController != null)
        {
            ModelController.UnloadCurrentModel();
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

    private void ProcessStartShakeCommand(string args)
    {
        if (ModelController == null || string.IsNullOrEmpty(args)) return;
        try
        {
            DestroyActionData data = JsonUtility.FromJson<DestroyActionData>(args);
            ModelController.StartShaking(data.targetPartID, data.worldPosition);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CommandInterpreter] Error parsing Shake Data: {ex.Message} | Args: {args}");
        }
    }

    private void ProcessStopShakeCommand(string args)
    {
        if (ModelController == null || string.IsNullOrEmpty(args)) return;
        try
        {
            DestroyActionData data = JsonUtility.FromJson<DestroyActionData>(args);
            ModelController.StopShaking(data.targetPartID);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CommandInterpreter] Error parsing Shake Data: {ex.Message} | Args: {args}");
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

    private void ProcessShowSliceIconCommand(string args)
    {
        if (ModelController == null || string.IsNullOrEmpty(args)) return;
        try
        {
            ShowSliceIconData data = JsonUtility.FromJson<ShowSliceIconData>(args);
            ModelController.ShowSliceIcon(data.worldPosition);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CommandInterpreter] Error parsing ShowSliceIconData: {ex.Message} | Args: {args}");
        }
    }

    private void ProcessHideSliceIconCommand()
    {
        if (ModelController != null) ModelController.HideSliceIcon();
    }
}