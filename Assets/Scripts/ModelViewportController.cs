using UnityEngine;
using System.Collections;
using System.Collections.Generic;
public class ModelViewportController : MonoBehaviour, IModelManipulator
{
    [Header("References")]
    [SerializeField] private Camera referenceCamera;
    [Header("Appearance")]
    [SerializeField] private Material placeholderMaterial;

    [Header("Axis Visuals")]
    [SerializeField] private Vector3 axisOriginOffset = new Vector3(0f, 0f, 0f);
    [SerializeField] private Material redAxisMaterial;
    [SerializeField] private Material greenAxisMaterial;
    [SerializeField] private Material blueAxisMaterial;

    private bool axesVisible = true;

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

    public bool IsAutoRotating => isAutoRotating;
    public string CurrentModelID { get; private set; }

    private AnimationCurve PRESET_VIEW_EASE_CURVE = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    void Awake()
    {
        initialPosition = transform.position;
        initialRotation = transform.rotation;
        initialScale = transform.localScale;

        if (referenceCamera != null)
        {
            initialCameraPosition = referenceCamera.transform.position;
            initialCameraRotation = referenceCamera.transform.rotation;
        }

        if (redAxisMaterial == null) redAxisMaterial = new Material(Shader.Find("Standard")) { color = Color.red };
        if (greenAxisMaterial == null) greenAxisMaterial = new Material(Shader.Find("Standard")) { color = Color.green };
        if (blueAxisMaterial == null) blueAxisMaterial = new Material(Shader.Find("Standard")) { color = Color.blue };
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
            axesContainer.transform.rotation = modelReferencePoint.rotation;
        }
    }

    public void SetAxesVisibility(bool visible)
    {
        axesVisible = visible;
        if (axesContainer != null)
        {
            axesContainer.SetActive(axesVisible);
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
        {
            rootModel.SetActive(isVisible);
        }
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
        if (worldContainer != null)
        {
            worldContainer.name = "WorldContainer_Destroying";
            Destroy(worldContainer);
        }

        axesCreated = false;
        axisVisuals.Clear();

        SetupContainers();
        CurrentModelID = modelId;

        rootModel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rootModel.name = "RootModel";
        rootModel.transform.SetParent(modelContainer.transform, false);

        // Initial defaults (will be updated by UpdatePlaceholderSize)
        rootModel.transform.localPosition = new Vector3(0.5f, 0.5f, 0.5f);
        rootModel.transform.localRotation = Quaternion.identity;
        rootModel.transform.localScale = Vector3.one;

        // Apply material to fix pink color
        if (placeholderMaterial != null)
        {
            Renderer r = rootModel.GetComponent<Renderer>();
            if (r != null) r.material = placeholderMaterial;
        }

        SetupReferencePoint(rootModel);
        EnsureAxisVisualsAreCreated();
    }

    public void UpdatePlaceholderSize(Vector3 newSize)
    {
        if (rootModel != null)
        {
            // 1. Scale the cube
            rootModel.transform.localScale = newSize;

            // 2. Center the cube (Unity Cube pivot is center, so local 0 is center)
            rootModel.transform.localPosition = Vector3.zero;

            // 3. Move the Axes to the MIN corner
            // The min corner of a centered cube is -(size/2)
            if (axesContainer != null)
            {
                axesContainer.transform.localPosition = -newSize * 0.5f;
            }
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

    private void SetupReferencePoint(GameObject rootModelObj)
    {
        // Reference point is the object itself, effectively 0,0,0 of the parent
        modelReferencePoint = worldContainer.transform;
    }

    public void EnsureAxisVisualsAreCreated()
    {
        if (!this.gameObject.activeInHierarchy || modelReferencePoint == null)
            return;

        if (!axesCreated)
        {
            CreateAxisVisuals();
            axesCreated = true;
        }
        axesContainer.SetActive(axesVisible);
    }

    private void CreateAxisVisuals()
    {
        foreach (Transform child in axesContainer.transform) Destroy(child.gameObject);
        axisVisuals.Clear();

        axisVisuals = AxisGenerator.CreateAxes(
            axesContainer.transform,
            Constants.AXIS_LENGTH,
            Constants.AXIS_THICKNESS,
            axisOriginOffset,
            redAxisMaterial, greenAxisMaterial, blueAxisMaterial
        );
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
