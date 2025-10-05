using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class MockedModelController : MonoBehaviour
{
    [Header("Control Settings")]
    [SerializeField] private float panSensitivity = 0.01f;
    [SerializeField] private float orbitSensitivity = 0.5f;
    [SerializeField] private float zoomSensitivity = 0.1f;
    [SerializeField] private float rollSensitivity = 0.5f;
    [SerializeField] private float scaleMin = 0.1f;
    [SerializeField] private float scaleMax = 10.0f;
    [SerializeField] private float presetViewRotationStep = 45f;
    [SerializeField] private float presetViewAnimationDuration = 0.4f;
    [SerializeField] private float autoRotationSpeed = 20f;
    [SerializeField] private AnimationCurve presetViewEaseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("References")]
    [SerializeField] private GameObject mockVisualModel;
    [SerializeField] private Camera referenceCamera;

    [Header("Axis Visuals")]
    [SerializeField] private Vector3 axisOriginOffset = new Vector3(0f, 0f, 0f);
    [SerializeField] private float axisLength = 10f;
    [SerializeField] private float axisThickness = 0.03f;
    [SerializeField] private float arrowheadRadiusFactor = 2.5f;
    [SerializeField] private float arrowheadHeightFactor = 3f;

    [Header("Axis Materials")]
    [SerializeField] private Material redAxisMaterial;
    [SerializeField] private Material greenAxisMaterial;
    [SerializeField] private Material blueAxisMaterial;

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
            float rotationAmount = autoRotationSpeed * autoRotationDirection * Time.deltaTime;
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
                transform.Rotate(Vector3.up, -screenDelta.x * orbitSensitivity, Space.World);
                break;
            case OrbitAxis.Vertical:
                transform.Rotate(referenceCamera.transform.right, screenDelta.y * orbitSensitivity, Space.World);
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
        Vector3 worldDelta = (right + up) * panSensitivity;
        transform.Translate(worldDelta, Space.World);
    }

    public void ProcessZoom(float zoomAmount)
    {
        StopContinuousRotation();
        if (isAnimatingPresetView || zoomAmount == 0) return;
        float scaleChange = 1.0f + (zoomAmount * zoomSensitivity);
        Vector3 newScale = transform.localScale * scaleChange;
        newScale.x = Mathf.Clamp(newScale.x, scaleMin, scaleMax);
        newScale.y = Mathf.Clamp(newScale.y, scaleMin, scaleMax);
        newScale.z = Mathf.Clamp(newScale.z, scaleMin, scaleMax);
        transform.localScale = newScale;
    }

    public void ProcessRoll(float angleDelta)
    {
        StopContinuousRotation();
        if (referenceCamera == null || isAnimatingPresetView) return;
        Vector3 rotationAxis = referenceCamera.transform.forward;
        transform.Rotate(rotationAxis, angleDelta * rollSensitivity, Space.World);
    }

    public void ResetState()
    {
        StopContinuousRotation();
        if (isAnimatingPresetView) StopAllCoroutines();
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
        Quaternion targetRotation = startRotation * Quaternion.AngleAxis(presetViewRotationStep * direction, Vector3.up);

        float elapsedTime = 0f;

        while (elapsedTime < presetViewAnimationDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsedTime / presetViewAnimationDuration);
            float easedProgress = presetViewEaseCurve.Evaluate(progress);
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
        if (mockVisualModel == null) return;
        mockVisualModel.transform.localScale = serverModelSize;
        EnsureAxisVisualsAreCreated();
    }

    void CreateAxisVisuals()
    {
        foreach (Transform child in axesContainer.transform) Destroy(child.gameObject);
        axisVisuals.Clear();

        CreateSingleAxisVisual(axesContainer.transform, Vector3.right, axisLength, axisThickness, redAxisMaterial, "X_Axis_Client");
        CreateSingleAxisVisual(axesContainer.transform, Vector3.up, axisLength, axisThickness, greenAxisMaterial, "Y_Axis_Client");
        CreateSingleAxisVisual(axesContainer.transform, Vector3.forward, axisLength, axisThickness, blueAxisMaterial, "Z_Axis_Client");
    }

    void CreateSingleAxisVisual(Transform parentForAxes, Vector3 direction, float length, float thickness, Material axisMat, string baseName)
    {
        float capHeight = thickness * arrowheadHeightFactor;
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
        float capRadius = thickness * arrowheadRadiusFactor;
        arrowheadCap.transform.localScale = new Vector3(capRadius, capHeight / 2f, capRadius);
        arrowheadCap.transform.localPosition = axisOriginOffset + direction * (shaftActualLength + capHeight / 2f);
        arrowheadCap.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction);
        if (arrowheadCap.GetComponent<Renderer>() != null && axisMat != null) arrowheadCap.GetComponent<Renderer>().material = axisMat;
        axisVisuals.Add(arrowheadCap);
    }
}