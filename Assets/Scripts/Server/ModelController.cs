using UnityEngine;
using System.Collections.Generic;
using EzySlice;
using System.Linq;
using System.Collections;
using UnityEngine.UI;
using UnityVolumeRendering;

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

    [Header("Available Models")]
    [SerializeField] private List<ModelData> availableModels = new();

    private Dictionary<string, ModelData> modelDataLookup = new();

    [Header("Cutting Components")]
    [SerializeField] private Material crossSectionMaterial;
    [SerializeField] private GameObject planeVisualizerPrefab;
    [SerializeField] private GameObject lineRendererPrefab;
    [SerializeField] private bool showPlaneVisualizer = true;

    [Header("Axis Visuals (Server)")]
    [SerializeField] private bool showServerAxes = true;
    [SerializeField] private Vector3 serverAxisOriginOffset = new Vector3(0f, 0f, 0f);

    [Header("Shaking Effect")]
    [SerializeField] private Vector3 wiggleAxis = Vector3.up;

    [Header("UI Feedback")]
    [SerializeField] private Camera serverCamera;
    [SerializeField] private RectTransform uiCanvasRectTransform;
    [SerializeField] private Image feedbackIconImage;
    [SerializeField] private Sprite destroyIconSprite;
    [SerializeField] private Sprite sliceIconSprite;

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
    private Dictionary<GameObject, Quaternion> originalLocalRotations = new Dictionary<GameObject, Quaternion>();

    private Vector3 refPointLocalPosition;
    private Quaternion refPointLocalRotation;

    public string CurrentModelID { get; private set; } = null;

    public Vector3 CurrentModelBoundsSize { get; private set; } = Vector3.one;

    void Awake()
    {
        foreach (var modelData in availableModels)
        {
            if (!string.IsNullOrEmpty(modelData.modelID))
            {
                modelDataLookup[modelData.modelID] = modelData;
            }
        }
        if (feedbackIconImage != null)
        {
            feedbackIconImage.gameObject.SetActive(false);
        }
        if (serverCamera == null)
        {
            Debug.LogError("[ModelController] Server Camera is not assigned. UI feedback will not work.");
        }
        if (uiCanvasRectTransform == null)
        {
            Debug.LogError("[ModelController] UI Canvas Rect Transform is not assigned. UI feedback will not work.");
        }
    }

    void LateUpdate()
    {
        if (axesContainer != null && worldContainer != null && modelReferencePoint != null)
        {
            axesContainer.transform.position = worldContainer.transform.TransformPoint(refPointLocalPosition);
            axesContainer.transform.rotation = worldContainer.transform.rotation * refPointLocalRotation;
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

        if (feedbackIconImage != null)
        {
            feedbackIconImage.gameObject.SetActive(false);
        }

        shakingCoroutines.Clear();
        originalLocalRotations.Clear();
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

        refPointLocalPosition = Vector3.zero;
        refPointLocalRotation = Quaternion.identity;
    }

    public void LoadNewModel(string modelId)
    {
        UnloadCurrentModel();

        if (!modelDataLookup.TryGetValue(modelId, out ModelData modelData))
        {
            Debug.LogError($"Model ID '{modelId}' not found!");
            return;
        }

        SetupContainers();
        CurrentModelID = modelId;

        GameObject rootModel = null;

        if (modelData is VolumetricModelData volumetricData)
        {
            rootModel = LoadVolumetricModel(volumetricData);
        }
        else if (modelData is PolygonalModelData polygonalData)
        {
            rootModel = LoadPrefabModel(polygonalData);
        }

        rootModel.name = "RootModel";
        allParts.Add(rootModel.name, rootModel);


#if UNITY_EDITOR
        if (modelData.boundsSize == Vector3.one)
        {
            CurrentModelBoundsSize = CalculateModelBoundsSize(rootModel);
            modelData.boundsSize = CurrentModelBoundsSize;
            UnityEditor.EditorUtility.SetDirty(modelData);
        }
#endif

        SetupReferencePoint(rootModel);

        SetupVisualHelpers();
    }

    private void SetupContainers()
    {
        worldContainer = new GameObject("WorldContainer");
        worldContainer.transform.SetParent(this.transform, false);

        modelContainer = new GameObject("ModelContainer");
        modelContainer.transform.SetParent(worldContainer.transform, false);

        axesContainer = new GameObject("AxesContainer");
        axesContainer.transform.SetParent(worldContainer.transform, false);
    }

    private GameObject LoadVolumetricModel(VolumetricModelData data)
    {
        RawDatasetImporter importer = new RawDatasetImporter(
            data.rawFilePath,
            data.dimX, data.dimY, data.dimZ,
            data.contentFormat,
            data.endianness,
            data.bytesToSkip
        );
        VolumeDataset dataset = importer.Import();
        VolumeRenderedObject volObj = VolumeObjectFactory.CreateObject(dataset);
        volObj.gameObject.transform.SetParent(modelContainer.transform, false);
        return volObj.gameObject;
    }

    private GameObject LoadPrefabModel(PolygonalModelData data)
    {
        if (data.prefab == null) return null;
        return Instantiate(data.prefab, modelContainer.transform);
    }


    private void SetupReferencePoint(GameObject rootModel)
    {
        modelReferencePoint = rootModel.transform.Find("ref");
        if (modelReferencePoint == null) modelReferencePoint = rootModel.transform;

        bool isVolumetricData = rootModel.GetComponent<VolumeRenderedObject>() != null;

        if (isVolumetricData)
        {
            refPointLocalPosition = new Vector3(-0.6f, -0.4f, -0.5f);
        }
        else
        {
            refPointLocalPosition = worldContainer.transform.InverseTransformPoint(modelReferencePoint.position);
            refPointLocalRotation = Quaternion.Inverse(worldContainer.transform.rotation) * modelReferencePoint.rotation;
        }
    }


    private void SetupVisualHelpers()
    {
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
        StopShaking(data.targetPartID, true);
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

    public void ShowSliceIcon(Vector3 worldPosition)
    {
        if (feedbackIconImage != null && sliceIconSprite != null && serverCamera != null && uiCanvasRectTransform != null)
        {
            Vector2 screenPoint = serverCamera.WorldToScreenPoint(worldPosition);

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                uiCanvasRectTransform,
                screenPoint,
                null,
                out Vector2 localPoint);

            feedbackIconImage.sprite = sliceIconSprite;
            feedbackIconImage.rectTransform.anchoredPosition = localPoint;
            feedbackIconImage.gameObject.SetActive(true);
        }
    }

    public void HideSliceIcon()
    {
        if (feedbackIconImage != null && feedbackIconImage.sprite == sliceIconSprite)
        {
            feedbackIconImage.gameObject.SetActive(false);
        }
    }

    public void StartShaking(string partID, Vector3 receivedWorldPosition)
    {
        if (allParts.TryGetValue(partID, out GameObject serverPartToShake))
        {
            if (!shakingCoroutines.ContainsKey(serverPartToShake))
            {
                originalLocalRotations[serverPartToShake] = serverPartToShake.transform.localRotation;
                Coroutine shakeCoroutine = StartCoroutine(ShakeCoroutine(serverPartToShake));
                shakingCoroutines[serverPartToShake] = shakeCoroutine;

                if (feedbackIconImage != null && destroyIconSprite != null && serverCamera != null && uiCanvasRectTransform != null)
                {
                    Vector2 screenPoint = serverCamera.WorldToScreenPoint(receivedWorldPosition);

                    RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        uiCanvasRectTransform,
                        screenPoint,
                        null,
                        out Vector2 localPoint);

                    feedbackIconImage.sprite = destroyIconSprite;
                    feedbackIconImage.rectTransform.anchoredPosition = localPoint;
                    feedbackIconImage.gameObject.SetActive(true);
                }
            }
        }
    }

    public void StopShaking(string partID, bool resetPosition = true)
    {
        if (feedbackIconImage != null && feedbackIconImage.sprite == destroyIconSprite)
        {
            feedbackIconImage.gameObject.SetActive(false);
        }

        if (allParts.TryGetValue(partID, out GameObject partToStop))
        {
            if (shakingCoroutines.TryGetValue(partToStop, out Coroutine shakeCoroutine))
            {
                StopCoroutine(shakeCoroutine);
                shakingCoroutines.Remove(partToStop);

                if (resetPosition && originalLocalRotations.TryGetValue(partToStop, out Quaternion originalRot))
                {
                    partToStop.transform.localRotation = originalRot;
                    originalLocalRotations.Remove(partToStop);
                }
            }
        }
    }

    private IEnumerator ShakeCoroutine(GameObject targetObject)
    {
        if (!originalLocalRotations.ContainsKey(targetObject)) yield break;

        Transform targetTransform = targetObject.transform;
        Quaternion startRotation = originalLocalRotations[targetObject];

        while (true)
        {
            float angle = Mathf.Sin(Time.time * Constants.WIGGLE_SPEED) * Constants.WIGGLE_ANGLE;
            Quaternion offsetRotation = Quaternion.AngleAxis(angle, wiggleAxis);
            targetTransform.localRotation = startRotation * offsetRotation;
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
        CreateSingleServerAxisVisual(axesParentTransform, Vector3.right, Constants.AXIS_LENGTH, Constants.AXIS_THICKNESS, Color.red, "X_Axis_Server");
        CreateSingleServerAxisVisual(axesParentTransform, Vector3.up, Constants.AXIS_LENGTH, Constants.AXIS_THICKNESS, Color.green, "Y_Axis_Server");
        CreateSingleServerAxisVisual(axesParentTransform, Vector3.forward, Constants.AXIS_LENGTH, Constants.AXIS_THICKNESS, Color.blue, "Z_Axis_Server");
    }

    void CreateSingleServerAxisVisual(Transform parentRef, Vector3 direction, float length, float thickness, Color color, string baseName)
    {
        float capHeight = thickness * Constants.ARROWHEAD_HEIGHT_FACTOR;
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
        float capRadius = thickness * Constants.ARROWHEAD_RADIUS_FACTOR;
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