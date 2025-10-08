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

    [Header("Shaking Effect")]
    [SerializeField] private float shakeMagnitude = 0.01f;
    [SerializeField] private float shakeRoughness = 5f;

    private GameObject worldContainer;
    private GameObject modelContainer;
    private GameObject axesContainer;
    private Transform modelReferencePoint;
    private GameObject rootModel;
    private List<GameObject> currentModelAxisVisuals = new List<GameObject>();
    private GameObject activePlaneVisualizer;
    private LineRenderer activeLineRenderer;

    private Stack<ActionRecord> undoStack = new Stack<ActionRecord>();
    private Stack<ActionRecord> redoStack = new Stack<ActionRecord>();
    private Dictionary<string, GameObject> allParts = new Dictionary<string, GameObject>();
    private Dictionary<GameObject, Coroutine> shakingCoroutines = new Dictionary<GameObject, Coroutine>();
    private Dictionary<GameObject, Vector3> originalLocalPositions = new Dictionary<GameObject, Vector3>();
    private Dictionary<GameObject, Vector3> shakeOffsets = new Dictionary<GameObject, Vector3>();

    public string CurrentModelID { get; private set; } = null;
    public Vector3 CurrentModelBoundsSize { get; private set; } = Vector3.one;

    void LateUpdate()
    {
        if (axesContainer != null && modelReferencePoint != null)
        {
            Vector3 worldShakeOffset = Vector3.zero;
            if (rootModel != null && shakeOffsets.TryGetValue(rootModel, out Vector3 localOffset))
            {
                if (rootModel.transform.parent != null)
                {
                    worldShakeOffset = rootModel.transform.parent.TransformVector(localOffset);
                }
                else
                {
                    worldShakeOffset = localOffset;
                }
            }

            axesContainer.transform.position = modelReferencePoint.position - worldShakeOffset;
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

    public void UnloadCurrentModel()
    {
        if (worldContainer != null)
        {
            Destroy(worldContainer);
        }

        foreach (var coroutine in shakingCoroutines.Values)
        {
            if (coroutine != null) StopCoroutine(coroutine);
        }

        shakingCoroutines.Clear();
        originalLocalPositions.Clear();
        shakeOffsets.Clear();
        allParts.Clear();
        undoStack.Clear();
        redoStack.Clear();
        currentModelAxisVisuals.Clear();

        modelReferencePoint = null;
        rootModel = null;
        worldContainer = null;
        modelContainer = null;
        axesContainer = null;
        CurrentModelID = null;
    }

    public void LoadNewModel(string modelId)
    {
        UnloadCurrentModel();

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

            modelReferencePoint = rootModel.transform.Find("ref");
            if (modelReferencePoint == null)
            {
                modelReferencePoint = rootModel.transform;
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
            if (axesContainer != null) axesContainer.SetActive(true);
        }
        else { CurrentModelID = null; }
    }

    private void ClearRedoStack()
    {
        while (redoStack.Count > 0)
        {
            ActionRecord undoneAction = redoStack.Pop();
            CleanUpAction(undoneAction);
        }
    }

    private void CleanUpAction(ActionRecord record)
    {
        if (record.Type == ActionType.Slice)
        {
            foreach (var hull in record.NewHulls)
            {
                if (hull != null)
                {
                    allParts.Remove(hull.name);
                    Destroy(hull);
                }
            }
        }
    }

    public void ExecuteSlice(SliceActionData data)
    {
        ClearRedoStack();

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

                        if (allParts.ContainsKey(upperHull.name) || allParts.ContainsKey(lowerHull.name))
                        {
                            Destroy(upperHull);
                            Destroy(lowerHull);
                            continue;
                        }

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
            undoStack.Push(record);
        }
    }

    public void ExecuteDestroy(DestroyActionData data)
    {
        StopShaking(data.targetPartID, false);
        ClearRedoStack();

        if (allParts.TryGetValue(data.targetPartID, out GameObject partToDestroy))
        {
            partToDestroy.SetActive(false);
            var record = new ActionRecord
            {
                Type = ActionType.Destroy,
                ActionID = data.actionID,
                DestroyedPart = partToDestroy
            };
            undoStack.Push(record);
        }
    }

    public void UndoLastAction()
    {
        if (undoStack.Count > 0)
        {
            ActionRecord record = undoStack.Pop();

            if (record.Type == ActionType.Slice)
            {
                foreach (var hull in record.NewHulls)
                {
                    if (hull != null) hull.SetActive(false);
                }
                foreach (var part in record.Originals)
                {
                    if (part != null) part.SetActive(true);
                }
            }
            else if (record.Type == ActionType.Destroy)
            {
                if (record.DestroyedPart != null)
                {
                    record.DestroyedPart.SetActive(true);
                    StopShaking(record.DestroyedPart.name);
                }
            }

            redoStack.Push(record);
        }
    }

    public void RedoLastAction()
    {
        if (redoStack.Count > 0)
        {
            ActionRecord record = redoStack.Pop();

            if (record.Type == ActionType.Slice)
            {
                foreach (var hull in record.NewHulls)
                {
                    if (hull != null) hull.SetActive(true);
                }
                foreach (var part in record.Originals)
                {
                    if (part != null) part.SetActive(false);
                }
            }
            else if (record.Type == ActionType.Destroy)
            {
                if (record.DestroyedPart != null) record.DestroyedPart.SetActive(false);
            }

            undoStack.Push(record);
        }
    }

    private void ClearHistory()
    {
        while (undoStack.Count > 0) CleanUpAction(undoStack.Pop());
        while (redoStack.Count > 0) CleanUpAction(redoStack.Pop());
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
        ClearHistory();

        var partsToRemove = allParts.Keys.Where(key => rootModel != null && key != rootModel.name).ToList();
        foreach (var key in partsToRemove)
        {
            if (allParts.TryGetValue(key, out GameObject part))
            {
                Destroy(part);
            }
            allParts.Remove(key);
        }

        if (rootModel != null)
        {
            rootModel.SetActive(true);
            if (!allParts.ContainsKey(rootModel.name))
            {
                allParts.Add(rootModel.name, rootModel);
            }
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

    public void StartShaking(string partID)
    {
        if (allParts.TryGetValue(partID, out GameObject partToShake))
        {
            if (!shakingCoroutines.ContainsKey(partToShake))
            {
                originalLocalPositions[partToShake] = partToShake.transform.localPosition;
                Coroutine shakeCoroutine = StartCoroutine(ShakeCoroutine(partToShake));
                shakingCoroutines[partToShake] = shakeCoroutine;
            }
        }
    }

    public void StopShaking(string partID, bool resetPosition = true)
    {
        if (allParts.TryGetValue(partID, out GameObject partToStop))
        {
            if (shakingCoroutines.TryGetValue(partToStop, out Coroutine shakeCoroutine))
            {
                StopCoroutine(shakeCoroutine);
                shakingCoroutines.Remove(partToStop);
                shakeOffsets.Remove(partToStop);

                if (resetPosition && originalLocalPositions.TryGetValue(partToStop, out Vector3 originalPos))
                {
                    partToStop.transform.localPosition = originalPos;
                    originalLocalPositions.Remove(partToStop);
                }
            }
        }
    }

    private IEnumerator ShakeCoroutine(GameObject targetObject)
    {
        if (!originalLocalPositions.ContainsKey(targetObject)) yield break;
        Transform targetTransform = targetObject.transform;
        Vector3 startPosition = originalLocalPositions[targetObject];
        float seedX = Random.Range(0f, 100f);
        float seedY = Random.Range(0f, 100f);
        float seedZ = Random.Range(0f, 100f);

        while (true)
        {
            float x = (Mathf.PerlinNoise(seedX, Time.time * shakeRoughness) - 0.5f) * 2f * shakeMagnitude;
            float y = (Mathf.PerlinNoise(seedY, Time.time * shakeRoughness) - 0.5f) * 2f * shakeMagnitude;
            float z = (Mathf.PerlinNoise(seedZ, Time.time * shakeRoughness) - 0.5f) * 2f * shakeMagnitude;

            Vector3 offset = new Vector3(x, y, z);
            shakeOffsets[targetObject] = offset;
            targetTransform.localPosition = startPosition + offset;
            yield return null;
        }
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
        shaft.transform.SetParent(parentRef, false);
        Destroy(shaft.GetComponent<CapsuleCollider>());
        shaft.transform.localScale = new Vector3(thickness, shaftActualLength / 2f, thickness);
        shaft.transform.localPosition = serverAxisOriginOffset + direction * (shaftActualLength / 2f);
        shaft.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction);
        ApplyServerMaterial(shaft.GetComponent<Renderer>(), color);
        currentModelAxisVisuals.Add(shaft);

        GameObject arrowheadCap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        arrowheadCap.name = baseName + "_HeadCap";
        arrowheadCap.transform.SetParent(parentRef, false);
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