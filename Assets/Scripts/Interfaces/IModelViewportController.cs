using UnityEngine;

/// <summary>
/// Interface for a model viewport controller that handles camera-like interactions
/// (orbit, pan, zoom, roll) and model state management.
/// </summary>
public interface IModelViewportController
{
    /// <summary>
    /// Is the model currently auto-rotating?
    /// </summary>
    bool IsAutoRotating { get; }

    /// <summary>
    /// Start continuous rotation in a given direction.
    /// </summary>
    void StartContinuousRotation(float direction);

    /// <summary>
    /// Stop continuous rotation.
    /// </summary>
    void StopContinuousRotation();

    /// <summary>
    /// Process orbit/rotation gesture (single-finger drag).
    /// </summary>
    void ProcessOrbit(Vector2 screenDelta);

    /// <summary>
    /// Process pan gesture (multi-finger drag).
    /// </summary>
    void ProcessPan(Vector2 screenDelta);

    /// <summary>
    /// Process zoom gesture (pinch or scroll).
    /// </summary>
    void ProcessZoom(float zoomAmount);

    /// <summary>
    /// Process roll/rotation gesture (two-finger rotate).
    /// </summary>
    void ProcessRoll(float angleDelta);

    /// <summary>
    /// Reset orbit lock (used internally for gesture handling).
    /// </summary>
    void ResetOrbitLock();

    /// <summary>
    /// Reset to initial state (position, rotation, scale).
    /// </summary>
    void ResetState();

    /// <summary>
    /// Apply a world transform (position, rotation, scale).
    /// </summary>
    void ApplyWorldTransform(Vector3 localPosition, Quaternion localRotation, Vector3 localScale);

    /// <summary>
    /// Set model visibility.
    /// </summary>
    void SetModelVisibility(bool isVisible);

    /// <summary>
    /// Trigger a preset view rotation animation.
    /// </summary>
    void TriggerPresetViewRotation(float direction);
}
