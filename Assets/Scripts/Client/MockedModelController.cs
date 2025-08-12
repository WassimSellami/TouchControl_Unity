using UnityEngine;
using System.Collections.Generic;

public class MockedModelController : MonoBehaviour
{
    [Header("Axis Visuals")]
    [SerializeField] private Vector3 axisOriginOffset = new Vector3(0f, 0f, 0f);
    [SerializeField] private float axisLength = 10f;
    [SerializeField] private float axisThickness = 0.03f;
    [SerializeField] private float arrowheadRadiusFactor = 2.5f;
    [SerializeField] private float arrowheadHeightFactor = 3f;

    [Header("Axis Materials (Assign in Inspector)")]
    [SerializeField] private Material redAxisMaterial;
    [SerializeField] private Material greenAxisMaterial;
    [SerializeField] private Material blueAxisMaterial;

    [Header("Mock Model Reference")]
    [SerializeField] private GameObject mockVisualModel; // Drag your mock cube/model here

    private Vector3 initialLocalEulerAngles;
    private Vector3 initialLocalScale;
    private List<GameObject> axisVisuals = new List<GameObject>();
    private bool axesCreated = false;
    // Removed [SerializeField] for refChildTransform, will always be this.transform for axes
    private Transform axesParentTransform; // Renamed for clarity

    void Awake()
    {
        // axesParentTransform will be this GameObject, ensuring axes aren't scaled with mockVisualModel
        axesParentTransform = this.transform;

        // Store initial transform of the visual model, not the controller itself
        if (mockVisualModel != null)
        {
            initialLocalEulerAngles = mockVisualModel.transform.localEulerAngles;
            initialLocalScale = mockVisualModel.transform.localScale;
        }
        else
        {
            Debug.LogWarning("[MockedModelController] Mock Visual Model not assigned! Initial scale/rotation cannot be stored or reset.");
            initialLocalEulerAngles = Vector3.zero;
            initialLocalScale = Vector3.one;
        }

        if (redAxisMaterial == null || greenAxisMaterial == null || blueAxisMaterial == null)
        {
            Debug.LogError("[MockedModelController] Axis materials are not assigned in the Inspector!");
        }
    }

    void OnEnable()
    {
        EnsureAxisVisualsAreCreated();
    }

    public void EnsureAxisVisualsAreCreated()
    {
        // Axes are always parented to this.transform (axesParentTransform)
        if (!this.gameObject.activeInHierarchy) return;

        if (!axesCreated && axesParentTransform != null && axesParentTransform.gameObject.activeInHierarchy)
        {
            CreateAxisVisuals();
            axesCreated = true;
        }
        foreach (GameObject vis in axisVisuals)
        {
            if (vis != null) vis.SetActive(true);
        }
    }

    void CreateAxisVisuals()
    {
        ClearAxisVisuals();
        // Axes are always parented to axesParentTransform (this.transform)
        CreateSingleAxisVisual(axesParentTransform, Vector3.right, axisLength, axisThickness, redAxisMaterial, "X_Axis_Client");
        CreateSingleAxisVisual(axesParentTransform, Vector3.up, axisLength, axisThickness, greenAxisMaterial, "Y_Axis_Client");
        CreateSingleAxisVisual(axesParentTransform, Vector3.forward, axisLength, axisThickness, blueAxisMaterial, "Z_Axis_Client");
    }

    void CreateSingleAxisVisual(Transform parentForAxes, Vector3 direction, float length, float thickness, Material axisMat, string baseName)
    {
        float capHeight = thickness * arrowheadHeightFactor;
        float shaftActualLength = length - capHeight;
        shaftActualLength = Mathf.Max(thickness / 2f, shaftActualLength);

        GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shaft.name = baseName + "_Shaft";
        shaft.transform.SetParent(parentForAxes);
        Destroy(shaft.GetComponent<CapsuleCollider>());
        shaft.transform.localScale = new Vector3(thickness, shaftActualLength / 2f, thickness);
        shaft.transform.localPosition = axisOriginOffset + direction * (shaftActualLength / 2f);
        shaft.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction);
        Renderer shaftRend = shaft.GetComponent<Renderer>();
        if (shaftRend != null && axisMat != null) shaftRend.material = axisMat;
        axisVisuals.Add(shaft);

        GameObject arrowheadCap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        arrowheadCap.name = baseName + "_HeadCap";
        arrowheadCap.transform.SetParent(parentForAxes);
        Destroy(arrowheadCap.GetComponent<CapsuleCollider>());

        float capRadius = thickness * arrowheadRadiusFactor;
        arrowheadCap.transform.localScale = new Vector3(capRadius, capHeight / 2f, capRadius);
        arrowheadCap.transform.localPosition = axisOriginOffset + direction * (shaftActualLength + capHeight / 2f);
        arrowheadCap.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction);

        Renderer headRend = arrowheadCap.GetComponent<Renderer>();
        if (headRend != null && axisMat != null) headRend.material = axisMat;
        axisVisuals.Add(arrowheadCap);
    }

    void ClearAxisVisuals()
    {
        foreach (GameObject vis in axisVisuals)
        {
            if (vis != null) Destroy(vis);
        }
        axisVisuals.Clear();
        axesCreated = false;
    }

    public void SetInitialState(Vector3 eulerAngles, Vector3 scale)
    {
        if (mockVisualModel != null)
        {
            mockVisualModel.transform.localEulerAngles = eulerAngles;
            mockVisualModel.transform.localScale = scale;
        }
        else
        {
            Debug.LogWarning("[MockedModelController] No mock visual model assigned to set initial state.");
        }
    }

    public void ResetState()
    {
        SetInitialState(initialLocalEulerAngles, initialLocalScale);
        EnsureAxisVisualsAreCreated(); // Re-create if cleared
    }

    public void ApplyServerModelScale(Vector3 serverModelSize)
    {
        if (mockVisualModel == null)
        {
            Debug.LogWarning("[MockedModelController] No mock visual model assigned to apply server scale.");
            return;
        }

        // Assuming a default mock model size of 1 unit in all axes.
        // Adjust this 'baseUnitSize' if your initial mock cube is not 1x1x1.
        Vector3 baseUnitSize = Vector3.one; // For a standard Unity Primitive cube/cylinder

        // Calculate the scale factor needed for each axis
        Vector3 scaleFactor = new Vector3(
            serverModelSize.x / baseUnitSize.x,
            serverModelSize.y / baseUnitSize.y,
            serverModelSize.z / baseUnitSize.z
        );

        // Apply this scale factor ONLY to the mock visual model, not its parent or axes
        mockVisualModel.transform.localScale = scaleFactor;

        Debug.Log($"[MockedModelController] Applied server scale: {serverModelSize} leading to localScale of mock visual model: {scaleFactor}");
    }
}