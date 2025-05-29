using UnityEngine;
using System.Globalization;
using static JsonUtilityHelper;

public class CommandInterpreter : MonoBehaviour
{
    public ModelController ModelController;
    public WebSocketServerManager WebSocketServerManager;
    public CaveCameraController CaveCamera;

    private float rotationStep = 15.0f;
    private float defaultScaleFactorIncrement = 1.1f;
    private float defaultScaleFactorDecrement = 0.9f;

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

        if (command == "INITIAL_CAMERA_STATE" || command == "UPDATE_CAMERA_STATE")
        {
            if (CaveCamera == null) { Debug.LogWarning($"[CommandInterpreter] CaveCamera not assigned. Cannot process {command}."); return; }
            if (string.IsNullOrEmpty(args)) { Debug.LogWarning($"[CommandInterpreter] {command} received with no arguments."); return; }
            try
            {
                CameraStateData state = JsonUtility.FromJson<CameraStateData>(args);
                CaveCamera.ApplyState(state);
            }
            catch (System.Exception ex) { Debug.LogError($"[CommandInterpreter] Error parsing CameraStateData for {command}: {ex.Message} | Args: {args}"); }
            return;
        }

        if (command == "SET_INITIAL_MODEL_TRANSFORM")
        {
            if (ModelController == null) { Debug.LogWarning("[CommandInterpreter] ModelController not assigned. Cannot set initial model transform."); return; }
            if (string.IsNullOrEmpty(args)) { Debug.LogWarning($"[CommandInterpreter] SET_INITIAL_MODEL_TRANSFORM received with no arguments."); return; }
            try
            {
                ModelTransformStateData state = JsonUtility.FromJson<ModelTransformStateData>(args);
                ModelController.SetInitialTransform(state.localPosition, state.localRotation, state.localScale);
            }
            catch (System.Exception ex) { Debug.LogError($"[CommandInterpreter] Error parsing ModelTransformStateData for SET_INITIAL_MODEL_TRANSFORM: {ex.Message} | Args: {args}"); }
            return;
        }

        if (ModelController == null)
        {
            Debug.LogError("[CommandInterpreter] ModelController not assigned. Cannot execute model command: " + command);
            return;
        }

        switch (command)
        {
            case "ROTATE_X_NEG": ModelController.ApplyRotationDelta(new Vector3(rotationStep, 0, 0)); break;
            case "ROTATE_X_POS": ModelController.ApplyRotationDelta(new Vector3(-rotationStep, 0, 0)); break;
            case "ROTATE_Y_POS": ModelController.ApplyRotationDelta(new Vector3(0, rotationStep, 0)); break;
            case "ROTATE_Y_NEG": ModelController.ApplyRotationDelta(new Vector3(0, -rotationStep, 0)); break;
            case "ROTATE_Z_POS": ModelController.ApplyRotationDelta(new Vector3(0, 0, rotationStep)); break;
            case "ROTATE_Z_NEG": ModelController.ApplyRotationDelta(new Vector3(0, 0, -rotationStep)); break;
            case "ROTATE_BY_DELTA": ParseAndApplyRotationDelta(args); break;
            case "SET_ROTATION": ParseAndSetRotation(args); break;
            case "SCALE_UP": ModelController.ApplyScaleFactor(defaultScaleFactorIncrement); break;
            case "SCALE_DOWN": ModelController.ApplyScaleFactor(defaultScaleFactorDecrement); break;
            case "SET_SCALE": ParseAndSetScale(args); break;
            case "APPLY_SCALE_FACTOR": ParseAndApplyScaleFactor(args); break;
            case "LOAD_MODEL":
                if (!string.IsNullOrEmpty(args))
                {
                    ModelController.LoadNewModel(args);
                    if (WebSocketServerManager != null) WebSocketServerManager.BroadcastModelChangeAndInitialState(args);
                }
                else Debug.LogWarning($"[CommandInterpreter] LOAD_MODEL command missing arguments.");
                break;
            default: Debug.LogWarning($"[CommandInterpreter] Unknown command received: {commandData}"); break;
        }
    }

    private void ParseAndApplyRotationDelta(string args)
    {
        if (string.IsNullOrEmpty(args)) return;
        string[] values = args.Split(',');
        if (values.Length == 2 &&
            float.TryParse(values[0], NumberStyles.Any, CultureInfo.InvariantCulture, out float deltaX) &&
            float.TryParse(values[1], NumberStyles.Any, CultureInfo.InvariantCulture, out float deltaY))
        {
            ModelController.ApplyRotationDelta(new Vector3(-deltaY, deltaX, 0f));
        }
    }

    private void ParseAndSetRotation(string args)
    {
        if (TryParseVector3(args, out Vector3 newRotation)) ModelController.SetRotation(newRotation);
    }

    private void ParseAndSetScale(string args)
    {
        if (TryParseVector3(args, out Vector3 newScale)) ModelController.SetScale(newScale);
    }

    private void ParseAndApplyScaleFactor(string args)
    {
        if (float.TryParse(args, NumberStyles.Any, CultureInfo.InvariantCulture, out float factor)) ModelController.ApplyScaleFactor(factor);
    }

    private bool TryParseVector3(string input, out Vector3 result)
    {
        result = Vector3.zero;
        if (string.IsNullOrEmpty(input)) return false;
        string[] values = input.Split(',');
        if (values.Length == 3 &&
            float.TryParse(values[0], NumberStyles.Any, CultureInfo.InvariantCulture, out float x) &&
            float.TryParse(values[1], NumberStyles.Any, CultureInfo.InvariantCulture, out float y) &&
            float.TryParse(values[2], NumberStyles.Any, CultureInfo.InvariantCulture, out float z))
        {
            result = new Vector3(x, y, z);
            return true;
        }
        return false;
    }
}