using UnityEngine;
using System.Collections.Generic;

public class MockedModelController : MonoBehaviour
{
    [Header("Axis Visuals (Configured by UIManager)")]
    // [SerializeField] private bool showAxes = true; // Removed, controlled by UIManager now
    [SerializeField] private Vector3 axisOriginOffset = new Vector3(0f, 0f, 0f);
    [SerializeField] private float axisLength = 10f;
    [SerializeField] private float axisThickness = 0.03f;
    [SerializeField] private float arrowheadRadiusFactor = 2.5f;
    [SerializeField] private float arrowheadHeightFactor = 3f;

    private Vector3 initialLocalEulerAngles;
    private Vector3 initialLocalScale;
    private List<GameObject> axisVisuals = new List<GameObject>();
    private bool axesCreated = false;

    void Awake()
    {
        initialLocalEulerAngles = transform.localEulerAngles;
        initialLocalScale = transform.localScale;
    }

    public void EnsureAxisVisualsAreCreated()
    {
        if (!axesCreated && this.gameObject.activeInHierarchy)
        {
            CreateAxisVisuals();
            axesCreated = true;
        }
    }

    public void SetAxisVisualsActive(bool isActive)
    {
        EnsureAxisVisualsAreCreated(); // Create them if they haven't been yet
        foreach (GameObject vis in axisVisuals)
        {
            if (vis != null)
            {
                vis.SetActive(isActive);
            }
        }
    }

    void CreateAxisVisuals()
    {
        ClearAxisVisuals(); // Clear before creating to prevent duplicates

        CreateSingleAxisVisual(this.transform, Vector3.right, axisLength, axisThickness, Color.red, "X_Axis_Client");
        CreateSingleAxisVisual(this.transform, Vector3.up, axisLength, axisThickness, Color.green, "Y_Axis_Client");
        CreateSingleAxisVisual(this.transform, Vector3.forward, axisLength, axisThickness, Color.blue, "Z_Axis_Client");
    }

    void CreateSingleAxisVisual(Transform parentDirect, Vector3 direction, float length, float thickness, Color color, string baseName)
    {
        float capHeight = thickness * arrowheadHeightFactor;
        float shaftActualLength = length - capHeight;
        shaftActualLength = Mathf.Max(thickness / 2f, shaftActualLength);

        GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shaft.name = baseName + "_Shaft";
        shaft.transform.SetParent(parentDirect);
        Destroy(shaft.GetComponent<CapsuleCollider>());
        shaft.transform.localScale = new Vector3(thickness, shaftActualLength / 2f, thickness);
        shaft.transform.localPosition = axisOriginOffset + direction * (shaftActualLength / 2f);
        shaft.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction);
        Renderer shaftRend = shaft.GetComponent<Renderer>();
        ApplyMaterial(shaftRend, color);
        axisVisuals.Add(shaft);

        GameObject arrowheadCap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        arrowheadCap.name = baseName + "_HeadCap";
        arrowheadCap.transform.SetParent(parentDirect);
        Destroy(arrowheadCap.GetComponent<CapsuleCollider>());

        float capRadius = thickness * arrowheadRadiusFactor;
        arrowheadCap.transform.localScale = new Vector3(capRadius, capHeight / 2f, capRadius);
        arrowheadCap.transform.localPosition = axisOriginOffset + direction * (shaftActualLength + capHeight / 2f);
        arrowheadCap.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction);

        Renderer headRend = arrowheadCap.GetComponent<Renderer>();
        ApplyMaterial(headRend, color);
        axisVisuals.Add(arrowheadCap);
    }

    void ApplyMaterial(Renderer rend, Color color)
    {
        if (rend == null) return;
        Shader unlitColorShader = Shader.Find("Unlit/Color");
        if (unlitColorShader != null) rend.material = new Material(unlitColorShader);
        else rend.material = new Material(Shader.Find("Legacy Shaders/Diffuse"));
        rend.material.color = color;
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
        transform.localEulerAngles = eulerAngles;
        transform.localScale = scale;
    }

    public void ResetState()
    {
        SetInitialState(initialLocalEulerAngles, initialLocalScale);
        SetAxisVisualsActive(false); // Hide axes on reset, gizmo button will show them again if active
    }

    public Transform GetRefChildTransform()
    {
        Transform refChild = transform.Find("ref");
        if (refChild != null) return refChild;
        return this.transform;
    }
}