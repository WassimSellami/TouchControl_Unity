using UnityEngine;
using System.Collections.Generic;
using EzySlice;
using System.Linq;
using System.Collections;

public class ModelController : MonoBehaviour
{
    private enum ActionType { Slice, Destroy }
    private class ActionRecord
    {
        public ActionType Type;
        public string ActionID;
        public List<GameObject> Originals;
        public List<GameObject> NewHulls;
        public GameObject DestroyedPart;
    }

    [Header("Model Prefabs")]
    [SerializeField] private GameObject modelPrefab1;
    [SerializeField] private GameObject modelPrefab2;

    [Header("Cutting Components")]
    [SerializeField] private Material crossSectionMaterial;
    [SerializeField] private GameObject planeVisualizerPrefab;
    [SerializeField] private GameObject lineRendererPrefab;
    [SerializeField] private bool showPlaneVisualizer = true;

    [Header("Axis Visuals (Server)")]
    [SerializeField] private bool showServerAxes = true;
    [SerializeField] private Vector3 serverAxisOriginOffset = new Vector3(0f, 0f, 0f);
    [SerializeField] private float serverAxisLength = 10f;
    [SerializeField] private float serverAxisThickness = 0.03f;
    [SerializeField] private float serverArrowheadRadiusFactor = 2.5f;
    [SerializeField] private float serverArrowheadHeightFactor = 3f;

    private GameObject worldContainer;
    private GameObject modelContainer;
    private GameObject axesContainer; // Dedicated container for axes
    private Transform modelReferencePoint; // The 'ref' point to track
    private GameObject rootModel;
    private List<GameObject> currentModelAxisVisuals = new List<GameObject>();
    private GameObject activePlaneVisualizer;
    private LineRenderer activeLineRenderer;

    private Dictionary<string, ActionRecord> history = new Dictionary<string, ActionRecord>();
    private Dictionary<string, GameObject> allParts = new Dictionary<string, GameObject>();

    public string CurrentModelID { get; private set; } = null;
    public Vector3 CurrentModelBoundsSize { get; private set; } = Vector3.one;

    void LateUpdate()
    {
        // This ensures the axes always follow the reference point
        if (axesContainer != null && modelReferencePoint != null)
        {
            axesContainer.transform.position = modelReferencePoint.position;
            axesContainer.transform.rotation = modelReferencePoint.rotation;
        }
    }

    public void ApplyWorldTransform(Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
    {
        if (worldContainer != null)
        {
            worldContainer.transform.localPosition = localPosition;
            worldContainer.transform.localRotation = localRotation;
            worldContainer.transform.localScale = localScale;
        }
    }

    public void LoadNewModel(string modelId)
    {
        if (worldContainer != null) Destroy(worldContainer);
        allParts.Clear();
        history.Clear();
        currentModelAxisVisuals.Clear();
        modelReferencePoint = null;

        worldContainer = new GameObject("WorldContainer");
        worldContainer.transform.SetParent(this.transform, false);

        modelContainer = new GameObject("ModelContainer");
        modelContainer.transform.SetParent(worldContainer.transform, false);

        axesContainer = new GameObject("AxesContainer");
        axesContainer.transform.SetParent(worldContainer.transform, false);

        GameObject prefabToLoad = null;
        modelId = modelId.ToUpperInvariant();
        CurrentModelID = modelId;

        if (modelId == "1") prefabToLoad = modelPrefab1;
        else if (modelId == "2") prefabToLoad = modelPrefab2;
        else { CurrentModelID = null; return; }

        if (prefabToLoad != null)
        {
            rootModel = Instantiate(prefabToLoad, modelContainer.transform);
            rootModel.name = "RootModel";
            allParts.Add(rootModel.name, rootModel);
            CurrentModelBoundsSize = CalculateModelBoundsSize(rootModel);

            // Find the reference point for the axes
            modelReferencePoint = rootModel.transform.Find("ref");
            if (modelReferencePoint == null)
            {
                modelReferencePoint = rootModel.transform; // Fallback to the model's root
            }

            if (planeVisualizerPrefab != null)
            {
                activePlaneVisualizer = Instantiate(planeVisualizerPrefab, worldContainer.transform, false);
                activePlaneVisualizer.SetActive(false);
            }
            if (lineRendererPrefab != null)
            {
                activeLineRenderer = Instantiate(lineRendererPrefab, worldContainer.transform, false).GetComponent<LineRenderer>();
                activeLineRenderer.enabled = false;
            }

            CreateServerAxisVisuals(axesContainer.transform);
        }
        else { CurrentModelID = null; }
    }

    public void ExecuteSlice(SliceActionData data)
    {
        var record = new ActionRecord
        {
            Type = ActionType.Slice,
            ActionID = data.actionID,
            Originals = new List<GameObject>(),
            NewHulls = new List<GameObject>()
        };

        foreach (string partID in data.targetPartIDs)
        {
            if (allParts.TryGetValue(partID, out GameObject originalPart) && originalPart.activeInHierarchy)
            {
                SlicedHull sliceResult = originalPart.Slice(data.planePoint, data.planeNormal, crossSectionMaterial);
                if (sliceResult != null)
                {
                    GameObject upperHull = sliceResult.CreateUpperHull(originalPart, crossSectionMaterial);
                    GameObject lowerHull = sliceResult.CreateLowerHull(originalPart, crossSectionMaterial);

                    if (upperHull != null && lowerHull != null)
                    {
                        upperHull.name = partID + "_U";
                        lowerHull.name = partID + "_L";

                        allParts.Add(upperHull.name, upperHull);
                        allParts.Add(lowerHull.name, lowerHull);

                        SetupHull(upperHull, originalPart);
                        SetupHull(lowerHull, originalPart);

                        StartCoroutine(AnimateSeparation(upperHull, lowerHull, originalPart, data.planeNormal, data.separationFactor));

                        record.Originals.Add(originalPart);
                        record.NewHulls.Add(upperHull);
                        record.NewHulls.Add(lowerHull);

                        originalPart.SetActive(false);
                    }
                }
            }
        }
        if (record.Originals.Count > 0)
        {
            history[data.actionID] = record;
        }
    }

    public void ExecuteDestroy(DestroyActionData data)
    {
        if (allParts.TryGetValue(data.targetPartID, out GameObject partToDestroy))
        {
            partToDestroy.SetActive(false);
            var record = new ActionRecord
            {
                Type = ActionType.Destroy,
                ActionID = data.actionID,
                DestroyedPart = partToDestroy
            };
            history[data.actionID] = record;
        }
    }

    public void UndoAction(string actionID)
    {
        if (history.TryGetValue(actionID, out ActionRecord record))
        {
            if (record.Type == ActionType.Slice)
            {
                foreach (var hull in record.NewHulls) hull.SetActive(false);
                foreach (var part in record.Originals) part.SetActive(true);
            }
            else if (record.Type == ActionType.Destroy)
            {
                if (record.DestroyedPart != null) record.DestroyedPart.SetActive(true);
            }
        }
    }

    public void RedoAction(string actionID)
    {
        if (history.TryGetValue(actionID, out ActionRecord record))
        {
            if (record.Type == ActionType.Slice)
            {
                foreach (var hull in record.NewHulls) hull.SetActive(true);
                foreach (var part in record.Originals) part.SetActive(false);
            }
            else if (record.Type == ActionType.Destroy)
            {
                if (record.DestroyedPart != null) record.DestroyedPart.SetActive(false);
            }
        }
    }

    public void UpdateVisualCropPlane(Vector3 position, Vector3 normal, float scale)
    {
        if (activePlaneVisualizer == null) return;
        activePlaneVisualizer.transform.position = position;
        activePlaneVisualizer.transform.rotation = Quaternion.LookRotation(normal);
        activePlaneVisualizer.transform.localScale = Vector3.one * scale;
        activePlaneVisualizer.SetActive(showPlaneVisualizer);
    }

    public void ResetCrop()
    {
        foreach (var part in allParts.Values)
        {
            if (part != null && part != rootModel)
            {
                Destroy(part);
            }
        }

        allParts.Clear();
        history.Clear();

        if (rootModel != null)
        {
            rootModel.SetActive(true);
            allParts.Add(rootModel.name, rootModel);
        }

        if (activePlaneVisualizer != null) activePlaneVisualizer.SetActive(false);
    }

    public void UpdateCutLine(Vector3 start, Vector3 end)
    {
        if (activeLineRenderer == null) return;
        activeLineRenderer.enabled = true;
        activeLineRenderer.positionCount = 2;
        activeLineRenderer.SetPosition(0, start);
        activeLineRenderer.SetPosition(1, end);
    }

    public void HideCutLine()
    {
        if (activeLineRenderer == null) return;
        activeLineRenderer.enabled = false;
        activeLineRenderer.positionCount = 0;
    }

    private void SetupHull(GameObject hull, GameObject original)
    {
        hull.transform.SetParent(original.transform.parent, false);
        hull.AddComponent<MeshCollider>().convex = true;
    }

    private IEnumerator AnimateSeparation(GameObject upperHull, GameObject lowerHull, GameObject original, Vector3 planeNormal, float separationFactor)
    {
        float duration = 0.3f;
        Bounds originalBounds = original.GetComponent<Renderer>().bounds;
        float separationDistance = originalBounds.size.magnitude * separationFactor;
        Vector3 separationVector = planeNormal * (separationDistance * 0.5f);

        Vector3 upperStartPos = upperHull.transform.position;
        Vector3 lowerStartPos = lowerHull.transform.position;
        Vector3 upperEndPos = upperStartPos + separationVector;
        Vector3 lowerEndPos = lowerStartPos - separationVector;

        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            if (upperHull == null || lowerHull == null) yield break;

            float t = Mathf.SmoothStep(0.0f, 1.0f, elapsedTime / duration);
            upperHull.transform.position = Vector3.Lerp(upperStartPos, upperEndPos, t);
            lowerHull.transform.position = Vector3.Lerp(lowerStartPos, lowerEndPos, t);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        if (upperHull != null) upperHull.transform.position = upperEndPos;
        if (lowerHull != null) lowerHull.transform.position = lowerEndPos;
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

    void CreateServerAxisVisuals(Transform axesParentTransform)
    {
        if (!showServerAxes || axesParentTransform == null) return;
        ClearCurrentModelAxisVisuals();
        CreateSingleServerAxisVisual(axesParentTransform, Vector3.right, serverAxisLength, serverAxisThickness, Color.red, "X_Axis_Server");
        CreateSingleServerAxisVisual(axesParentTransform, Vector3.up, serverAxisLength, serverAxisThickness, Color.green, "Y_Axis_Server");
        CreateSingleServerAxisVisual(axesParentTransform, Vector3.forward, serverAxisLength, serverAxisThickness, Color.blue, "Z_Axis_Server");
    }

    void CreateSingleServerAxisVisual(Transform parentRef, Vector3 direction, float length, float thickness, Color color, string baseName)
    {
        float capHeight = thickness * serverArrowheadHeightFactor;
        float shaftActualLength = Mathf.Max(thickness / 2f, length - capHeight);
        GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shaft.name = baseName + "_Shaft";
        shaft.transform.SetParent(parentRef, false); // Set parent without affecting world position
        Destroy(shaft.GetComponent<CapsuleCollider>());
        shaft.transform.localScale = new Vector3(thickness, shaftActualLength / 2f, thickness);
        shaft.transform.localPosition = serverAxisOriginOffset + direction * (shaftActualLength / 2f);
        shaft.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction);
        ApplyServerMaterial(shaft.GetComponent<Renderer>(), color);
        currentModelAxisVisuals.Add(shaft);

        GameObject arrowheadCap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        arrowheadCap.name = baseName + "_HeadCap";
        arrowheadCap.transform.SetParent(parentRef, false); // Set parent without affecting world position
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