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
        HideAllPanelsAndPopups();
        if (placeholderModelVisual != null) placeholderModelVisual.SetActive(false);
        DeactivateInteractionSystems();
    }

    void Start()
    {
        if (resetViewAndModelButton != null) resetViewAndModelButton.onClick.AddListener(HandleResetViewAndModel);
        if (presetViewCycleButton != null) presetViewCycleButton.onClick.AddListener(HandlePresetViewCycle);
        if (toggleRotationGizmoButton != null) toggleRotationGizmoButton.onClick.AddListener(ToggleRotationGizmoAndAxesVisibility); // Changed listener
        if (toggleCropBoxButton != null) toggleCropBoxButton.onClick.AddListener(ToggleCropBoxVisibility);
        if (modelInfoButton != null) modelInfoButton.onClick.AddListener(() => ToggleModelInfoPopup(true));
        if (closeInfoButton != null) closeInfoButton.onClick.AddListener(() => ToggleModelInfoPopup(false));

        if (mockedModelController == null && placeholderModelVisual != null)
        {
            mockedModelController = placeholderModelVisual.GetComponentInChildren<MockedModelController>(true);
        }
        if (mockedModelController == null) Debug.LogError("UIManager: MockedModelController reference is not set and could not be found.");
    }

    private void HideAllPanelsAndPopups()
    {
        if (connectionPanel != null) connectionPanel.SetActive(false);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (modelViewPanel != null) modelViewPanel.SetActive(false);
        if (modelInfoPopup != null) modelInfoPopup.SetActive(false);
    }

    private void DeactivateInteractionSystems(bool keepAxesState = false)
    {
        if (rotationGizmoSystem != null) rotationGizmoSystem.SetActive(false);
        // isRotationGizmoVisible = false; // State is managed by the toggle button

        if (mockedModelController != null && !keepAxesState)
        {
            mockedModelController.SetAxisVisualsActive(false); // Hide axes when deactivating other systems
        }

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
        // DeactivateInteractionSystems(); // Don't hide axes here, let button state control it
        if (modelViewPanel != null) modelViewPanel.SetActive(true);
        if (placeholderModelVisual != null)
        {
            placeholderModelVisual.SetActive(true); // Activate the parent
            if (mockedModelController != null)
            {
                mockedModelController.gameObject.SetActive(true); // Ensure MockedModel itself is active
                mockedModelController.EnsureAxisVisualsAreCreated(); // Create axes if not already there
                // Visibility of axes is now tied to gizmo visibility state
                mockedModelController.SetAxisVisualsActive(isRotationGizmoVisible);
            }
        }

        if (rotationGizmoSystem != null) rotationGizmoSystem.SetActive(isRotationGizmoVisible);
        if (cropBoxSystem != null) cropBoxSystem.SetActive(isCropBoxVisible); // Keep this for crop system
        if (confirmCropButton != null) confirmCropButton.gameObject.SetActive(isCropBoxVisible);
    }

    public void ToggleRotationGizmoAndAxesVisibility() // Renamed method
    {
        isRotationGizmoVisible = !isRotationGizmoVisible;

        if (rotationGizmoSystem != null)
        {
            rotationGizmoSystem.SetActive(isRotationGizmoVisible);
        }
        if (mockedModelController != null)
        {
            mockedModelController.SetAxisVisualsActive(isRotationGizmoVisible);
        }

        if (isRotationGizmoVisible && cropBoxSystem != null) // If gizmo becomes active, hide crop
        {
            cropBoxSystem.SetActive(false);
            isCropBoxVisible = false;
            if (confirmCropButton != null) confirmCropButton.gameObject.SetActive(false);
        }
    }

    public void ToggleCropBoxVisibility()
    {
        if (cropBoxSystem == null) return;
        isCropBoxVisible = !isCropBoxVisible;
        cropBoxSystem.SetActive(isCropBoxVisible);
        if (confirmCropButton != null) confirmCropButton.gameObject.SetActive(isCropBoxVisible);

        if (isCropBoxVisible) // If crop box becomes active, hide gizmo and its axes
        {
            if (rotationGizmoSystem != null) rotationGizmoSystem.SetActive(false);
            if (mockedModelController != null) mockedModelController.SetAxisVisualsActive(false);
            isRotationGizmoVisible = false;
        }
    }

    public void ToggleModelInfoPopup(bool show)
    {
        if (modelInfoPopup != null) modelInfoPopup.SetActive(show);
    }

    public void HandleResetViewAndModel()
    {
        if (cameraController != null) cameraController.ResetView();
        if (mockedModelController != null) mockedModelController.ResetState(); // This will hide axes

        isRotationGizmoVisible = false; // Reset gizmo state
        if (rotationGizmoSystem != null) rotationGizmoSystem.SetActive(false);

        DeactivateInteractionSystems(); // This will also ensure axes are hidden by default after reset
    }

    private void HandlePresetViewCycle()
    {
        if (cameraController != null) cameraController.CyclePresetView();
        DeactivateInteractionSystems(true); // Keep axes state as per gizmo button
        if (rotationGizmoSystem != null) rotationGizmoSystem.SetActive(isRotationGizmoVisible);
        if (mockedModelController != null) mockedModelController.SetAxisVisualsActive(isRotationGizmoVisible);

    }
}