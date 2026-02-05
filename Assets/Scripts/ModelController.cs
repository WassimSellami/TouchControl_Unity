using EzySlice;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityVolumeRendering;

public class ModelController : MonoBehaviour, IModelViewer
{
    private LineRenderer activeLineRenderer;
    private GameObject activePlaneVisualizer;
    private readonly Dictionary<string, GameObject> allParts = new();

    [Header("Available Models")]
    [SerializeField] private List<ModelData> availableModels = new();
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

    [Header("Axis Visuals (Server)")]
    private List<GameObject> currentModelAxisVisuals = new();
    [SerializeField] private bool showServerAxes = true;

    private Stack<ActionRecord> undoStack = new();

    [Header("Volumetric Settings")]
    [SerializeField] private Material volumetricSliceMaterial;

    [Header("Shaking Effect")]
    [SerializeField] private Vector3 wiggleAxis = Vector3.up;

    private GameObject worldContainer;

    private IEnumerator AnimateSeparation(GameObject upperHull, GameObject lowerHull, GameObject original, Vector3 planeNormal, float separationFactor)
    {
        float duration = 0.3f; Bounds originalBounds = GetBounds(original);
        float separationDistance = originalBounds.size.magnitude * separationFactor;
        Vector3 separationVector = planeNormal * (separationDistance * 0.5f);
        Vector3 upperStart = upperHull.transform.position, lowerStart = lowerHull.transform.position;
        Vector3 upperEnd = upperStart + separationVector, lowerEnd = lowerStart - separationVector;
        float elapsed = 0f;
        while (elapsed < duration) { if (upperHull == null || lowerHull == null) yield break; float t = Mathf.SmoothStep(0f, 1f, elapsed / duration); upperHull.transform.position = Vector3.Lerp(upperStart, upperEnd, t); lowerHull.transform.position = Vector3.Lerp(lowerStart, lowerEnd, t); elapsed += Time.deltaTime; yield return null; }
        if (upperHull != null) upperHull.transform.position = upperEnd; if (lowerHull != null) lowerHull.transform.position = lowerEnd;
    }

    private void ApplyVolumeCut(GameObject root, Vector3 texturePoint, Vector3 worldNormal, bool invertNormal)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        foreach (Renderer rend in renderers)
        {
            if (rend.gameObject.name.Contains("Shaft") || rend.gameObject.name.Contains("Head")) continue;
            Vector3 localNormal = rend.transform.InverseTransformDirection(worldNormal);
            if (invertNormal) localNormal = -localNormal;
            rend.material.SetVector("_PlanePos", texturePoint);
            rend.material.SetVector("_PlaneNormal", localNormal);
        }
    }

    void Awake()
    {
        foreach (var modelData in availableModels) { if (!string.IsNullOrEmpty(modelData.modelID)) modelDataLookup[modelData.modelID] = modelData; }
        if (feedbackIconImage != null) { feedbackIconImage.gameObject.SetActive(false); feedbackIconImage.rectTransform.pivot = new Vector2(0.5f, 0.5f); }
    }

    private void CleanUpAction(ActionRecord record)
    {
        if (record.Type == ActionType.Slice) foreach (var hull in record.NewHulls) { if (hull != null) { allParts.Remove(hull.name); Destroy(hull); } }
    }
    void ClearCurrentModelAxisVisuals() { foreach (GameObject vis in currentModelAxisVisuals) if (vis != null) Destroy(vis); currentModelAxisVisuals.Clear(); }

    private void ExecuteMeshSlice(GameObject originalPart, SliceActionData data, ActionRecord record)
    {
        var result = SliceUtility.ExecuteMeshSlice(
            originalPart,
            data.planePoint,
            data.planeNormal,
            crossSectionMaterial,
            this,
            originalPart.transform.parent
        );

        if (result.isValid)
        {
            allParts.Add(result.upperHull.name, result.upperHull);
            allParts.Add(result.lowerHull.name, result.lowerHull);

            record.Originals.Add(originalPart);
            record.NewHulls.Add(result.upperHull);
            record.NewHulls.Add(result.lowerHull);

            originalPart.SetActive(false);
        }
    }

    private void ExecuteVolumetricSlice(GameObject originalPart, SliceActionData data, ActionRecord record)
    {
        GameObject upperHull = Instantiate(originalPart, originalPart.transform.parent);
        GameObject lowerHull = Instantiate(originalPart, originalPart.transform.parent);
        upperHull.name = originalPart.name + "_U"; lowerHull.name = originalPart.name + "_L";

        Renderer volRend = originalPart.GetComponentInChildren<Renderer>();
        if (volRend == null) return;

        Vector3 localHitPos = volRend.transform.InverseTransformPoint(data.planePoint);
        Vector3 textureSpacePos = localHitPos + new Vector3(0.5f, 0.5f, 0.5f);

        ApplyVolumeCut(upperHull, textureSpacePos, data.planeNormal, false);
        ApplyVolumeCut(lowerHull, textureSpacePos, data.planeNormal, true);

        allParts.Add(upperHull.name, upperHull); allParts.Add(lowerHull.name, lowerHull);
        StartCoroutine(AnimateSeparation(upperHull, lowerHull, originalPart, data.planeNormal, data.separationFactor));

        record.Originals.Add(originalPart); record.NewHulls.Add(upperHull); record.NewHulls.Add(lowerHull);
        originalPart.SetActive(false);
    }

    private Bounds GetBounds(GameObject go) { Renderer[] rends = go.GetComponentsInChildren<Renderer>(); if (rends.Length == 0) return new Bounds(go.transform.position, Vector3.one); Bounds b = rends[0].bounds; for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds); return b; }

    void LateUpdate()
    {
        if (axesContainer != null && worldContainer != null && modelReferencePoint != null)
        {
            axesContainer.transform.position = worldContainer.transform.TransformPoint(refPointLocalPosition);
            axesContainer.transform.rotation = worldContainer.transform.rotation * refPointLocalRotation;
        }
    }

    private void SetupContainers()
    {
        worldContainer = new GameObject("WorldContainer"); worldContainer.transform.SetParent(this.transform, false);
        modelContainer = new GameObject("ModelContainer"); modelContainer.transform.SetParent(worldContainer.transform, false);
        axesContainer = new GameObject("AxesContainer"); axesContainer.transform.SetParent(worldContainer.transform, false);
    }

    private void SetupHull(GameObject hull, GameObject original) { hull.transform.SetParent(original.transform.parent, false); hull.AddComponent<MeshCollider>().convex = true; }

    private void SetupReferencePoint(GameObject rootModel)
    {
        modelReferencePoint = rootModel.transform.Find("ref");
        if (modelReferencePoint == null) modelReferencePoint = rootModel.transform;
        refPointLocalPosition = worldContainer.transform.InverseTransformPoint(modelReferencePoint.position);
        refPointLocalRotation = Quaternion.Inverse(worldContainer.transform.rotation) * modelReferencePoint.rotation;
    }

    private void SetupVisualHelpers()
    {
        if (planeVisualizerPrefab != null) { activePlaneVisualizer = Instantiate(planeVisualizerPrefab, worldContainer.transform, false); activePlaneVisualizer.SetActive(false); }
        if (lineRendererPrefab != null) { activeLineRenderer = Instantiate(lineRendererPrefab, worldContainer.transform, false).GetComponent<LineRenderer>(); activeLineRenderer.enabled = false; }
        if (axesContainer != null) { axesContainer.SetActive(true); Material matX = new Material(Shader.Find("Unlit/Color")) { color = Color.red }; Material matY = new Material(Shader.Find("Unlit/Color")) { color = Color.green }; Material matZ = new Material(Shader.Find("Unlit/Color")) { color = Color.blue }; currentModelAxisVisuals = AxisGenerator.CreateAxes(axesContainer.transform, Constants.AXIS_LENGTH, Constants.AXIS_THICKNESS, serverAxisOriginOffset, matX, matY, matZ); }
    }

    private IEnumerator ShakeCoroutine(GameObject targetObject)
    {
        Transform targetTransform = targetObject.transform; Quaternion startRotation = targetTransform.localRotation;
        while (true) { float angle = Mathf.Sin(Time.time * Constants.WIGGLE_SPEED) * Constants.WIGGLE_ANGLE; targetTransform.localRotation = startRotation * Quaternion.AngleAxis(angle, wiggleAxis); yield return null; }
    }

    private void ShowLocalServerIcon(Vector3 worldPos, Sprite icon)
    {
        if (feedbackIconImage == null || serverCamera == null || uiCanvasRectTransform == null) return;

        Vector3 screenPoint = serverCamera.WorldToScreenPoint(worldPos);
        if (screenPoint.z < 0) return;

        feedbackIconImage.sprite = icon;
        InteractionUtility.PositionIcon(feedbackIconImage, (Vector2)screenPoint, uiCanvasRectTransform, null);
    }

    private string SpriteToBase64(Sprite sprite)
    {
        if (sprite == null) return string.Empty;
        try { Texture2D texture = sprite.texture; RenderTexture tmp = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Linear); Graphics.Blit(texture, tmp); RenderTexture previous = RenderTexture.active; RenderTexture.active = tmp; Texture2D readableTexture = new Texture2D(texture.width, texture.height); readableTexture.ReadPixels(new Rect(0, 0, tmp.width, tmp.height), 0, 0); readableTexture.Apply(); RenderTexture.active = previous; RenderTexture.ReleaseTemporary(tmp); byte[] bytes = readableTexture.EncodeToPNG(); string base64 = Convert.ToBase64String(bytes); Destroy(readableTexture); return base64; }
        catch { return string.Empty; }
    }

    public void ApplyWorldTransform(Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
    {
        if (worldContainer != null) { worldContainer.transform.localPosition = localPosition; worldContainer.transform.localRotation = localRotation; worldContainer.transform.localScale = localScale; }
    }

    public void ExecuteDestroy(DestroyActionData data)
    {
        StopShaking(data.targetPartID, true);
        while (redoStack.Count > 0) CleanUpAction(redoStack.Pop());
        if (allParts.TryGetValue(data.targetPartID, out GameObject partToDestroy))
        {
            partToDestroy.SetActive(false);
            undoStack.Push(new ActionRecord { Type = ActionType.Destroy, ActionID = data.actionID, DestroyedPart = partToDestroy });
        }
    }

    public void ExecuteSlice(SliceActionData data)
    {
        while (redoStack.Count > 0) CleanUpAction(redoStack.Pop());
        var record = new ActionRecord { Type = ActionType.Slice, ActionID = data.actionID, Originals = new List<GameObject>(), NewHulls = new List<GameObject>() };

        foreach (string partID in data.targetPartIDs)
        {
            if (allParts.TryGetValue(partID, out GameObject originalPart) && originalPart.activeInHierarchy)
            {
                if (originalPart.GetComponentInChildren<VolumeRenderedObject>() != null) { ExecuteVolumetricSlice(originalPart, data, record); }
                else { ExecuteMeshSlice(originalPart, data, record); }
            }
        }
        if (record.Originals.Count > 0) undoStack.Push(record);
    }

    public ModelMetadataList GetAllModelsMetadata()
    {
        var metadataList = new List<ModelMetadata>();
        foreach (var modelData in availableModels) metadataList.Add(new ModelMetadata { modelID = modelData.modelID, displayName = modelData.displayName, description = modelData.description, thumbnailBase64 = SpriteToBase64(modelData.thumbnail) });
        return new ModelMetadataList { models = metadataList.ToArray() };
    }
    public void HideCutLine() { if (activeLineRenderer != null) activeLineRenderer.enabled = false; }
    public void HideSliceIcon() { if (feedbackIconImage != null && feedbackIconImage.sprite == sliceIconSprite) feedbackIconImage.gameObject.SetActive(false); }

    public void LoadNewModel(string modelId)
    {
        UnloadCurrentModel();
        if (!modelDataLookup.TryGetValue(modelId, out ModelData modelData)) return;
        SetupContainers();
        CurrentModelID = modelId;
        GameObject root = ModelLoader.Load(modelData, modelContainer.transform, volumetricSliceMaterial);
        if (root != null) { rootModel = root; rootModel.name = "RootModel"; allParts.Add(rootModel.name, rootModel); CurrentModelBoundsSize = modelData.boundsSize; SetupReferencePoint(rootModel); }
        SetupVisualHelpers();
    }

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

    public void StartShaking(string partID, Vector3 receivedWorldPosition)
    {
        if (allParts.TryGetValue(partID, out GameObject serverPartToShake))
        {
            if (!shakingCoroutines.ContainsKey(serverPartToShake))
            {
                Quaternion originalRot = serverPartToShake.transform.localRotation;
                originalLocalRotations[serverPartToShake] = originalRot;

                shakingCoroutines[serverPartToShake] = StartCoroutine(
                    InteractionUtility.ShakeCoroutine(serverPartToShake.transform, originalRot, wiggleAxis)
                );

                ShowLocalServerIcon(receivedWorldPosition, destroyIconSprite);
            }
        }
    }

    public void StopShaking(string partID, bool resetPosition = true)
    {
        if (feedbackIconImage != null && feedbackIconImage.sprite == destroyIconSprite) feedbackIconImage.gameObject.SetActive(false);
        if (allParts.TryGetValue(partID, out GameObject partToStop)) { if (shakingCoroutines.TryGetValue(partToStop, out Coroutine shakeCoroutine)) { StopCoroutine(shakeCoroutine); shakingCoroutines.Remove(partToStop); if (resetPosition && originalLocalRotations.TryGetValue(partToStop, out Quaternion originalRot)) { partToStop.transform.localRotation = originalRot; originalLocalRotations.Remove(partToStop); } } }
    }

    public void UndoLastAction()
    {
        if (undoStack.Count == 0) return;
        ActionRecord record = undoStack.Pop();
        if (record.Type == ActionType.Slice) { foreach (var hull in record.NewHulls) if (hull != null) hull.SetActive(false); foreach (var part in record.Originals) if (part != null) part.SetActive(true); }
        else if (record.Type == ActionType.Destroy && record.DestroyedPart != null) record.DestroyedPart.SetActive(true);
        redoStack.Push(record);
    }

    public void UnloadCurrentModel()
    {
        if (worldContainer != null) Destroy(worldContainer);
        foreach (var coroutine in shakingCoroutines.Values) { if (coroutine != null) StopCoroutine(coroutine); }
        if (feedbackIconImage != null) feedbackIconImage.gameObject.SetActive(false);
        shakingCoroutines.Clear(); originalLocalRotations.Clear(); allParts.Clear(); undoStack.Clear(); redoStack.Clear();
        ClearCurrentModelAxisVisuals(); modelReferencePoint = null; rootModel = null; worldContainer = null; modelContainer = null; axesContainer = null; CurrentModelID = null;
    }

    public void UpdateCutLine(Vector3 start, Vector3 end) { if (activeLineRenderer == null) return; activeLineRenderer.enabled = true; activeLineRenderer.positionCount = 2; activeLineRenderer.SetPosition(0, start); activeLineRenderer.SetPosition(1, end); }

    public void UpdateVisualCropPlane(Vector3 position, Vector3 normal, float scale)
    {
        if (activePlaneVisualizer == null) return;
        activePlaneVisualizer.transform.SetPositionAndRotation(position, Quaternion.LookRotation(normal));
        activePlaneVisualizer.transform.localScale = Vector3.one * scale;
        activePlaneVisualizer.SetActive(showPlaneVisualizer);
    }

    public Vector3 CurrentModelBoundsSize { get; private set; } = Vector3.one;

    public string CurrentModelID { get; private set; } = null;

    private enum ActionType { Slice, Destroy }
    private class ActionRecord
    {
        public string ActionID;
        public GameObject DestroyedPart;
        public List<GameObject> NewHulls;
        public List<GameObject> Originals;
        public ActionType Type;
    }
}