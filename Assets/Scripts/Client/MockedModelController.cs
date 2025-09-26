using UnityEngine;
using System.Collections.Generic;

public class MockedModelController : MonoBehaviour
{
    [Header("Control Settings")]
    [SerializeField] private float panSensitivity = 0.01f;
    [SerializeField] private float orbitSensitivity = 0.5f;
    [SerializeField] private float zoomSensitivity = 0.1f;
    [SerializeField] private float scaleMin = 0.1f;
    [SerializeField] private float scaleMax = 10.0f;
    [SerializeField] private float presetViewRotationStep = 45f;

    [Header("References")]
    [SerializeField] private GameObject mockVisualModel;
    [SerializeField] private Camera referenceCamera;

    [Header("Axis Visuals")]
    [SerializeField] private Vector3 axisOriginOffset = new Vector3(0f, 0f, 0f);
    [SerializeField] private float axisLength = 10f; // A larger, more visible default length
    [SerializeField] private float axisThickness = 0.03f;
    [SerializeField] private float arrowheadRadiusFactor = 2.5f;
    [SerializeField] private float arrowheadHeightFactor = 3f;

    [Header("Axis Materials")]
    [SerializeField] private Material redAxisMaterial;
    [SerializeField] private Material greenAxisMaterial;
    [SerializeField] private Material blueAxisMaterial;

    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private Vector3 initialScale;

    private List<GameObject> axisVisuals = new List<GameObject>();
    private bool axesCreated = false;

    // FINAL: This container will be a SIBLING to the model, not a child.
    private GameObject axesContainer;
    private Transform modelReferencePoint;

    void Awake()
    {
        initialPosition = transform.position;
        initialRotation = transform.rotation;
        initialScale = transform.localScale;

        if (mockVisualModel == null) Debug.LogWarning("[MockedModelController] Mock Visual Model not assigned.");
        if (redAxisMaterial == null || greenAxisMaterial == null || blueAxisMaterial == null) Debug.LogError("[MockedModelController] Axis materials are not assigned in the Inspector!");
        if (referenceCamera == null) Debug.LogError("[MockedModelController] Reference Camera not assigned! Panning will not work.");

        // Create the container and parent it to this controller. It will live alongside the visual model.
        axesContainer = new GameObject("Client_Axes_Container");
        axesContainer.transform.SetParent(this.transform, false);
    }

    // FINAL: Use LateUpdate to sync the axes' position/rotation to the reference point AFTER all scaling has been calculated for the frame.
    void LateUpdate()
    {
        if (axesContainer != null && modelReferencePoint != null)
        {
            // This ensures the axes origin is always perfectly at the reference point's
            // world position and rotation, even after the model has been scaled non-uniformly.
            axesContainer.transform.position = modelReferencePoint.position;
            axesContainer.transform.rotation = modelReferencePoint.rotation;
        }
    }

    public void ProcessOrbit(Vector2 screenDelta)
    {
        float horizontalInput = -screenDelta.x * orbitSensitivity;
        transform.Rotate(Vector3.up, horizontalInput, Space.World);
    }

    public void ProcessPan(Vector2 screenDelta)
    {
        if (referenceCamera == null) return;
        Vector3 right = referenceCamera.transform.right * -screenDelta.x;
        Vector3 up = referenceCamera.transform.up * -screenDelta.y;
        Vector3 worldDelta = (right + up) * panSensitivity;
        transform.Translate(worldDelta, Space.World);
    }

    public void ProcessZoom(float zoomAmount)
    {
        if (zoomAmount == 0) return;
        float scaleChange = 1.0f + (zoomAmount * zoomSensitivity);
        Vector3 newScale = transform.localScale * scaleChange;
        newScale.x = Mathf.Clamp(newScale.x, scaleMin, scaleMax);
        newScale.y = Mathf.Clamp(newScale.y, scaleMin, scaleMax);
        newScale.z = Mathf.Clamp(newScale.z, scaleMin, scaleMax);
        // This scale change affects both the model AND the axesContainer, which is correct for zooming.
        transform.localScale = newScale;
    }

    public void ResetState()
    {
        transform.position = initialPosition;
        transform.rotation = initialRotation;
        transform.localScale = initialScale;
        EnsureAxisVisualsAreCreated();
    }

    public void CyclePresetView()
    {
        transform.Rotate(Vector3.up, presetViewRotationStep, Space.World);
    }

    public void EnsureAxisVisualsAreCreated()
    {
        if (!this.gameObject.activeInHierarchy || mockVisualModel == null) return;

        modelReferencePoint = mockVisualModel.transform.Find("ref");
        if (modelReferencePoint == null)
        {
            Debug.LogWarning("[MockedModelController] 'ref' child not found on mockVisualModel. Defaulting to model's root transform.");
            modelReferencePoint = mockVisualModel.transform;
        }

        if (!axesCreated)
        {
            CreateAxisVisuals();
            axesCreated = true;
        }
        axesContainer.SetActive(true);
    }

    public void ApplyServerModelScale(Vector3 serverModelSize)
    {
        if (mockVisualModel == null) return;
        // Apply the initial, non-uniform scale ONLY to the visual model.
        // The sibling axesContainer is NOT affected by this local scale change.
        mockVisualModel.transform.localScale = serverModelSize;

        // Ensure axes are created or updated after the model is scaled.
        EnsureAxisVisualsAreCreated();
    }

    void CreateAxisVisuals()
    {
        // Clear old visuals from the container first
        foreach (Transform child in axesContainer.transform)
        {
            Destroy(child.gameObject);
        }
        axisVisuals.Clear();

        // Parent the new axes to the special container.
        // This container inherits the uniform zoom scale from the parent controller,
        // but it does NOT inherit the non-uniform initial scale from the model.
        CreateSingleAxisVisual(axesContainer.transform, Vector3.right, axisLength, axisThickness, redAxisMaterial, "X_Axis_Client");
        CreateSingleAxisVisual(axesContainer.transform, Vector3.up, axisLength, axisThickness, greenAxisMaterial, "Y_Axis_Client");
        CreateSingleAxisVisual(axesContainer.transform, Vector3.forward, axisLength, axisThickness, blueAxisMaterial, "Z_Axis_Client");
    }

    void CreateSingleAxisVisual(Transform parentForAxes, Vector3 direction, float length, float thickness, Material axisMat, string baseName)
    {
        float capHeight = thickness * arrowheadHeightFactor;
        float shaftActualLength = Mathf.Max(thickness / 2f, length - capHeight);

        GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shaft.name = baseName + "_Shaft";
        shaft.transform.SetParent(parentForAxes, false);
        Destroy(shaft.GetComponent<CapsuleCollider>());
        shaft.transform.localScale = new Vector3(thickness, shaftActualLength / 2f, thickness);
        shaft.transform.localPosition = axisOriginOffset + direction * (shaftActualLength / 2f);
        shaft.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction);
        if (shaft.GetComponent<Renderer>() != null && axisMat != null) shaft.GetComponent<Renderer>().material = axisMat;
        axisVisuals.Add(shaft);

        GameObject arrowheadCap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        arrowheadCap.name = baseName + "_HeadCap";
        arrowheadCap.transform.SetParent(parentForAxes, false);
        Destroy(arrowheadCap.GetComponent<CapsuleCollider>());
        float capRadius = thickness * arrowheadRadiusFactor;
        arrowheadCap.transform.localScale = new Vector3(capRadius, capHeight / 2f, capRadius);
        arrowheadCap.transform.localPosition = axisOriginOffset + direction * (shaftActualLength + capHeight / 2f);
        arrowheadCap.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction);
        if (arrowheadCap.GetComponent<Renderer>() != null && axisMat != null) arrowheadCap.GetComponent<Renderer>().material = axisMat;
        axisVisuals.Add(arrowheadCap);
    }
}