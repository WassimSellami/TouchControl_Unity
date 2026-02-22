using UnityEngine;
using System;
public class CommandInterpreter : MonoBehaviour
{
    public ModelController ModelController;
    public WebSocketServerManager WebSocketServerManager;
    [SerializeField] private ServerModelUIPanel serverUIPanel;

    private int updateCounter = 0;
    private float fpsTimer = 0f;
    private int lastMeasuredFps = 0;

    void Start()
    {
        if (ModelController == null) Debug.LogWarning("[CommandInterpreter] ModelController not assigned.");
        if (WebSocketServerManager == null) Debug.LogError("[CommandInterpreter] WebSocketServerManager not assigned.");
        if (serverUIPanel == null) serverUIPanel = FindObjectOfType<ServerModelUIPanel>();
    }

    void Update()
    {
        fpsTimer += Time.deltaTime;

        // Every 1 second, report the count and reset
        if (fpsTimer >= 1.0f)
        {
            lastMeasuredFps = updateCounter;

            if (lastMeasuredFps > 0) // Only log if we are actually receiving data
            {
                Debug.Log($"<color=cyan>[Server FPS]</color> Receiving transform updates at: <b>{lastMeasuredFps} FPS</b>");
            }

            updateCounter = 0;
            fpsTimer = 0f;
        }
    }

    public void InterpretAndExecute(string commandData)
    {
        string[] parts = commandData.Split(new char[] { ':' }, 2);
        string command = parts[0].ToUpperInvariant();
        string args = parts.Length > 1 ? parts[1] : null;

        switch (command)
        {
            case Constants.UPDATE_MODEL_TRANSFORM:
                updateCounter++;
                ProcessUpdateModelTransformCommand(args);
                break;

            case Constants.LOAD_MODEL:
                if (serverUIPanel != null)
                {
                    serverUIPanel.SetListVisibility(false);
                }
                ProcessLoadModelCommand(args);
                break;
            case Constants.UNLOAD_MODEL:
                if (serverUIPanel != null)
                {
                    serverUIPanel.SetListVisibility(true);
                }
                ProcessUnloadModelCommand();
                break;
            case Constants.UPDATE_CAMERA_TRANSFORM: ProcessUpdateCameraTransformCommand(args); break;
            case Constants.UPDATE_VISUAL_CROP_PLANE: ProcessVisualCropPlaneCommand(args); break;
            case Constants.EXECUTE_SLICE_ACTION: ProcessExecuteSliceActionCommand(args); break;
            case Constants.EXECUTE_DESTROY_ACTION: ProcessExecuteDestroyActionCommand(args); break;
            case Constants.START_SHAKE: ProcessStartShakeCommand(args); break;
            case Constants.STOP_SHAKE: ProcessStopShakeCommand(args); break;
            case Constants.UNDO_ACTION: ProcessUndoActionCommand(); break;
            case Constants.REDO_ACTION: ProcessRedoActionCommand(); break;
            case Constants.RESET_ALL: ProcessResetAllCommand(); break;
            case Constants.UPDATE_CUT_LINE: ProcessUpdateCutLineCommand(args); break;
            case Constants.HIDE_CUT_LINE: ProcessHideCutLineCommand(); break;
            case Constants.SHOW_SLICE_ICON: ProcessShowSliceIconCommand(args); break;
            case Constants.HIDE_SLICE_ICON: ProcessHideSliceIconCommand(); break;
            case Constants.TOGGLE_AXES:
                if (ModelController != null && args != null) ModelController.SetAxesVisibility(bool.Parse(args));
                break;
            default:
                Debug.LogWarning($"[CommandInterpreter] Unknown command: {commandData}");
                break;
        }
    }

    private void ProcessUpdateModelTransformCommand(string args)
    {
        if (ModelController == null || string.IsNullOrEmpty(args)) return;
        try { ModelTransformStateData state = JsonUtility.FromJson<ModelTransformStateData>(args); ModelController.ApplyWorldTransform(state.localPosition, state.localRotation, state.localScale); }
        catch (Exception ex) { Debug.LogError(ex.Message); }
    }

    private void ProcessUpdateCameraTransformCommand(string args)
    {
        if (WebSocketServerManager == null || string.IsNullOrEmpty(args)) return;
        try { ClientCameraStateData state = JsonUtility.FromJson<ClientCameraStateData>(args); WebSocketServerManager.UpdateServerCameraTransform(state.position, state.rotation); }
        catch (Exception ex) { Debug.LogError(ex.Message); }
    }

    private void ProcessLoadModelCommand(string args)
    {
        if (ModelController == null || string.IsNullOrEmpty(args)) return;
        ModelController.LoadNewModel(args);
        if (WebSocketServerManager != null) { WebSocketServerManager.BroadcastModelLoaded(args); WebSocketServerManager.SendModelSizeUpdate(ModelController.CurrentModelBoundsSize); }
    }

    private void ProcessUnloadModelCommand() { if (ModelController != null) ModelController.UnloadCurrentModel(); }
    private void ProcessVisualCropPlaneCommand(string args) { try { VisualCropPlaneData d = JsonUtility.FromJson<VisualCropPlaneData>(args); ModelController.UpdateVisualCropPlane(d.position, d.normal, d.scale); } catch { } }
    private void ProcessExecuteSliceActionCommand(string args) { try { SliceActionData d = JsonUtility.FromJson<SliceActionData>(args); ModelController.ExecuteSlice(d); } catch { } }
    private void ProcessExecuteDestroyActionCommand(string args) { try { DestroyActionData d = JsonUtility.FromJson<DestroyActionData>(args); ModelController.ExecuteDestroy(d); } catch { } }
    private void ProcessStartShakeCommand(string args) { try { DestroyActionData d = JsonUtility.FromJson<DestroyActionData>(args); ModelController.StartShaking(d.targetPartID, d.worldPosition); } catch { } }
    private void ProcessStopShakeCommand(string args) { try { DestroyActionData d = JsonUtility.FromJson<DestroyActionData>(args); ModelController.StopShaking(d.targetPartID); } catch { } }
    private void ProcessUndoActionCommand() { if (ModelController != null) ModelController.UndoLastAction(); }
    private void ProcessRedoActionCommand() { if (ModelController != null) ModelController.RedoLastAction(); }
    private void ProcessResetAllCommand() { if (ModelController != null) ModelController.ResetCrop(); }
    private void ProcessUpdateCutLineCommand(string args) { try { LineData d = JsonUtility.FromJson<LineData>(args); ModelController.UpdateCutLine(d.start, d.end); } catch { } }
    private void ProcessHideCutLineCommand() { if (ModelController != null) ModelController.HideCutLine(); }
    private void ProcessShowSliceIconCommand(string args) { try { ShowSliceIconData d = JsonUtility.FromJson<ShowSliceIconData>(args); ModelController.ShowSliceIcon(d.worldPosition); } catch { } }
    private void ProcessHideSliceIconCommand() { if (ModelController != null) ModelController.HideSliceIcon(); }
}
