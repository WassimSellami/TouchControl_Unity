using UnityEngine;

public class CaveCameraController : MonoBehaviour
{
    private Camera caveCamera;

    void Awake()
    {
        caveCamera = GetComponentInChildren<Camera>();
        if (caveCamera == null)
        {
            caveCamera = GetComponent<Camera>();
        }
        if (caveCamera == null)
        {
            Debug.LogError("CaveCameraController: No Camera component found on this GameObject or its children.");
            enabled = false;
            return;
        }

        if (!caveCamera.orthographic)
        {
            Debug.LogWarning("CaveCameraController: The assigned CAVE camera is not set to Orthographic projection. OrthoSize updates from client will be ignored or may have unintended effects.");
        }
    }

    public void ApplyState(CameraStateData state)
    {
        if (state == null || !enabled || caveCamera == null) return;

        transform.position = state.position;
        transform.rotation = state.rotation;

        if (caveCamera.orthographic)
        {
            // Only apply orthoSize if it's a valid positive value received from the client.
            // Client sends -1f if its camera isn't orthographic.
            if (state.orthoSize > 0f)
            {
                caveCamera.orthographicSize = state.orthoSize;
            }
        }
    }
}