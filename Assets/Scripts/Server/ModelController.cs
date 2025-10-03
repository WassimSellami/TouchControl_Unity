using UnityEngine;
using System.Collections.Generic;

public class ModelController : MonoBehaviour
{
    [Header("Model Prefabs")]
    [SerializeField] private GameObject modelPrefab1;
    [SerializeField] private GameObject modelPrefab2;

    [Header("Axis Visuals (Server)")]
    [SerializeField] private bool showServerAxes = true;
    [SerializeField] private Vector3 serverAxisOriginOffset = new Vector3(0f, 0f, 0f);
    [SerializeField] private float serverAxisLength = 10f;
    [SerializeField] private float serverAxisThickness = 0.03f;
    [SerializeField] private float serverArrowheadRadiusFactor = 2.5f;
    [SerializeField] private float serverArrowheadHeightFactor = 3f;

    private GameObject currentInstantiatedModel;
    private List<GameObject> currentModelAxisVisuals = new List<GameObject>();

    public string CurrentModelID { get; private set; } = null;
    public Vector3 CurrentModelBoundsSize { get; private set; } = Vector3.one;

    public void ApplyWorldTransform(Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
    {
        if (currentInstantiatedModel != null)
        {
            currentInstantiatedModel.transform.localPosition = localPosition;
            currentInstantiatedModel.transform.localRotation = localRotation;
            currentInstantiatedModel.transform.localScale = localScale;
        }
    }

    public void LoadNewModel(string modelId)
    {
        if (modelPrefab1 == null || modelPrefab2 == null)
        {
            Debug.LogError("[ModelController] Model prefabs not set.");
            return;
        }

        if (currentInstantiatedModel != null)
        {
            ClearCurrentModelAxisVisuals();
            Destroy(currentInstantiatedModel);
            currentInstantiatedModel = null;
        }

        GameObject prefabToLoad = null;
        modelId = modelId.ToUpperInvariant();
        CurrentModelID = modelId;

        if (modelId == "1") prefabToLoad = modelPrefab1;
        else if (modelId == "2") prefabToLoad = modelPrefab2;
        else { Debug.LogWarning($"[ModelController] Unknown model ID: {modelId}"); CurrentModelID = null; return; }

        if (prefabToLoad != null)
        {
            currentInstantiatedModel = Instantiate(prefabToLoad, this.transform);
            currentInstantiatedModel.transform.localPosition = Vector3.zero;
            currentInstantiatedModel.transform.localRotation = Quaternion.identity;
            currentInstantiatedModel.transform.localScale = Vector3.one;
            CurrentModelBoundsSize = CalculateModelBoundsSize(currentInstantiatedModel);

            Transform refChildServer = currentInstantiatedModel.transform.Find("ref");
            if (refChildServer == null) CreateServerAxisVisuals(currentInstantiatedModel.transform);
            else CreateServerAxisVisuals(refChildServer);
        }
        else { Debug.LogError($"[ModelController] Failed to find prefab for model ID: {modelId}"); CurrentModelID = null; }
    }

    private Vector3 CalculateModelBoundsSize(GameObject model)
    {
        if (model == null) return Vector3.one;
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return Vector3.one;
        Bounds bounds = new Bounds(renderers[0].bounds.center, Vector3.zero);
        foreach (Renderer rend in renderers) { bounds.Encapsulate(rend.bounds); }
        return bounds.size;
    }

    void ClearCurrentModelAxisVisuals()
    {
        foreach (GameObject vis in currentModelAxisVisuals) { if (vis != null) Destroy(vis); }
        currentModelAxisVisuals.Clear();
    }

    void CreateServerAxisVisuals(Transform referencePointTransform)
    {
        if (!showServerAxes || referencePointTransform == null) return;
        ClearCurrentModelAxisVisuals();
        CreateSingleServerAxisVisual(referencePointTransform, Vector3.right, serverAxisLength, serverAxisThickness, Color.red, "X_Axis_Server");
        CreateSingleServerAxisVisual(referencePointTransform, Vector3.up, serverAxisLength, serverAxisThickness, Color.green, "Y_Axis_Server");
        CreateSingleServerAxisVisual(referencePointTransform, Vector3.forward, serverAxisLength, serverAxisThickness, Color.blue, "Z_Axis_Server");
    }

    void CreateSingleServerAxisVisual(Transform parentRef, Vector3 direction, float length, float thickness, Color color, string baseName)
    {
        float capHeight = thickness * serverArrowheadHeightFactor;
        float shaftActualLength = Mathf.Max(thickness / 2f, length - capHeight);
        GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shaft.name = baseName + "_Shaft";
        shaft.transform.SetParent(parentRef);
        Destroy(shaft.GetComponent<CapsuleCollider>());
        shaft.transform.localScale = new Vector3(thickness, shaftActualLength / 2f, thickness);
        shaft.transform.localPosition = serverAxisOriginOffset + direction * (shaftActualLength / 2f);
        shaft.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction);
        ApplyServerMaterial(shaft.GetComponent<Renderer>(), color);
        currentModelAxisVisuals.Add(shaft);

        GameObject arrowheadCap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        arrowheadCap.name = baseName + "_HeadCap";
        arrowheadCap.transform.SetParent(parentRef);
        Destroy(arrowheadCap.GetComponent<CapsuleCollider>());
        float capRadius = thickness * serverArrowheadRadiusFactor;
        arrowheadCap.transform.localScale = new Vector3(capRadius, capHeight / 2f, capRadius);
        arrowheadCap.transform.localPosition = serverAxisOriginOffset + direction * (shaftActualLength + capHeight / 2f);
        arrowheadCap.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction);
        ApplyServerMaterial(arrowheadCap.GetComponent<Renderer>(), color);
        currentModelAxisVisuals.Add(arrowheadCap);
    }

    void ApplyServerMaterial(Renderer rend, Color color)
    {
        if (rend == null) return;
        Shader unlitColorShader = Shader.Find("Unlit/Color");
        if (unlitColorShader != null) rend.material = new Material(unlitColorShader);
        else rend.material = new Material(Shader.Find("Legacy Shaders/Diffuse"));
        rend.material.color = color;
    }
}