using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class MockedModelController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject mockVisualModel;
    [SerializeField] private Camera referenceCamera;

    [Header("Axis Visuals")]
    [SerializeField] private Vector3 axisOriginOffset = new Vector3(0f, 0f, 0f);

    [Header("Axis Materials")]
    [SerializeField] private Material redAxisMaterial;
    [SerializeField] private Material greenAxisMaterial;
    [SerializeField] private Material blueAxisMaterial;

    private AnimationCurve PRESET_VIEW_EASE_CAVE = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
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

    private Vector3 originalMockModelScale = Vector3.one;

    public bool IsAutoRotating => isAutoRotating;

    void Awake()
    {
        initialPosition = transform.position;
        initialRotation = transform.rotation;
        initialScale = transform.localScale;

        if (mockVisualModel == null) Debug.LogWarning("[MockedModelController] Mock Visual Model not assigned.");
        if (redAxisMaterial == null || greenAxisMaterial == null || blueAxisMaterial == null) Debug.LogError("[MockedModelController] Axis materials are not assigned in the Inspector!");
        if (referenceCamera == null) Debug.LogError("[MockedModelController] Reference Camera not assigned! Panning will not work.");
        else
        {
            initialCameraPosition = referenceCamera.transform.position;
            initialCameraRotation = referenceCamera.transform.rotation;
        }

        axesContainer = new GameObject("Client_Axes_Container");
        axesContainer.transform.SetParent(this.transform, false);

        if (mockVisualModel != null)
        {
            mockVisualModel.SetActive(false);
        }
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

    public void SetModelVisibility(bool isVisible)
    {
        if (mockVisualModel != null)
        {
            mockVisualModel.SetActive(isVisible);
        }
    }

    public void ProcessOrbit(Vector2 screenDelta)
    {
        StopContinuousRotation();
        if (referenceCamera == null || isAnimatingPresetView) return;

        if (lockedOrbitAxis == OrbitAxis.None && screenDelta.sqrMagnitude > 0.01f)
        {
            if (Mathf.Abs(screenDelta.x) > Mathf.Abs(screenDelta.y)) lockedOrbitAxis = OrbitAxis.Horizontal;
            else lockedOrbitAxis = OrbitAxis.Vertical;
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
        if (referenceCamera == null || isAnimatingPresetView) return;
        Vector3 right = referenceCamera.transform.right * -screenDelta.x;
        Vector3 up = referenceCamera.transform.up * -screenDelta.y;
        Vector3 worldDelta = (right + up) * Constants.PAN_SENSITIVITY;
        transform.Translate(worldDelta, Space.World);
    }

    public void ProcessZoom(float zoomAmount)
    {
        StopContinuousRotation();
        if (isAnimatingPresetView || zoomAmount == 0) return;
        float scaleChange = 1.0f + (zoomAmount * Constants.ZOOM_SENSITIVITY);
        Vector3 newScale = transform.localScale * scaleChange;
        newScale.x = Mathf.Clamp(newScale.x, Constants.SCALE_MIN, Constants.SCALE_MAX);
        newScale.y = Mathf.Clamp(newScale.y, Constants.SCALE_MIN, Constants.SCALE_MAX);
        newScale.z = Mathf.Clamp(newScale.z, Constants.SCALE_MIN, Constants.SCALE_MAX);
        transform.localScale = newScale;
    }

    public void ProcessRoll(float angleDelta)
    {
        StopContinuousRotation();
        if (referenceCamera == null || isAnimatingPresetView) return;
        Vector3 rotationAxis = referenceCamera.transform.forward;
        transform.Rotate(rotationAxis, angleDelta * Constants.ROLL_SENSITIVITY, Space.World);
    }

    public void ResetState()
    {
        StopContinuousRotation();
        if (isAnimatingPresetView) StopAllCoroutines();
        isAnimatingPresetView = false;

        transform.position = initialPosition;
        transform.rotation = initialRotation;
        transform.localScale = initialScale;

        if (mockVisualModel != null)
        {
            mockVisualModel.transform.localScale = Vector3.one;
        }
        originalMockModelScale = Vector3.one;

        if (referenceCamera != null)
        {
            referenceCamera.transform.position = initialCameraPosition;
            referenceCamera.transform.rotation = initialCameraRotation;
        }

        EnsureAxisVisualsAreCreated();
    }


    public void TriggerPresetViewRotation(float direction)
    {
        StopContinuousRotation();
        if (isAnimatingPresetView) return;
        StartCoroutine(AnimatePresetViewRotation(direction));
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
            float easedProgress = PRESET_VIEW_EASE_CAVE.Evaluate(progress);
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, easedProgress);
            yield return null;
        }

        transform.rotation = targetRotation;
        isAnimatingPresetView = false;
    }

    public void EnsureAxisVisualsAreCreated()
    {
        if (!this.gameObject.activeInHierarchy || mockVisualModel == null) return;

        modelReferencePoint = mockVisualModel.transform.Find("ref");
        if (modelReferencePoint == null)
        {
            Debug.LogWarning("[MockedModelController] 'ref' child not found on mockVisualModel. Defaulting to model's root transform.");
            modelReferencePoint = mockVisualModel.transform;
        }

        if (!axesCreated)
        {
            CreateAxisVisuals();
            axesCreated = true;
        }
        axesContainer.SetActive(true);
    }

    public void ApplyServerModelScale(Vector3 serverModelSize)
    {

        if (mockVisualModel == null)
        {
            return;
        }

        Quaternion cachedRotation = mockVisualModel.transform.rotation;
        mockVisualModel.transform.rotation = Quaternion.identity;

        Renderer[] renderers = mockVisualModel.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            mockVisualModel.transform.rotation = cachedRotation;
            return;
        }

        Bounds currentBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            currentBounds.Encapsulate(renderers[i].bounds);
        }

        Vector3 currentWorldSize = currentBounds.size;

        if (currentWorldSize.x == 0 || currentWorldSize.y == 0 || currentWorldSize.z == 0)
        {
            mockVisualModel.transform.rotation = cachedRotation;
            return;
        }

        Vector3 requiredScaleFactor = new Vector3(
            serverModelSize.x / currentWorldSize.x,
            serverModelSize.y / currentWorldSize.y,
            serverModelSize.z / currentWorldSize.z
        );

        Vector3 newScale = Vector3.Scale(mockVisualModel.transform.localScale, requiredScaleFactor);

        mockVisualModel.transform.localScale = newScale;

        mockVisualModel.transform.rotation = cachedRotation;

        EnsureAxisVisualsAreCreated();
    }


    void CreateAxisVisuals()
    {
        foreach (Transform child in axesContainer.transform) Destroy(child.gameObject);
        axisVisuals.Clear();

        CreateSingleAxisVisual(axesContainer.transform, Vector3.right, Constants.AXIS_LENGTH, Constants.AXIS_THICKNESS, redAxisMaterial, "X_Axis_Client");
        CreateSingleAxisVisual(axesContainer.transform, Vector3.up, Constants.AXIS_LENGTH, Constants.AXIS_THICKNESS, greenAxisMaterial, "Y_Axis_Client");
        CreateSingleAxisVisual(axesContainer.transform, Vector3.forward, Constants.AXIS_LENGTH, Constants.AXIS_THICKNESS, blueAxisMaterial, "Z_Axis_Client");
    }

    void CreateSingleAxisVisual(Transform parentForAxes, Vector3 direction, float length, float thickness, Material axisMat, string baseName)
    {
        float capHeight = thickness * Constants.ARROWHEAD_HEIGHT_FACTOR;
        float shaftActualLength = Mathf.Max(thickness / 2f, length - capHeight);

        GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shaft.name = baseName + "_Shaft";
        shaft.transform.SetParent(parentForAxes, false);
        Destroy(shaft.GetComponent<CapsuleCollider>());
        shaft.transform.localScale = new Vector3(thickness, shaftActualLength / 2f, thickness);
        shaft.transform.localPosition = axisOriginOffset + direction * (shaftActualLength / 2f);
        shaft.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction);
        if (shaft.GetComponent<Renderer>() != null && axisMat != null) shaft.GetComponent<Renderer>().material = axisMat;
        axisVisuals.Add(shaft);

        GameObject arrowheadCap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        arrowheadCap.name = baseName + "_HeadCap";
        arrowheadCap.transform.SetParent(parentForAxes, false);
        Destroy(arrowheadCap.GetComponent<CapsuleCollider>());
        float capRadius = thickness * Constants.ARROWHEAD_RADIUS_FACTOR;
        arrowheadCap.transform.localScale = new Vector3(capRadius, capHeight / 2f, capRadius);
        arrowheadCap.transform.localPosition = axisOriginOffset + direction * (shaftActualLength + capHeight / 2f);
        arrowheadCap.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction);
        if (arrowheadCap.GetComponent<Renderer>() != null && axisMat != null) arrowheadCap.GetComponent<Renderer>().material = axisMat;
        axisVisuals.Add(arrowheadCap);
    }
}
