using UnityEngine;
using System.Collections.Generic;

public class ModelController : MonoBehaviour
{
    [Header("Transform Control Speeds")]
    public float rotationLerpSpeed = 5.0f;
    public float scaleLerpSpeed = 5.0f;

    [Header("Model Prefabs")]
    [SerializeField] private GameObject cubePrefab;
    [SerializeField] private GameObject cylinderPrefab;

    [Header("Axis Visuals (Server)")]
    [SerializeField] private bool showServerAxes = true;
    [SerializeField] private Vector3 serverAxisOriginOffset = new Vector3(0f, 0f, 0f);
    [SerializeField] private float serverAxisLength = 10f;
    [SerializeField] private float serverAxisThickness = 0.03f;
    [SerializeField] private float serverArrowheadRadiusFactor = 2.5f;
    [SerializeField] private float serverArrowheadHeightFactor = 3f;

    private GameObject currentInstantiatedModel;
    private List<GameObject> currentModelAxisVisuals = new List<GameObject>();
    private Quaternion targetRootModelRotation = Quaternion.identity;
    private Vector3 targetRootModelScale = Vector3.one;

    public string CurrentModelID { get; private set; } = null;

    void Update()
    {
        if (currentInstantiatedModel != null)
        {
            currentInstantiatedModel.transform.localRotation = Quaternion.Slerp(currentInstantiatedModel.transform.localRotation, targetRootModelRotation, Time.deltaTime * rotationLerpSpeed);
            currentInstantiatedModel.transform.localScale = Vector3.Lerp(currentInstantiatedModel.transform.localScale, targetRootModelScale, Time.deltaTime * scaleLerpSpeed);
        }
    }

    void ClearCurrentModelAxisVisuals()
    {
        foreach (GameObject vis in currentModelAxisVisuals)
        {
            if (vis != null) Destroy(vis);
        }
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
        float shaftActualLength = length - capHeight;
        shaftActualLength = Mathf.Max(thickness, shaftActualLength);

        GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shaft.name = baseName + "_Shaft";
        shaft.transform.SetParent(parentRef);
        Destroy(shaft.GetComponent<CapsuleCollider>());
        shaft.transform.localScale = new Vector3(thickness, shaftActualLength / 2f, thickness);
        shaft.transform.localPosition = serverAxisOriginOffset + direction * (shaftActualLength / 2f);
        shaft.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction);
        Renderer shaftRend = shaft.GetComponent<Renderer>();
        ApplyServerMaterial(shaftRend, color);
        currentModelAxisVisuals.Add(shaft);

        GameObject arrowheadCap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        arrowheadCap.name = baseName + "_HeadCap";
        arrowheadCap.transform.SetParent(parentRef);
        Destroy(arrowheadCap.GetComponent<CapsuleCollider>());

        float capRadius = thickness * serverArrowheadRadiusFactor;
        arrowheadCap.transform.localScale = new Vector3(capRadius, capHeight / 2f, capRadius);
        arrowheadCap.transform.localPosition = serverAxisOriginOffset + direction * (shaftActualLength + capHeight / 2f);
        arrowheadCap.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction);
        Renderer headRend = arrowheadCap.GetComponent<Renderer>();
        ApplyServerMaterial(headRend, color);
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

    public void SetInitialTransform(Vector3 clientMockedModelLocalPos, Quaternion clientMockedModelLocalRot, Vector3 clientMockedModelLocalScale)
    {
        if (currentInstantiatedModel == null)
        {
            Debug.LogWarning("[ModelController] SetInitialTransform called but no model is loaded.");
            return;
        }
        currentInstantiatedModel.transform.localPosition = clientMockedModelLocalPos;
        currentInstantiatedModel.transform.localRotation = clientMockedModelLocalRot;
        currentInstantiatedModel.transform.localScale = clientMockedModelLocalScale;

        Transform refChildServer = currentInstantiatedModel.transform.Find("ref");
        if (refChildServer == null)
        {
            CreateServerAxisVisuals(currentInstantiatedModel.transform);
        }
        else
        {
            CreateServerAxisVisuals(refChildServer);
        }

        targetRootModelRotation = currentInstantiatedModel.transform.localRotation;
        targetRootModelScale = currentInstantiatedModel.transform.localScale;
    }

    public void LoadNewModel(string modelId)
    {
        if (cubePrefab == null || cylinderPrefab == null)
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

        if (modelId == "CUBE") prefabToLoad = cubePrefab;
        else if (modelId == "CYLINDER") prefabToLoad = cylinderPrefab;
        else { Debug.LogWarning($"[ModelController] Unknown model ID: {modelId}"); CurrentModelID = null; return; }

        if (prefabToLoad != null)
        {
            currentInstantiatedModel = Instantiate(prefabToLoad);
            currentInstantiatedModel.transform.SetParent(this.transform);
            currentInstantiatedModel.transform.localPosition = Vector3.zero;
            currentInstantiatedModel.transform.localRotation = Quaternion.identity;
            currentInstantiatedModel.transform.localScale = Vector3.one;

            targetRootModelRotation = currentInstantiatedModel.transform.localRotation;
            targetRootModelScale = currentInstantiatedModel.transform.localScale;
        }
        else { Debug.LogError($"[ModelController] Failed to find prefab for model ID: {modelId}"); CurrentModelID = null; }
    }

    public void SetRotation(Vector3 newEulerAngles) { targetRootModelRotation = Quaternion.Euler(newEulerAngles); }
    public void ApplyRotationDelta(Vector3 rotationDeltaEuler) { targetRootModelRotation = Quaternion.AngleAxis(rotationDeltaEuler.y, Vector3.up) * Quaternion.AngleAxis(rotationDeltaEuler.x, Vector3.right) * targetRootModelRotation; }
    public void SetScale(Vector3 newScale) { targetRootModelScale = newScale; }
    public void ApplyScaleFactor(float scaleFactor) { targetRootModelScale *= scaleFactor; }
    public Transform GetCurrentModelTransform() { return currentInstantiatedModel != null ? currentInstantiatedModel.transform : null; }
}