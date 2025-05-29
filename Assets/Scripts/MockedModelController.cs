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


    private Vector3 initialLocalEulerAngles;
    private Vector3 initialLocalScale;
    private List<GameObject> axisVisuals = new List<GameObject>();
    private bool axesCreated = false;
    private Transform refChildTransform;

    void Awake()
    {
        initialLocalEulerAngles = transform.localEulerAngles;
        initialLocalScale = transform.localScale;

        refChildTransform = transform.Find("ref");
        if (refChildTransform == null)
        {
            Debug.LogWarning($"[MockedModelController] 'ref' child not found in '{this.name}'. Axes will be parented to MockedModel root.");
            refChildTransform = this.transform;
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
        if (!this.gameObject.activeInHierarchy) return;

        if (!axesCreated && refChildTransform != null && refChildTransform.gameObject.activeInHierarchy)
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
        if (refChildTransform == null)
        {
            refChildTransform = transform.Find("ref");
            if (refChildTransform == null)
            {
                refChildTransform = this.transform;
            }
        }

        CreateSingleAxisVisual(refChildTransform, Vector3.right, axisLength, axisThickness, redAxisMaterial, "X_Axis_Client");
        CreateSingleAxisVisual(refChildTransform, Vector3.up, axisLength, axisThickness, greenAxisMaterial, "Y_Axis_Client");
        CreateSingleAxisVisual(refChildTransform, Vector3.forward, axisLength, axisThickness, blueAxisMaterial, "Z_Axis_Client");
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
        transform.localEulerAngles = eulerAngles;
        transform.localScale = scale;
    }

    public void ResetState()
    {
        SetInitialState(initialLocalEulerAngles, initialLocalScale);
        EnsureAxisVisualsAreCreated();
    }

    public Transform GetRefChildTransform()
    {
        if (refChildTransform == null)
        {
            refChildTransform = transform.Find("ref");
            if (refChildTransform == null)
            {
                return this.transform;
            }
        }
        return refChildTransform;
    }
}