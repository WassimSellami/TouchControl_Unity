using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("UI Panels")]
    [SerializeField] private GameObject connectionPanel;
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject modelViewPanel;
    [SerializeField] private GameObject modelInfoPopup;

    [Header("Interactive Systems")]
    [SerializeField] private GameObject placeholderModelVisual;
    [SerializeField] private GameObject rotationGizmoSystem;
    [SerializeField] private GameObject cropBoxSystem;

    [Header("Controller References")]
    [SerializeField] private CameraController cameraController;
    [SerializeField] private MockedModelController mockedModelController;
    [SerializeField] private CropBoxController cropBoxController;
    [SerializeField] private InputManager inputManagerRef;

    [Header("Buttons in ModelViewPanel")]
    [SerializeField] private Button backButtonToMainMenu;
    [SerializeField] private Button toggleRotationGizmoButton;
    [SerializeField] private Button toggleCropBoxButton;
    [SerializeField] private Button resetViewAndModelButton;
    [SerializeField] private Button presetViewCycleButton;
    [SerializeField] private Button modelInfoButton;
    [SerializeField] private Button confirmCropButton;

    [Header("Buttons in Popups")]
    [SerializeField] private Button closeInfoButton;

    private bool isRotationGizmoVisible = false;
    private bool isCropBoxVisible = false;

    void Awake()
    {
        if (mockedModelController == null && placeholderModelVisual != null) mockedModelController = placeholderModelVisual.GetComponentInChildren<MockedModelController>(true);
        if (cropBoxController == null && cropBoxSystem != null) cropBoxController = cropBoxSystem.GetComponent<CropBoxController>();
        if (inputManagerRef == null) inputManagerRef = FindObjectOfType<InputManager>();
        HideAllPanelsAndPopups();
        if (placeholderModelVisual != null) placeholderModelVisual.SetActive(false);
        DeactivateInteractionSystems();
    }

    void Start()
    {
        //ShowModelViewPanelInitiallyForTesting(); // Uncomment for PC test

        if (resetViewAndModelButton != null) resetViewAndModelButton.onClick.AddListener(HandleResetViewAndModel);
        if (presetViewCycleButton != null) presetViewCycleButton.onClick.AddListener(HandlePresetViewCycle);
        if (toggleRotationGizmoButton != null) toggleRotationGizmoButton.onClick.AddListener(ToggleRotationGizmoVisibility);
        if (toggleCropBoxButton != null) toggleCropBoxButton.onClick.AddListener(ToggleCropBoxVisibility);
        if (modelInfoButton != null) modelInfoButton.onClick.AddListener(() => ToggleModelInfoPopup(true));
        if (closeInfoButton != null) closeInfoButton.onClick.AddListener(() => ToggleModelInfoPopup(false));
        if (confirmCropButton != null) confirmCropButton.onClick.AddListener(HandleConfirmCrop);
    }

    private void ShowModelViewPanelInitiallyForTesting()
    {
        HideAllPanelsAndPopups();
        if (modelViewPanel != null) modelViewPanel.SetActive(true);
        if (placeholderModelVisual != null)
        {
            placeholderModelVisual.SetActive(true);
            if (mockedModelController != null)
            {
                mockedModelController.gameObject.SetActive(true);
                mockedModelController.EnsureAxisVisualsAreCreated();
            }
        }
        isRotationGizmoVisible = false; if (rotationGizmoSystem != null) rotationGizmoSystem.SetActive(false);
        isCropBoxVisible = false; if (cropBoxSystem != null) cropBoxSystem.SetActive(false);
        if (confirmCropButton != null) confirmCropButton.gameObject.SetActive(false);
        if (inputManagerRef != null) inputManagerRef.SetCroppingModeActive(false);
    }

    private void HideAllPanelsAndPopups()
    {
        if (connectionPanel != null) connectionPanel.SetActive(false);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (modelViewPanel != null) modelViewPanel.SetActive(false);
        if (modelInfoPopup != null) modelInfoPopup.SetActive(false);
    }

    private void DeactivateInteractionSystems()
    {
        if (rotationGizmoSystem != null) rotationGizmoSystem.SetActive(false);
        isRotationGizmoVisible = false;
        if (cropBoxSystem != null) cropBoxSystem.SetActive(false);
        isCropBoxVisible = false;
        if (confirmCropButton != null) confirmCropButton.gameObject.SetActive(false);
    }

    public void ShowConnectionPanel()
    {
        HideAllPanelsAndPopups();
        if (placeholderModelVisual != null) placeholderModelVisual.SetActive(false);
        DeactivateInteractionSystems();
        if (connectionPanel != null) connectionPanel.SetActive(true);
    }

    public void ShowMainMenuPanel()
    {
        HideAllPanelsAndPopups();
        if (placeholderModelVisual != null) placeholderModelVisual.SetActive(false);
        DeactivateInteractionSystems();
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
    }

    public void ShowModelViewPanel()
    {
        HideAllPanelsAndPopups();
        if (modelViewPanel != null) modelViewPanel.SetActive(true);
        if (placeholderModelVisual != null)
        {
            placeholderModelVisual.SetActive(true);
            if (mockedModelController != null)
            {
                mockedModelController.gameObject.SetActive(true);
                mockedModelController.EnsureAxisVisualsAreCreated();
            }
        }
        if (rotationGizmoSystem != null) rotationGizmoSystem.SetActive(isRotationGizmoVisible);
        if (cropBoxSystem != null)
        {
            cropBoxSystem.SetActive(isCropBoxVisible);
            if (isCropBoxVisible && cropBoxController != null && cropBoxController.targetModel != null && cropBoxController.targetModel.gameObject.activeInHierarchy) cropBoxController.UpdateVisuals();
        }
        if (confirmCropButton != null) confirmCropButton.gameObject.SetActive(isCropBoxVisible);
        if (inputManagerRef != null) inputManagerRef.SetCroppingModeActive(isCropBoxVisible);
    }

    public void ToggleRotationGizmoVisibility()
    {
        isRotationGizmoVisible = !isRotationGizmoVisible;
        if (rotationGizmoSystem != null) rotationGizmoSystem.SetActive(isRotationGizmoVisible);
        if (inputManagerRef != null) inputManagerRef.SetCroppingModeActive(false);
        if (isRotationGizmoVisible && cropBoxSystem != null)
        {
            cropBoxSystem.SetActive(false); isCropBoxVisible = false;
            if (confirmCropButton != null) confirmCropButton.gameObject.SetActive(false);
        }
    }

    public void ToggleCropBoxVisibility()
    {
        if (cropBoxSystem == null) return;
        isCropBoxVisible = !isCropBoxVisible;
        cropBoxSystem.SetActive(isCropBoxVisible);
        if (inputManagerRef != null) inputManagerRef.SetCroppingModeActive(isCropBoxVisible);
        if (isCropBoxVisible)
        {
            if (cropBoxController != null && cropBoxController.targetModel != null && cropBoxController.targetModel.gameObject.activeInHierarchy) cropBoxController.UpdateVisuals();
            if (rotationGizmoSystem != null) { rotationGizmoSystem.SetActive(false); isRotationGizmoVisible = false; }
        }
        if (confirmCropButton != null) confirmCropButton.gameObject.SetActive(isCropBoxVisible);
    }

    private void HandleConfirmCrop()
    {
        if (cropBoxSystem != null) cropBoxSystem.SetActive(false);
        isCropBoxVisible = false;
        if (confirmCropButton != null) confirmCropButton.gameObject.SetActive(false);
        if (inputManagerRef != null) inputManagerRef.SetCroppingModeActive(false);
    }

    public void ToggleModelInfoPopup(bool show) { if (modelInfoPopup != null) modelInfoPopup.SetActive(show); }
    public void HandleResetViewAndModel() { if (cameraController != null) cameraController.ResetView(); if (mockedModelController != null) mockedModelController.ResetState(); DeactivateInteractionSystems(); if (inputManagerRef != null) inputManagerRef.SetCroppingModeActive(false); }
    private void HandlePresetViewCycle() { if (cameraController != null) cameraController.CyclePresetView(); }
}