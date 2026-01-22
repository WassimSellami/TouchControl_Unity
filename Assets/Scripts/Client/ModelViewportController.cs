using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityVolumeRendering;
using System.IO;
using UnityEngine.Networking;

public class ModelViewportController : MonoBehaviour, IModelViewportController
{
    [Header("References")]
    [SerializeField] private Camera referenceCamera;
    [SerializeField] private List<ModelData> availableModels = new List<ModelData>();
    [SerializeField] private Material volumetricDefaultMaterial;

    [Header("Axis Visuals")]
    [SerializeField] private Vector3 axisOriginOffset = new Vector3(0f, 0f, 0f);
    [SerializeField] private Material redAxisMaterial;
    [SerializeField] private Material greenAxisMaterial;
    [SerializeField] private Material blueAxisMaterial;

    [Header("Auto Rotation")]
    [SerializeField] private Vector3 wiggleAxis = Vector3.up;

    // Internal state
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private Vector3 initialScale;

    private Vector3 initialCameraPosition;
    private Quaternion initialCameraRotation;

    private List<GameObject> axisVisuals = new List<GameObject>();
    private bool axesCreated = false;
    private GameObject axesContainer;
    private Transform modelReferencePoint;

    private enum OrbitAxis { None, Horizontal, Vertical }
    private OrbitAxis lockedOrbitAxis = OrbitAxis.None;

    private bool isAnimatingPresetView = false;
    private bool isAutoRotating = false;
    private float autoRotationDirection = 0f;

    private GameObject worldContainer;
    private GameObject modelContainer;
    private GameObject rootModel;
    private Vector3 refPointLocalPosition = Vector3.zero;
    private Quaternion refPointLocalRotation = Quaternion.identity;
    private Dictionary<string, ModelData> modelDataLookup = new Dictionary<string, ModelData>();

    public bool IsAutoRotating => isAutoRotating;
    public string CurrentModelID { get; private set; }

    private AnimationCurve PRESET_VIEW_EASE_CURVE = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    void Awake()
    {
        initialPosition = transform.position;
        initialRotation = transform.rotation;
        initialScale = transform.localScale;

        if (referenceCamera == null)
            Debug.LogError("ModelViewportController: Reference Camera not assigned!");
        else
        {
            initialCameraPosition = referenceCamera.transform.position;
            initialCameraRotation = referenceCamera.transform.rotation;
        }

        foreach (var modelData in availableModels)
        {
            if (!string.IsNullOrEmpty(modelData.modelID))
                modelDataLookup[modelData.modelID] = modelData;
        }

        if (redAxisMaterial == null)
            redAxisMaterial = new Material(Shader.Find("Standard")) { color = Color.red };
        if (greenAxisMaterial == null)
            greenAxisMaterial = new Material(Shader.Find("Standard")) { color = Color.green };
        if (blueAxisMaterial == null)
            blueAxisMaterial = new Material(Shader.Find("Standard")) { color = Color.blue };
    }

    void Update()
    {
        if (isAutoRotating)
        {
            float rotationAmount = Constants.AUTO_ROTATION_SPEED * autoRotationDirection * Time.deltaTime;
            transform.Rotate(Vector3.up, rotationAmount, Space.World);
        }
    }

    void LateUpdate()
    {
        if (axesContainer != null && modelReferencePoint != null)
        {
            axesContainer.transform.position = modelReferencePoint.position;
            axesContainer.transform.rotation = modelReferencePoint.rotation;
        }
    }

    public void StartContinuousRotation(float direction)
    {
        isAutoRotating = true;
        autoRotationDirection = direction;
    }

    public void StopContinuousRotation()
    {
        isAutoRotating = false;
        autoRotationDirection = 0f;
    }

    public void ProcessOrbit(Vector2 screenDelta)
    {
        StopContinuousRotation();

        if (referenceCamera == null || isAnimatingPresetView)
            return;

        if (lockedOrbitAxis == OrbitAxis.None && screenDelta.sqrMagnitude > 0.01f)
        {
            if (Mathf.Abs(screenDelta.x) > Mathf.Abs(screenDelta.y))
                lockedOrbitAxis = OrbitAxis.Horizontal;
            else
                lockedOrbitAxis = OrbitAxis.Vertical;
        }

        switch (lockedOrbitAxis)
        {
            case OrbitAxis.Horizontal:
                transform.Rotate(Vector3.up, -screenDelta.x * Constants.ORBIT_SENSITIVITY, Space.World);
                break;
            case OrbitAxis.Vertical:
                transform.Rotate(referenceCamera.transform.right, screenDelta.y * Constants.ORBIT_SENSITIVITY, Space.World);
                break;
        }
    }

    public void ResetOrbitLock()
    {
        lockedOrbitAxis = OrbitAxis.None;
    }

    public void ProcessPan(Vector2 screenDelta)
    {
        StopContinuousRotation();

        if (referenceCamera == null || isAnimatingPresetView)
            return;

        Vector3 right = referenceCamera.transform.right * -screenDelta.x;
        Vector3 up = referenceCamera.transform.up * -screenDelta.y;
        Vector3 worldDelta = (right + up) * Constants.PAN_SENSITIVITY;

        transform.Translate(worldDelta, Space.World);
    }

    public void ProcessZoom(float zoomAmount)
    {
        StopContinuousRotation();

        if (isAnimatingPresetView)
            zoomAmount = 0;

        float scaleChange = 1.0f + zoomAmount * Constants.ZOOM_SENSITIVITY;
        Vector3 newScale = transform.localScale * scaleChange;

        newScale.x = Mathf.Clamp(newScale.x, Constants.SCALE_MIN, Constants.SCALE_MAX);
        newScale.y = Mathf.Clamp(newScale.y, Constants.SCALE_MIN, Constants.SCALE_MAX);
        newScale.z = Mathf.Clamp(newScale.z, Constants.SCALE_MIN, Constants.SCALE_MAX);

        transform.localScale = newScale;
    }

    public void ProcessRoll(float angleDelta)
    {
        StopContinuousRotation();

        if (referenceCamera == null || isAnimatingPresetView)
            return;

        Vector3 rotationAxis = referenceCamera.transform.forward;
        transform.Rotate(rotationAxis, angleDelta * Constants.ROLL_SENSITIVITY, Space.World);
    }

    public void ResetState()
    {
        StopContinuousRotation();

        if (isAnimatingPresetView)
            StopAllCoroutines();

        isAnimatingPresetView = false;
        transform.position = initialPosition;
        transform.rotation = initialRotation;
        transform.localScale = initialScale;


        if (referenceCamera != null)
        {
            referenceCamera.transform.position = initialCameraPosition;
            referenceCamera.transform.rotation = initialCameraRotation;
        }

        EnsureAxisVisualsAreCreated();
    }

    public void SetModelVisibility(bool isVisible)
    {
        if (rootModel != null)
            rootModel.SetActive(isVisible);
    }

    public void TriggerPresetViewRotation(float direction)
    {
        StopContinuousRotation();
        if (isAnimatingPresetView)
            return;

        StartCoroutine(AnimatePresetViewRotation(direction));
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
        if (!modelDataLookup.TryGetValue(modelId, out ModelData modelData))
        {
            Debug.LogError($"Model ID {modelId} not found!");
            return;
        }

        if (worldContainer != null)
        {
            worldContainer.name = "WorldContainer_Destroying";
            Destroy(worldContainer);
        }

        axesCreated = false;
        axisVisuals.Clear();

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

        if (rootModel != null)
        {
            rootModel.name = "RootModel";
            this.rootModel = rootModel;
            SetupReferencePoint(rootModel);
            EnsureAxisVisualsAreCreated();
        }
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
        string filePath = data.rawFilePath;

        // ANDROID FIX: Extract file from APK to readable storage
        if (Application.platform == RuntimePlatform.Android)
        {
            string fileName = System.IO.Path.GetFileName(data.rawFilePath);
            string persistentPath = System.IO.Path.Combine(Application.persistentDataPath, fileName);

            if (!System.IO.File.Exists(persistentPath))
            {
                // FIX: Use manual string concatenation to enforce forward slashes.
                // Path.Combine can add backslashes on Windows, which breaks Android URLs.
                string sourcePath = Application.streamingAssetsPath + "/" + fileName;

                Debug.Log($"Attempting to download from: {sourcePath}"); // Debug log

                UnityWebRequest request = UnityWebRequest.Get(sourcePath);
                var operation = request.SendWebRequest();

                while (!operation.isDone) { } // Block until done

                if (request.result == UnityWebRequest.Result.Success)
                {
                    System.IO.File.WriteAllBytes(persistentPath, request.downloadHandler.data);
                }
                else
                {
                    Debug.LogError($"Failed to copy volumetric file from {sourcePath}: {request.error}");
                    return null;
                }
            }
            filePath = persistentPath;
        }

        // Normal Loading Logic
        var importer = new RawDatasetImporter(
            filePath,
            data.dimX, data.dimY, data.dimZ,
            data.contentFormat,
            data.endianness,
            data.bytesToSkip
        );

        VolumeDataset dataset = importer.Import();
        if (dataset == null)
        {
            Debug.LogError("Failed to import volumetric dataset.");
            return null;
        }

        VolumeRenderedObject volObj = VolumeObjectFactory.CreateObject(dataset);
        volObj.gameObject.transform.SetParent(modelContainer.transform, false);

        if (volumetricDefaultMaterial != null)
        {
            Renderer r = volObj.GetComponent<Renderer>();
            if (r != null)
                r.sharedMaterial = volumetricDefaultMaterial;
        }

        return volObj.gameObject;
    }

    private GameObject LoadPrefabModel(PolygonalModelData data)
    {
        if (data.prefab == null)
            return null;

        return Instantiate(data.prefab, modelContainer.transform);
    }

    private void SetupReferencePoint(GameObject rootModelObj)
    {
        modelReferencePoint = rootModelObj.transform.Find("ref");

        if (modelReferencePoint == null)
            modelReferencePoint = rootModelObj.transform;

        bool isVolumetricData = rootModelObj.GetComponent<VolumeRenderedObject>() != null;

        if (isVolumetricData)
        {
            refPointLocalPosition = new Vector3(-0.6f, -0.4f, -0.5f);
        }
        else
        {
            refPointLocalPosition = worldContainer.transform.InverseTransformPoint(modelReferencePoint.position);
        }

        refPointLocalRotation = Quaternion.Inverse(worldContainer.transform.rotation) * modelReferencePoint.rotation;
    }

    public void EnsureAxisVisualsAreCreated()
    {
        if (!this.gameObject.activeInHierarchy || modelReferencePoint == null)
            return;

        if (!axesCreated)
        {
            CreateAxisVisuals();
            axesCreated = true;
            axesContainer.SetActive(true);
        }
    }

    private void CreateAxisVisuals()
    {
        foreach (Transform child in axesContainer.transform)
            Destroy(child.gameObject);

        axisVisuals.Clear();

        CreateSingleAxisVisual(
            axesContainer.transform,
            Vector3.right,
            Constants.AXIS_LENGTH,
            Constants.AXIS_THICKNESS,
            redAxisMaterial,
            "XAxis_Client"
        );

        CreateSingleAxisVisual(
            axesContainer.transform,
            Vector3.up,
            Constants.AXIS_LENGTH,
            Constants.AXIS_THICKNESS,
            greenAxisMaterial,
            "YAxis_Client"
        );

        CreateSingleAxisVisual(
            axesContainer.transform,
            Vector3.forward,
            Constants.AXIS_LENGTH,
            Constants.AXIS_THICKNESS,
            blueAxisMaterial,
            "ZAxis_Client"
        );
    }

    private void CreateSingleAxisVisual(
        Transform parentForAxes,
        Vector3 direction,
        float length,
        float thickness,
        Material axisMat,
        string baseName
    )
    {
        float capHeight = thickness * Constants.ARROWHEAD_HEIGHT_FACTOR;
        float shaftActualLength = Mathf.Max(thickness * 2f, length - capHeight);

        // Create shaft
        GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shaft.name = baseName + "_Shaft";
        shaft.transform.SetParent(parentForAxes, false);
        Destroy(shaft.GetComponent<CapsuleCollider>());

        shaft.transform.localScale = new Vector3(thickness, shaftActualLength / 2f, thickness);
        shaft.transform.localPosition = axisOriginOffset + direction * (shaftActualLength / 2f);
        shaft.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction);

        if (shaft.GetComponent<Renderer>() != null && axisMat != null)
            shaft.GetComponent<Renderer>().material = axisMat;

        axisVisuals.Add(shaft);

        // Create arrowhead
        GameObject arrowheadCap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        arrowheadCap.name = baseName + "_HeadCap";
        arrowheadCap.transform.SetParent(parentForAxes, false);
        Destroy(arrowheadCap.GetComponent<CapsuleCollider>());

        float capRadius = thickness * Constants.ARROWHEAD_RADIUS_FACTOR;
        arrowheadCap.transform.localScale = new Vector3(capRadius, capHeight / 2f, capRadius);
        arrowheadCap.transform.localPosition = axisOriginOffset + direction * (shaftActualLength + capHeight / 2f);
        arrowheadCap.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction);

        if (arrowheadCap.GetComponent<Renderer>() != null && axisMat != null)
            arrowheadCap.GetComponent<Renderer>().material = axisMat;

        axisVisuals.Add(arrowheadCap);
    }

    private IEnumerator AnimatePresetViewRotation(float direction)
    {
        isAnimatingPresetView = true;
        Quaternion startRotation = transform.rotation;
        Quaternion targetRotation = startRotation * Quaternion.AngleAxis(Constants.PRESET_VIEW_ROTATION_STEP * direction, Vector3.up);

        float elapsedTime = 0f;

        while (elapsedTime < Constants.PRESET_VIEW_ANIMATION_DURATION)
        {
            elapsedTime += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsedTime / Constants.PRESET_VIEW_ANIMATION_DURATION);
            float easedProgress = PRESET_VIEW_EASE_CURVE.Evaluate(progress);

            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, easedProgress);
            yield return null;
        }

        transform.rotation = targetRotation;
        isAnimatingPresetView = false;
    }
}
