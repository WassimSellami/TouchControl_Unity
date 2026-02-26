using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityVolumeRendering;
using System.IO;
using System.Linq;

public class ModelController : MonoBehaviour, IModelViewer
{
    private LineRenderer activeLineRenderer;
    private GameObject activePlaneVisualizer;
    private readonly Dictionary<string, GameObject> allParts = new();

    [Header("Available Models")]
    [SerializeField] private List<ModelData> availableModels = new List<ModelData>();
    private GameObject axesContainer;

    [Header("Cutting Components")]
    [SerializeField] private Material crossSectionMaterial;
    [SerializeField] private RectTransform uiCanvasRectTransform;
    [SerializeField] private Sprite destroyIconSprite;
    [SerializeField] private Sprite sliceIconSprite;
    [SerializeField] private Image feedbackIconImage;
    [SerializeField] private GameObject lineRendererPrefab;
    private GameObject modelContainer;

    private readonly Dictionary<string, ModelData> modelDataLookup = new();
    private Transform modelReferencePoint;
    private readonly Dictionary<GameObject, Quaternion> originalLocalRotations = new();
    [SerializeField] private GameObject planeVisualizerPrefab;
    private readonly Stack<ActionRecord> redoStack = new();

    private Vector3 refPointLocalPosition;
    private Quaternion refPointLocalRotation;
    private GameObject rootModel;
    [SerializeField] private Vector3 serverAxisOriginOffset = Vector3.zero;

    [Header("UI Feedback")]
    [SerializeField] private Camera serverCamera;
    private readonly Dictionary<GameObject, Coroutine> shakingCoroutines = new();
    [SerializeField] private bool showPlaneVisualizer = true;
    [SerializeField] private ServerLoadingUI loadingUI; 

    [Header("Axis Visuals (Server)")]
    private List<GameObject> currentModelAxisVisuals = new();
    [SerializeField] private bool showServerAxes = true;

    private Stack<ActionRecord> undoStack = new();

    [Header("Volumetric Settings")]
    [SerializeField] private Material volumetricSliceMaterial;
    [Range(0f, 1f)] public float volumeMinVal = 0.0f;
    [Range(0f, 1f)] public float volumeMaxVal = 1.0f;

    [Header("Shaking Effect")]
    [SerializeField] private Vector3 wiggleAxis = Vector3.up;

    private GameObject worldContainer;
    private WebSocketServerManager wsManager;
    private Vector3 currentModelCenterOffset;

    private Coroutine loadCoroutine; // ADDED TRACKING
    private string RegistryPath => Path.Combine(Application.persistentDataPath, "external_registry.json");
    private Dictionary<string, string> runtimeFileSizes = new Dictionary<string, string>();
    private Dictionary<string, string> runtimeThumbPaths = new Dictionary<string, string>();


    void Awake()
    {
        LoadRegistry();
        RefreshModelLookup();
        if (feedbackIconImage != null) { feedbackIconImage.gameObject.SetActive(false); feedbackIconImage.rectTransform.pivot = new Vector2(0.5f, 0.5f); }
        wsManager = FindObjectOfType<WebSocketServerManager>();
    }

    private void OnValidate()
    {
        if (Application.isPlaying) SetVolumeDensity(volumeMinVal, volumeMaxVal);
    }

    void LateUpdate()
    {
        UpdateAxisScale();
    }

    private void UpdateAxisScale()
    {
        if (axesContainer == null || serverCamera == null) return;

        float distance = Vector3.Distance(serverCamera.transform.position, axesContainer.transform.position);

        if (serverCamera.orthographic)
        {
            distance = serverCamera.orthographicSize * 2f;
        }

        // Apply the same constant factor
        axesContainer.transform.localScale = Vector3.one * (distance * Constants.AXIS_SCREEN_SCALE);
    }

    public void RemoveModel(string modelID)
    {
        if (availableModels == null) return;
        var modelToRemove = availableModels.Find(m => m != null && m.modelID == modelID);

        if (modelToRemove != null)
        {
            availableModels.Remove(modelToRemove);
            if (runtimeFileSizes != null && runtimeFileSizes.ContainsKey(modelID)) runtimeFileSizes.Remove(modelID);
            RefreshModelLookup();
            SaveRegistry();
            if (wsManager != null) wsManager.BroadcastModelList();
            ServerModelUIPanel serverUI = FindObjectOfType<ServerModelUIPanel>();
            if (serverUI != null) serverUI.PopulateServerList(GetAllModelsMetadata().models.ToList());
        }
    }

    public void SetVolumeDensity(float min, float max)
    {
        volumeMinVal = min;
        volumeMaxVal = max;

        if (modelContainer == null) return;
        Renderer[] renderers = modelContainer.GetComponentsInChildren<Renderer>();
        foreach (Renderer rend in renderers)
        {
            if (rend.gameObject.name.Contains("Shaft") || rend.gameObject.name.Contains("Head")) continue;
            if (rend.material.HasProperty("_MinVal")) rend.material.SetFloat("_MinVal", min);
            if (rend.material.HasProperty("_MaxVal")) rend.material.SetFloat("_MaxVal", max);
        }
    }

    private void ApplyDensityToPart(GameObject part)
    {
        Renderer[] renderers = part.GetComponentsInChildren<Renderer>();
        foreach (Renderer rend in renderers)
        {
            if (rend.gameObject.name.Contains("Shaft") || rend.gameObject.name.Contains("Head")) continue;
            if (rend.material.HasProperty("_MinVal")) rend.material.SetFloat("_MinVal", volumeMinVal);
            if (rend.material.HasProperty("_MaxVal")) rend.material.SetFloat("_MaxVal", volumeMaxVal);
        }
    }

    private void RefreshModelLookup()
    {
        modelDataLookup.Clear();
        foreach (var modelData in availableModels)
        {
            if (modelData != null && !string.IsNullOrEmpty(modelData.modelID))
                modelDataLookup[modelData.modelID] = modelData;
        }
    }

    public void LoadNewModel(string modelId)
    {
        if (loadCoroutine != null) StopCoroutine(loadCoroutine);
        loadCoroutine = StartCoroutine(LoadModelRoutine(modelId));
    }

    private IEnumerator LoadModelRoutine(string modelId)
    {
        UnloadCurrentModel();
        if (!modelDataLookup.TryGetValue(modelId, out ModelData modelData)) yield break;

        if (loadingUI != null) loadingUI.Show(modelData.displayName);

        // Wait one frame so the Spinner/Loading UI can actually appear before the main thread gets heavy
        yield return null;

        SetupContainers();
        CurrentModelID = modelId;

        // ModelLoader.Load is synchronous, but by putting it in a coroutine after a yield, 
        // the server UI shows the spinner first.
        GameObject root = ModelLoader.Load(modelData, modelContainer.transform, volumetricSliceMaterial);

        if (root != null)
        {
            rootModel = root;
            rootModel.name = "RootModel";
            allParts.Add(rootModel.name, rootModel);
            AlignToCorner(rootModel);
            GenerateAndBroadcastProxy(modelId);

            volumeMinVal = 0.0f;
            volumeMaxVal = 1.0f;
            SetVolumeDensity(volumeMinVal, volumeMaxVal);
        }

        showServerAxes = true;
        SetupVisualHelpers();

        if (loadingUI != null) loadingUI.Hide();

        // Notify client load is complete
        if (wsManager != null)
        {
            wsManager.BroadcastModelLoaded(modelId);
            wsManager.SendModelSizeUpdate(CurrentModelBoundsSize);
        }

        loadCoroutine = null;
    }

    public void UnloadCurrentModel()
    {
        if (loadCoroutine != null)
        {
            StopCoroutine(loadCoroutine);
            loadCoroutine = null;
        }

        if (loadingUI != null) loadingUI.Hide();

        foreach (var coroutine in shakingCoroutines.Values) if (coroutine != null) StopCoroutine(coroutine);
        if (feedbackIconImage != null) feedbackIconImage.gameObject.SetActive(false);
        if (worldContainer != null) Destroy(worldContainer);

        shakingCoroutines.Clear();
        originalLocalRotations.Clear();
        allParts.Clear();
        undoStack.Clear();
        redoStack.Clear();
        ClearCurrentModelAxisVisuals();
        modelReferencePoint = null;
        rootModel = null;
        worldContainer = null;
        modelContainer = null;
        axesContainer = null;
        CurrentModelID = null;
    }

    // --- EXISTING REGISTRY AND METADATA METHODS ---

    [Serializable] public class ModelRegistryEntry { public string id; public string name; public string desc; public string filePath; public string size; public string thumbPath; public bool isVolumetric; public int dx, dy, dz; }
    [Serializable] public class RegistryWrapper { public List<ModelRegistryEntry> entries = new List<ModelRegistryEntry>(); }

    public void RegisterRuntimeModel(ModelData newModel, string sizeStr, string thumbPath)
    {
        if (newModel == null) return;
        availableModels.Add(newModel);
        runtimeFileSizes[newModel.modelID] = sizeStr;
        runtimeThumbPaths[newModel.modelID] = thumbPath;
        RefreshModelLookup();
        SaveRegistry();
        if (wsManager != null) wsManager.BroadcastModelList();
        ServerModelUIPanel serverUI = FindObjectOfType<ServerModelUIPanel>();
        if (serverUI != null) serverUI.PopulateServerList(GetAllModelsMetadata().models.ToList());
    }

    private void SaveRegistry()
    {
        RegistryWrapper wrapper = new RegistryWrapper();
        foreach (var m in availableModels)
        {
            string path = ""; bool isVol = false; int x = 0, y = 0, z = 0;
            if (m is PolygonalModelData p) path = p.modelFilePath;
            else if (m is VolumetricModelData v) { path = v.rawFilePath; isVol = true; x = v.dimX; y = v.dimY; z = v.dimZ; }
            if (string.IsNullOrEmpty(path)) continue;
            wrapper.entries.Add(new ModelRegistryEntry { id = m.modelID, name = m.displayName, desc = m.description, filePath = path, size = runtimeFileSizes.ContainsKey(m.modelID) ? runtimeFileSizes[m.modelID] : m.fileSize, thumbPath = runtimeThumbPaths.ContainsKey(m.modelID) ? runtimeThumbPaths[m.modelID] : "", isVolumetric = isVol, dx = x, dy = y, dz = z });
        }
        File.WriteAllText(RegistryPath, JsonUtility.ToJson(wrapper));
    }

    private void LoadRegistry()
    {
        if (!File.Exists(RegistryPath)) return;
        try
        {
            RegistryWrapper wrapper = JsonUtility.FromJson<RegistryWrapper>(File.ReadAllText(RegistryPath));
            foreach (var e in wrapper.entries)
            {
                if (!File.Exists(e.filePath)) continue;
                runtimeFileSizes[e.id] = e.size; runtimeThumbPaths[e.id] = e.thumbPath;
                Sprite loadedThumb = null;
                if (!string.IsNullOrEmpty(e.thumbPath) && File.Exists(e.thumbPath))
                {
                    byte[] bytes = File.ReadAllBytes(e.thumbPath); Texture2D tex = new Texture2D(2, 2); tex.LoadImage(bytes);
                    loadedThumb = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.one * 0.5f);
                }
                if (e.isVolumetric)
                {
                    VolumetricModelData v = ScriptableObject.CreateInstance<VolumetricModelData>();
                    v.modelID = e.id; v.displayName = e.name; v.description = e.desc; v.rawFilePath = e.filePath; v.dimX = e.dx; v.dimY = e.dy; v.dimZ = e.dz; v.fileSize = e.size; v.thumbnail = loadedThumb; availableModels.Add(v);
                }
                else
                {
                    PolygonalModelData p = ScriptableObject.CreateInstance<PolygonalModelData>();
                    p.modelID = e.id; p.displayName = e.name; p.description = e.desc; p.modelFilePath = e.filePath; p.fileSize = e.size; p.thumbnail = loadedThumb; availableModels.Add(p);
                }
            }
        }
        catch { }
    }

    private void GenerateAndBroadcastProxy(string modelId)
    {
        if (!modelDataLookup.TryGetValue(modelId, out ModelData data)) return;
        MeshNetworkData proxyData = null;
        if (data is PolygonalModelData) proxyData = AutoMeshProxy.GenerateFromMesh(rootModel);
        else if (data is VolumetricModelData volData) proxyData = AutoMeshProxy.GenerateFromVolume(volData);
        if (proxyData != null && wsManager != null) wsManager.BroadcastCustomCommand("LOAD_PROXY_MESH", JsonUtility.ToJson(proxyData));
    }

    public ModelMetadataList GetAllModelsMetadata()
    {
        var metadataList = new List<ModelMetadata>();
        foreach (var modelData in availableModels)
        {
            if (modelData == null) continue;
            string typeLabel = (modelData is VolumetricModelData) ? "Volumetric" : "Polygonal";
            string size = runtimeFileSizes.ContainsKey(modelData.modelID) ? runtimeFileSizes[modelData.modelID] : modelData.fileSize;
            metadataList.Add(new ModelMetadata { modelID = modelData.modelID, displayName = modelData.displayName, description = modelData.description, thumbnailBase64 = SpriteToBase64(modelData.thumbnail), modelType = typeLabel, fileSize = size });
        }
        return new ModelMetadataList { models = metadataList.ToArray() };
    }

    public void ApplyWorldTransform(Vector3 localPosition, Quaternion localRotation, Vector3 localScale) { if (worldContainer != null) { worldContainer.transform.localPosition = localPosition; worldContainer.transform.localRotation = localRotation; worldContainer.transform.localScale = localScale; } }

    public void ExecuteDestroy(DestroyActionData data)
    {
        StopShaking(data.targetPartID, true);
        while (redoStack.Count > 0) CleanUpAction(redoStack.Pop());
        if (allParts.TryGetValue(data.targetPartID, out GameObject partToDestroy)) { partToDestroy.SetActive(false); undoStack.Push(new ActionRecord { Type = ActionType.Destroy, ActionID = data.actionID, DestroyedPart = partToDestroy }); }
    }

    public void ExecuteSlice(SliceActionData data)
    {
        while (redoStack.Count > 0) CleanUpAction(redoStack.Pop());
        var record = new ActionRecord { Type = ActionType.Slice, ActionID = data.actionID, Originals = new List<GameObject>(), NewHulls = new List<GameObject>() };
        foreach (string partID in data.targetPartIDs)
        {
            if (allParts.TryGetValue(partID, out GameObject originalPart) && originalPart.activeInHierarchy)
            {
                if (originalPart.GetComponentInChildren<VolumeRenderedObject>() != null) ExecuteVolumetricSlice(originalPart, data, record);
                else ExecuteMeshSlice(originalPart, data, record);
            }
        }
        if (record.Originals.Count > 0) undoStack.Push(record);
    }

    public void HideCutLine() { if (activeLineRenderer != null) activeLineRenderer.enabled = false; }
    public void HideSliceIcon() { if (feedbackIconImage != null && feedbackIconImage.sprite == sliceIconSprite) feedbackIconImage.gameObject.SetActive(false); }
    public void RedoLastAction()
    {
        if (redoStack.Count == 0) return;
        ActionRecord record = redoStack.Pop();
        if (record.Type == ActionType.Slice) { foreach (var hull in record.NewHulls) if (hull != null) hull.SetActive(true); foreach (var part in record.Originals) if (part != null) part.SetActive(false); }
        else if (record.Type == ActionType.Destroy && record.DestroyedPart != null) record.DestroyedPart.SetActive(false);
        undoStack.Push(record);
    }

    public void ResetCrop()
    {
        while (undoStack.Count > 0) CleanUpAction(undoStack.Pop());
        while (redoStack.Count > 0) CleanUpAction(redoStack.Pop());
        if (modelContainer == null) return;
        for (int i = modelContainer.transform.childCount - 1; i >= 0; i--) { GameObject child = modelContainer.transform.GetChild(i).gameObject; if (rootModel != null && child == rootModel) continue; Destroy(child); }
        if (rootModel != null) { rootModel.SetActive(true); allParts.Clear(); allParts.Add(rootModel.name, rootModel); }
        if (activePlaneVisualizer != null) activePlaneVisualizer.SetActive(false);
    }

    public void ResetState() { UnloadCurrentModel(); transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); transform.localScale = Vector3.one; }
    public void SetModelVisibility(bool isVisible) { if (rootModel != null) rootModel.SetActive(isVisible); }
    public void ShowSliceIcon(Vector3 worldPosition) { ShowLocalServerIcon(worldPosition, sliceIconSprite); }
    public void StartShaking(string partID, Vector3 receivedWorldPosition) { if (allParts.TryGetValue(partID, out GameObject serverPartToShake)) { if (!shakingCoroutines.ContainsKey(serverPartToShake)) { Quaternion originalRot = serverPartToShake.transform.localRotation; originalLocalRotations[serverPartToShake] = originalRot; shakingCoroutines[serverPartToShake] = StartCoroutine(InteractionUtility.ShakeCoroutine(serverPartToShake.transform, originalRot, wiggleAxis)); ShowLocalServerIcon(receivedWorldPosition, destroyIconSprite); } } }
    public void StopShaking(string partID, bool resetPosition = true) { if (feedbackIconImage != null && feedbackIconImage.sprite == destroyIconSprite) feedbackIconImage.gameObject.SetActive(false); if (allParts.TryGetValue(partID, out GameObject partToStop)) { if (shakingCoroutines.TryGetValue(partToStop, out Coroutine shakeCoroutine)) { StopCoroutine(shakeCoroutine); shakingCoroutines.Remove(partToStop); if (resetPosition && originalLocalRotations.TryGetValue(partToStop, out Quaternion originalRot)) { partToStop.transform.localRotation = originalRot; originalLocalRotations.Remove(partToStop); } } } }
    public void UndoLastAction()
    {
        if (undoStack.Count == 0) return;
        ActionRecord record = undoStack.Pop();
        if (record.Type == ActionType.Slice) { foreach (var hull in record.NewHulls) if (hull != null) hull.SetActive(false); foreach (var part in record.Originals) if (part != null) part.SetActive(true); }
        else if (record.Type == ActionType.Destroy && record.DestroyedPart != null) record.DestroyedPart.SetActive(true);
        redoStack.Push(record);
    }

    public void UpdateCutLine(Vector3 start, Vector3 end) { if (activeLineRenderer != null) activeLineRenderer.enabled = true; activeLineRenderer.positionCount = 2; activeLineRenderer.SetPosition(0, start); activeLineRenderer.SetPosition(1, end); }
    public void UpdateVisualCropPlane(Vector3 position, Vector3 normal, float scale) { if (activePlaneVisualizer == null) return; activePlaneVisualizer.transform.SetPositionAndRotation(position, Quaternion.LookRotation(normal)); activePlaneVisualizer.transform.localScale = Vector3.one * scale; activePlaneVisualizer.SetActive(showPlaneVisualizer); }
    public Vector3 CurrentModelBoundsSize { get; private set; } = Vector3.one;
    public string CurrentModelID { get; private set; } = null;
    public void SetAxesVisibility(bool visible) { showServerAxes = visible; if (axesContainer != null) axesContainer.SetActive(showServerAxes); }
    private void ClearCurrentModelAxisVisuals() { foreach (GameObject vis in currentModelAxisVisuals) if (vis != null) Destroy(vis); currentModelAxisVisuals.Clear(); }
    private Bounds GetBounds(GameObject go) { Renderer[] rends = go.GetComponentsInChildren<Renderer>(); if (rends.Length == 0) return new Bounds(go.transform.position, Vector3.one); Bounds b = rends[0].bounds; for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds); return b; }
    private IEnumerator AnimateSeparation(GameObject upperHull, GameObject lowerHull, GameObject original, Vector3 planeNormal, float separationFactor) { float duration = 0.3f; Bounds originalBounds = GetBounds(original); Vector3 separationVector = planeNormal * (originalBounds.size.magnitude * separationFactor * 0.5f); Vector3 upperStart = upperHull.transform.position, lowerStart = lowerHull.transform.position; Vector3 upperEnd = upperStart + separationVector, lowerEnd = lowerStart - separationVector; float elapsed = 0f; while (elapsed < duration) { if (upperHull == null || lowerHull == null) yield break; float t = Mathf.SmoothStep(0f, 1f, elapsed / duration); upperHull.transform.position = Vector3.Lerp(upperStart, upperEnd, t); lowerHull.transform.position = Vector3.Lerp(lowerStart, lowerEnd, t); elapsed += Time.deltaTime; yield return null; } if (upperHull != null) upperHull.transform.position = upperEnd; if (lowerHull != null) lowerHull.transform.position = lowerEnd; }
    private void ApplyVolumeCut(GameObject root, Vector3 texturePoint, Vector3 worldNormal, bool invertNormal) { Renderer[] renderers = root.GetComponentsInChildren<Renderer>(); foreach (Renderer rend in renderers) { if (rend.gameObject.name.Contains("Shaft") || rend.gameObject.name.Contains("Head")) continue; Vector3 localNormal = rend.transform.InverseTransformDirection(worldNormal); if (invertNormal) localNormal = -localNormal; rend.material.SetVector("_PlanePos", texturePoint); rend.material.SetVector("_PlaneNormal", localNormal); } }
    private void CleanUpAction(ActionRecord record) { if (record.Type == ActionType.Slice) foreach (var hull in record.NewHulls) { if (hull != null) { allParts.Remove(hull.name); Destroy(hull); } } }

    private void ExecuteVolumetricSlice(GameObject originalPart, SliceActionData data, ActionRecord record)
    {
        GameObject partA = Instantiate(originalPart, originalPart.transform.parent); GameObject partB = Instantiate(originalPart, originalPart.transform.parent); partA.name = originalPart.name + "_A"; partB.name = originalPart.name + "_B";
        Renderer volRend = originalPart.GetComponentInChildren<Renderer>(); if (volRend == null) return;
        Vector3 localHitPos = volRend.transform.InverseTransformPoint(data.planePoint); Vector3 textureSpacePos = localHitPos + new Vector3(0.5f, 0.5f, 0.5f);
        ApplyVolumeCut(partA, textureSpacePos, data.planeNormal, false); ApplyVolumeCut(partB, textureSpacePos, data.planeNormal, true);
        ApplyDensityToPart(partA); ApplyDensityToPart(partB);
        if (!allParts.ContainsKey(partA.name)) allParts.Add(partA.name, partA); if (!allParts.ContainsKey(partB.name)) allParts.Add(partB.name, partB);
        StartCoroutine(AnimateSeparation(partA, partB, originalPart, data.planeNormal, data.separationFactor)); record.Originals.Add(originalPart); record.NewHulls.Add(partA); record.NewHulls.Add(partB); originalPart.SetActive(false);
    }

    private void ExecuteMeshSlice(GameObject originalPart, SliceActionData data, ActionRecord record)
    {
        GameObject meshTarget = originalPart.GetComponent<MeshFilter>() != null ? originalPart : originalPart.GetComponentInChildren<MeshFilter>()?.gameObject;
        if (meshTarget == null) return;
        while (redoStack.Count > 0) CleanUpAction(redoStack.Pop());
        var result = SliceUtility.ExecuteMeshSlice(meshTarget, data.planePoint, data.planeNormal, crossSectionMaterial, this, originalPart.transform.parent);
        if (result.isValid)
        {
            result.upperHull.name = originalPart.name + "_A"; result.lowerHull.name = originalPart.name + "_B";
            if (allParts.ContainsKey(result.upperHull.name)) allParts[result.upperHull.name] = result.upperHull; else allParts.Add(result.upperHull.name, result.upperHull);
            if (allParts.ContainsKey(result.lowerHull.name)) allParts[result.lowerHull.name] = result.lowerHull; else allParts.Add(result.lowerHull.name, result.lowerHull);
            record.Originals.Add(originalPart); record.NewHulls.Add(result.upperHull); record.NewHulls.Add(result.lowerHull); originalPart.SetActive(false);
        }
    }
    private void SetupContainers() { worldContainer = new GameObject("WorldContainer"); worldContainer.transform.SetParent(this.transform, false); modelContainer = new GameObject("ModelContainer"); modelContainer.transform.SetParent(worldContainer.transform, false); axesContainer = new GameObject("AxesContainer"); axesContainer.transform.SetParent(worldContainer.transform, false); }
    private void AlignToCorner(GameObject rootModel) { Bounds b = SliceUtility.GetFullBounds(rootModel); CurrentModelBoundsSize = b.size; rootModel.transform.localPosition = -b.center; currentModelCenterOffset = -b.extents; modelReferencePoint = rootModel.transform; }
    private void SetupVisualHelpers()
    {
        if (planeVisualizerPrefab != null) { activePlaneVisualizer = Instantiate(planeVisualizerPrefab, worldContainer.transform, false); activePlaneVisualizer.SetActive(false); }
        if (lineRendererPrefab != null) { activeLineRenderer = Instantiate(lineRendererPrefab, worldContainer.transform, false).GetComponent<LineRenderer>(); activeLineRenderer.enabled = false; }
        if (axesContainer != null)
        {
            axesContainer.SetActive(showServerAxes); axesContainer.transform.localPosition = currentModelCenterOffset;
            Material matX = new Material(Shader.Find("Unlit/Color")) { color = Color.red }; Material matY = new Material(Shader.Find("Unlit/Color")) { color = Color.green }; Material matZ = new Material(Shader.Find("Unlit/Color")) { color = Color.blue };
            currentModelAxisVisuals = AxisGenerator.CreateAxes(axesContainer.transform, Constants.AXIS_LENGTH, Constants.AXIS_THICKNESS, Vector3.zero, matX, matY, matZ);
        }
    }
    private void ShowLocalServerIcon(Vector3 worldPos, Sprite icon) { if (feedbackIconImage == null || serverCamera == null || uiCanvasRectTransform == null) return; Vector3 screenPoint = serverCamera.WorldToScreenPoint(worldPos); if (screenPoint.z < 0) return; feedbackIconImage.sprite = icon; InteractionUtility.PositionIcon(feedbackIconImage, (Vector2)screenPoint, uiCanvasRectTransform, null, false); }
    private string SpriteToBase64(Sprite sprite) { if (sprite == null) return string.Empty; try { Texture2D texture = sprite.texture; RenderTexture tmp = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Linear); Graphics.Blit(texture, tmp); RenderTexture previous = RenderTexture.active; RenderTexture.active = tmp; Texture2D readableTexture = new Texture2D(texture.width, texture.height); readableTexture.ReadPixels(new Rect(0, 0, tmp.width, tmp.height), 0, 0); readableTexture.Apply(); RenderTexture.active = previous; RenderTexture.ReleaseTemporary(tmp); byte[] bytes = readableTexture.EncodeToPNG(); string base64 = Convert.ToBase64String(bytes); Destroy(readableTexture); return base64; } catch { return string.Empty; } }
    private enum ActionType { Slice, Destroy }
    private class ActionRecord { public string ActionID; public GameObject DestroyedPart; public List<GameObject> NewHulls; public List<GameObject> Originals; public ActionType Type; }
}