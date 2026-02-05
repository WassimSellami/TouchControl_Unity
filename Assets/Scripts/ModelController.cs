using UnityEngine;
using System.Collections.Generic;
using EzySlice;
using System.Collections;
using UnityEngine.UI;
using System;

public class ModelController : MonoBehaviour, IModelViewer
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

    [Header("Volumetric Settings")]
    [SerializeField] private Material volumetricSliceMaterial;

    [Header("Shaking Effect")]
    [SerializeField] private Vector3 wiggleAxis = Vector3.up;

    [Header("UI Feedback")]
    [SerializeField] private Camera serverCamera;
    [SerializeField] private RectTransform uiCanvasRectTransform;
    [SerializeField] private Image feedbackIconImage;
    [SerializeField] private Sprite destroyIconSprite;
    [SerializeField] private Sprite sliceIconSprite;
    [SerializeField] private float iconVerticalOffsetPercent = 0.08f;

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
            feedbackIconImage.rectTransform.pivot = new Vector2(0.5f, 0.5f);
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

    public void SetModelVisibility(bool isVisible)
    {
        if (rootModel != null)
            rootModel.SetActive(isVisible);
    }

    public void UnloadCurrentModel()
    {
        if (worldContainer != null) Destroy(worldContainer);
        foreach (var coroutine in shakingCoroutines.Values) { if (coroutine != null) StopCoroutine(coroutine); }
        if (feedbackIconImage != null) feedbackIconImage.gameObject.SetActive(false);

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
        refPointLocalPosition = Vector3.zero;
        refPointLocalRotation = Quaternion.identity;
    }

    public void LoadNewModel(string modelId)
    {
        UnloadCurrentModel();
        if (!modelDataLookup.TryGetValue(modelId, out ModelData modelData)) return;

        SetupContainers();
        CurrentModelID = modelId;
        GameObject root = ModelLoader.Load(modelData, modelContainer.transform, volumetricSliceMaterial);

        if (root != null)
        {
            rootModel = root;
            rootModel.name = "RootModel";
            allParts.Add(rootModel.name, rootModel);
            CurrentModelBoundsSize = modelData.boundsSize;
            SetupReferencePoint(rootModel);
        }
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

    private void SetupReferencePoint(GameObject rootModel)
    {
        modelReferencePoint = rootModel.transform.Find("ref");
        if (modelReferencePoint == null) modelReferencePoint = rootModel.transform;
        refPointLocalPosition = worldContainer.transform.InverseTransformPoint(modelReferencePoint.position);
        refPointLocalRotation = Quaternion.Inverse(worldContainer.transform.rotation) * modelReferencePoint.rotation;
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
        if (axesContainer != null)
        {
            axesContainer.SetActive(true);
            Material matX = new Material(Shader.Find("Unlit/Color")) { color = Color.red };
            Material matY = new Material(Shader.Find("Unlit/Color")) { color = Color.green };
            Material matZ = new Material(Shader.Find("Unlit/Color")) { color = Color.blue };
            currentModelAxisVisuals = AxisGenerator.CreateAxes(axesContainer.transform, Constants.AXIS_LENGTH, Constants.AXIS_THICKNESS, serverAxisOriginOffset, matX, matY, matZ);
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
                SlicedHull sliceResult = originalPart.Slice(data.planePoint, data.planeNormal, crossSectionMaterial);
                if (sliceResult != null)
                {
                    GameObject upperHull = sliceResult.CreateUpperHull(originalPart, crossSectionMaterial);
                    GameObject lowerHull = sliceResult.CreateLowerHull(originalPart, crossSectionMaterial);
                    if (upperHull != null && lowerHull != null)
                    {
                        upperHull.name = partID + "_U"; lowerHull.name = partID + "_L";
                        allParts.Add(upperHull.name, upperHull); allParts.Add(lowerHull.name, lowerHull);
                        SetupHull(upperHull, originalPart); SetupHull(lowerHull, originalPart);
                        StartCoroutine(AnimateSeparation(upperHull, lowerHull, originalPart, data.planeNormal, data.separationFactor));
                        record.Originals.Add(originalPart); record.NewHulls.Add(upperHull); record.NewHulls.Add(lowerHull);
                        originalPart.SetActive(false);
                    }
                }
            }
        }
        if (record.Originals.Count > 0) undoStack.Push(record);
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

    public void UndoLastAction()
    {
        if (undoStack.Count == 0) return;
        ActionRecord record = undoStack.Pop();
        if (record.Type == ActionType.Slice)
        {
            foreach (var hull in record.NewHulls) if (hull != null) hull.SetActive(false);
            foreach (var part in record.Originals) if (part != null) part.SetActive(true);
        }
        else if (record.Type == ActionType.Destroy && record.DestroyedPart != null) record.DestroyedPart.SetActive(true);
        redoStack.Push(record);
    }

    public void RedoLastAction()
    {
        if (redoStack.Count == 0) return;
        ActionRecord record = redoStack.Pop();
        if (record.Type == ActionType.Slice)
        {
            foreach (var hull in record.NewHulls) if (hull != null) hull.SetActive(true);
            foreach (var part in record.Originals) if (part != null) part.SetActive(false);
        }
        else if (record.Type == ActionType.Destroy && record.DestroyedPart != null) record.DestroyedPart.SetActive(false);
        undoStack.Push(record);
    }

    private void CleanUpAction(ActionRecord record)
    {
        if (record.Type == ActionType.Slice) foreach (var hull in record.NewHulls) { if (hull != null) { allParts.Remove(hull.name); Destroy(hull); } }
    }

    public void UpdateVisualCropPlane(Vector3 position, Vector3 normal, float scale)
    {
        if (activePlaneVisualizer == null) return;
        activePlaneVisualizer.transform.SetPositionAndRotation(position, Quaternion.LookRotation(normal));
        activePlaneVisualizer.transform.localScale = Vector3.one * scale;
        activePlaneVisualizer.SetActive(showPlaneVisualizer);
    }

    public void ResetCrop()
    {
        while (undoStack.Count > 0) CleanUpAction(undoStack.Pop());
        while (redoStack.Count > 0) CleanUpAction(redoStack.Pop());
        if (modelContainer == null) return;
        for (int i = modelContainer.transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = modelContainer.transform.GetChild(i).gameObject;
            if (rootModel != null && child == rootModel) continue;
            Destroy(child);
        }
        if (rootModel != null) { rootModel.SetActive(true); allParts.Clear(); allParts.Add(rootModel.name, rootModel); }
        if (activePlaneVisualizer != null) activePlaneVisualizer.SetActive(false);
    }

    public void UpdateCutLine(Vector3 start, Vector3 end)
    {
        if (activeLineRenderer == null) return;
        activeLineRenderer.enabled = true; activeLineRenderer.positionCount = 2;
        activeLineRenderer.SetPosition(0, start); activeLineRenderer.SetPosition(1, end);
    }

    public void HideCutLine() { if (activeLineRenderer != null) activeLineRenderer.enabled = false; }

    public void ShowSliceIcon(Vector3 worldPosition)
    {
        ShowLocalServerIcon(worldPosition, sliceIconSprite);
    }

    public void HideSliceIcon()
    {
        if (feedbackIconImage != null && feedbackIconImage.sprite == sliceIconSprite) feedbackIconImage.gameObject.SetActive(false);
    }

    public void StartShaking(string partID, Vector3 receivedWorldPosition)
    {
        if (allParts.TryGetValue(partID, out GameObject serverPartToShake))
        {
            if (!shakingCoroutines.ContainsKey(serverPartToShake))
            {
                originalLocalRotations[serverPartToShake] = serverPartToShake.transform.localRotation;
                shakingCoroutines[serverPartToShake] = StartCoroutine(ShakeCoroutine(serverPartToShake));
                ShowLocalServerIcon(receivedWorldPosition, destroyIconSprite);
            }
        }
    }

    public void StopShaking(string partID, bool resetPosition = true)
    {
        if (feedbackIconImage != null && feedbackIconImage.sprite == destroyIconSprite) feedbackIconImage.gameObject.SetActive(false);
        if (allParts.TryGetValue(partID, out GameObject partToStop))
        {
            if (shakingCoroutines.TryGetValue(partToStop, out Coroutine shakeCoroutine))
            {
                StopCoroutine(shakeCoroutine); shakingCoroutines.Remove(partToStop);
                if (resetPosition && originalLocalRotations.TryGetValue(partToStop, out Quaternion originalRot))
                {
                    partToStop.transform.localRotation = originalRot; originalLocalRotations.Remove(partToStop);
                }
            }
        }
    }

    private void ShowLocalServerIcon(Vector3 worldPos, Sprite icon)
    {
        if (feedbackIconImage == null || serverCamera == null || uiCanvasRectTransform == null) return;

        Vector3 screenPoint = serverCamera.WorldToScreenPoint(worldPos);
        if (screenPoint.z < 0) return;

        float offsetPx = Screen.height * iconVerticalOffsetPercent;
        Vector2 adjustedScreenPoint = new Vector2(screenPoint.x, screenPoint.y + offsetPx);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(uiCanvasRectTransform, adjustedScreenPoint, null, out Vector2 localPoint);

        feedbackIconImage.sprite = icon;
        feedbackIconImage.rectTransform.anchoredPosition = localPoint;
        feedbackIconImage.gameObject.SetActive(true);
    }

    private IEnumerator ShakeCoroutine(GameObject targetObject)
    {
        Transform targetTransform = targetObject.transform;
        Quaternion startRotation = targetTransform.localRotation;
        while (true)
        {
            float angle = Mathf.Sin(Time.time * Constants.WIGGLE_SPEED) * Constants.WIGGLE_ANGLE;
            targetTransform.localRotation = startRotation * Quaternion.AngleAxis(angle, wiggleAxis);
            yield return null;
        }
    }

    private void SetupHull(GameObject hull, GameObject original) { hull.transform.SetParent(original.transform.parent, false); hull.AddComponent<MeshCollider>().convex = true; }

    private IEnumerator AnimateSeparation(GameObject upperHull, GameObject lowerHull, GameObject original, Vector3 planeNormal, float separationFactor)
    {
        float duration = 0.3f; Bounds originalBounds = GetBounds(original);
        float separationDistance = originalBounds.size.magnitude * separationFactor;
        Vector3 separationVector = planeNormal * (separationDistance * 0.5f);
        Vector3 upperStart = upperHull.transform.position, lowerStart = lowerHull.transform.position;
        Vector3 upperEnd = upperStart + separationVector, lowerEnd = lowerStart - separationVector;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (upperHull == null || lowerHull == null) yield break;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            upperHull.transform.position = Vector3.Lerp(upperStart, upperEnd, t);
            lowerHull.transform.position = Vector3.Lerp(lowerStart, lowerEnd, t);
            elapsed += Time.deltaTime; yield return null;
        }
        if (upperHull != null) upperHull.transform.position = upperEnd;
        if (lowerHull != null) lowerHull.transform.position = lowerEnd;
    }

    private Bounds GetBounds(GameObject go)
    {
        Renderer[] rends = go.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) return new Bounds(go.transform.position, Vector3.one);
        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        return b;
    }

    void ClearCurrentModelAxisVisuals() { foreach (GameObject vis in currentModelAxisVisuals) if (vis != null) Destroy(vis); currentModelAxisVisuals.Clear(); }

    public ModelMetadataList GetAllModelsMetadata()
    {
        var metadataList = new List<ModelMetadata>();
        foreach (var modelData in availableModels)
        {
            metadataList.Add(new ModelMetadata { modelID = modelData.modelID, displayName = modelData.displayName, description = modelData.description, thumbnailBase64 = SpriteToBase64(modelData.thumbnail) });
        }
        return new ModelMetadataList { models = metadataList.ToArray() };
    }

    private string SpriteToBase64(Sprite sprite)
    {
        if (sprite == null) return "";
        try
        {
            Texture2D texture = sprite.texture;
            RenderTexture tmp = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Linear);
            Graphics.Blit(texture, tmp);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = tmp;
            Texture2D readableTexture = new Texture2D(texture.width, texture.height);
            readableTexture.ReadPixels(new Rect(0, 0, tmp.width, tmp.height), 0, 0);
            readableTexture.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(tmp);
            byte[] bytes = readableTexture.EncodeToPNG();
            string base64 = Convert.ToBase64String(bytes);
            Destroy(readableTexture);
            return base64;
        }
        catch { return ""; }
    }

    public void ResetState()
    {
        UnloadCurrentModel();
        transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        transform.localScale = Vector3.one;
    }
}