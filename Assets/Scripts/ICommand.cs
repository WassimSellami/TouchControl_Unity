using UnityEngine;

public interface ICommand
{
    string ActionID { get; }
    void Execute();
    void Undo();
    void CleanUp();
}

public interface IModelViewer
{
    void LoadNewModel(string modelId);
    void ResetState();
    void SetModelVisibility(bool isVisible);
    void ApplyWorldTransform(Vector3 localPosition, Quaternion localRotation, Vector3 localScale);
}

public interface IModelManipulator : IModelViewer
{
    void StartContinuousRotation(float direction);
    void StopContinuousRotation();
    void ProcessOrbit(Vector2 screenDelta);
    void ProcessPan(Vector2 screenDelta);
    void ProcessZoom(float zoomAmount);
    void ProcessRoll(float angleDelta);
    void ResetOrbitLock();
    void TriggerPresetViewRotation(float direction);
    bool IsAutoRotating { get; }
}
