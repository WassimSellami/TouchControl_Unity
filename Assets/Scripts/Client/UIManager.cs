using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("UI Panels")]
    [SerializeField]
    public GameObject connectionPanel;
    [SerializeField]
    public GameObject mainMenuPanel;
    [SerializeField]
    public GameObject modelViewPanel;

    [Header("System References")]
    [SerializeField]
    private MockedModelController mockedModelController;
    [SerializeField]
    private InputManager inputManagerRef;
    [SerializeField]
    private CuttingPlaneManager cuttingPlaneManager;
    [SerializeField]
    private HistoryManager historyManager;

    [Header("Buttons")]
    [SerializeField]
    private Button backButtonToMainMenu;
    [SerializeField]
    private Button ResetViewButton;
    [SerializeField]
    private Button CyclePresetViewButton;
    [SerializeField]
    private Button ResetCropButton;
    [SerializeField]
    private Button undoButton;
    [SerializeField]
    private Button redoButton;

    void Awake()
    {
        if (inputManagerRef == null) inputManagerRef = FindObjectOfType<InputManager>();
        if (cuttingPlaneManager == null) cuttingPlaneManager = FindObjectOfType<CuttingPlaneManager>();
        if (historyManager == null) historyManager = FindObjectOfType<HistoryManager>();

        if (inputManagerRef == null) Debug.LogError("[UIManager] InputManager not found in scene!");
        if (cuttingPlaneManager == null) Debug.LogError("[UIManager] CuttingPlaneManager not found in scene!");
        if (historyManager == null) Debug.LogError("[UIManager] HistoryManager not found in scene!");

        HideAllPanelsAndPopups();
    }

    void Start()
    {
        if (backButtonToMainMenu != null) backButtonToMainMenu.onClick.AddListener(ShowMainMenuPanel);
        else Debug.LogWarning("[UIManager] backButtonToMainMenu is not assigned.");

        if (ResetViewButton != null) ResetViewButton.onClick.AddListener(OnResetViewButtonPressed);
        else Debug.LogWarning("[UIManager] ResetViewButton is not assigned.");

        if (CyclePresetViewButton != null) CyclePresetViewButton.onClick.AddListener(OnCycleViewButtonPressed);
        else Debug.LogWarning("[UIManager] CyclePresetViewButton is not assigned.");

        if (ResetCropButton != null) ResetCropButton.onClick.AddListener(OnResetCropButtonPressed);
        else Debug.LogWarning("[UIManager] ResetCropButton is not assigned.");

        if (undoButton != null) undoButton.onClick.AddListener(OnUndoButtonPressed);
        else Debug.LogWarning("[UIManager] UndoButton is not assigned.");

        if (redoButton != null) redoButton.onClick.AddListener(OnRedoButtonPressed);
        else Debug.LogWarning("[UIManager] RedoButton is not assigned.");
    }

    private void OnUndoButtonPressed()
    {
        if (historyManager != null) historyManager.Undo();
    }

    private void OnRedoButtonPressed()
    {
        if (historyManager != null) historyManager.Redo();
    }

    private void OnResetCropButtonPressed()
    {
        if (cuttingPlaneManager != null) cuttingPlaneManager.ResetCrop();
        else Debug.LogWarning("[UIManager] CuttingPlaneManager is null, cannot reset crop.");
    }

    private void HideAllPanelsAndPopups()
    {
        if (connectionPanel != null) connectionPanel.SetActive(false);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (modelViewPanel != null) modelViewPanel.SetActive(false);
    }

    private void DeactivateInteractionSystems()
    {
        if (mockedModelController != null) mockedModelController.SetModelVisibility(false);
    }

    public void ShowConnectionPanel()
    {
        HideAllPanelsAndPopups();
        DeactivateInteractionSystems();
        if (connectionPanel != null) connectionPanel.SetActive(true);
    }

    public void ShowLoadingScreenOrMinimalStatus()
    {
        HideAllPanelsAndPopups();
        if (connectionPanel != null) connectionPanel.SetActive(true);
        DeactivateInteractionSystems();
    }

    public void ShowMainMenuPanel()
    {
        HideAllPanelsAndPopups();
        DeactivateInteractionSystems();
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
    }

    public void ShowModelViewPanel()
    {
        HideAllPanelsAndPopups();
        if (modelViewPanel != null) modelViewPanel.SetActive(true);

        if (mockedModelController != null)
        {
            mockedModelController.gameObject.SetActive(true);
            mockedModelController.SetModelVisibility(true);
            mockedModelController.EnsureAxisVisualsAreCreated();
        }
        else Debug.LogWarning("[UIManager] MockedModelController is null, cannot activate model visuals.");
    }

    private void OnResetViewButtonPressed()
    {
        if (mockedModelController != null) mockedModelController.ResetState();
        else Debug.LogWarning("[UIManager] MockedModelController is null, cannot reset model view state.");
    }

    private void OnCycleViewButtonPressed()
    {
        if (mockedModelController != null) mockedModelController.CyclePresetView();
        else Debug.LogWarning("[UIManager] MockedModelController is null, cannot cycle preset view.");
    }
}