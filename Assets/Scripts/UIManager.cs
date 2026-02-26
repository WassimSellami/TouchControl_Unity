using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;

public class UIManager : MonoBehaviour
{
    [Header("UI Panels")]
    [SerializeField] public GameObject connectionPanel;
    [SerializeField] public GameObject mainMenuPanel;
    [SerializeField] public GameObject modelViewPanel;
    [SerializeField] public GameObject infoPanel;

    [Header("Model Button Setup")]
    [SerializeField] private Transform modelButtonsContainer;
    [SerializeField] private GameObject modelButtonPrefab;

    [Header("System References")]
    [SerializeField] private ModelViewportController mockedModelController;
    [SerializeField] private InputManager inputManagerRef;
    [SerializeField] private CuttingPlaneManager cuttingPlaneManager;
    [SerializeField] private HistoryManager historyManager;
    [SerializeField] private WebSocketClientManager wsManager;

    [Header("Buttons")]
    [SerializeField] private Button backButtonToMainMenu;
    [SerializeField] private Button ResetViewButton;
    [SerializeField] private Button ResetCropButton;
    [SerializeField] private Button undoButton;
    [SerializeField] private Button redoButton;
    [SerializeField] private Button infoButton;
    [SerializeField] private Button toggleAxesButton;

    [Header("Volumetric Controls")]
    [SerializeField] private GameObject densityControlPanel;
    [SerializeField] private Button toggleDensityButton;
    [SerializeField] private Slider minDensitySlider;
    [SerializeField] private Slider maxDensitySlider;

    [Header("Fallbacks")]
    [SerializeField] private Sprite defaultThumbnail;

    private List<GameObject> dynamicModelButtons = new List<GameObject>();
    private string currentActiveModelID = "";
    private HashSet<string> visitedModelIds = new HashSet<string>();
    private bool currentAxesState = true;

    void Awake()
    {
        HideAllPanelsAndPopups();
    }

    void Start()
    {
        if (backButtonToMainMenu != null) backButtonToMainMenu.onClick.AddListener(ShowMainMenuPanel);
        if (ResetViewButton != null) ResetViewButton.onClick.AddListener(OnResetViewButtonPressed);
        if (ResetCropButton != null) ResetCropButton.onClick.AddListener(OnResetCropButtonPressed);
        if (undoButton != null) undoButton.onClick.AddListener(OnUndoButtonPressed);
        if (redoButton != null) redoButton.onClick.AddListener(OnRedoButtonPressed);
        if (infoButton != null) infoButton.onClick.AddListener(ToggleInfoPanel);
        if (toggleAxesButton != null) toggleAxesButton.onClick.AddListener(OnToggleAxesPressed);

        if (toggleDensityButton != null) toggleDensityButton.onClick.AddListener(OnToggleDensityPressed);
        if (densityControlPanel != null) densityControlPanel.SetActive(false);

        if (minDensitySlider != null) minDensitySlider.onValueChanged.AddListener(OnDensityChanged);
        if (maxDensitySlider != null) maxDensitySlider.onValueChanged.AddListener(OnDensityChanged);
    }

    private void OnToggleDensityPressed()
    {
        if (densityControlPanel != null)
        {
            bool isActive = !densityControlPanel.activeSelf;
            densityControlPanel.SetActive(isActive);
            if (inputManagerRef != null) inputManagerRef.SetInteractionEnabled(!isActive);
        }
    }

    private void OnDensityChanged(float value)
    {
        if (wsManager != null && minDensitySlider != null && maxDensitySlider != null)
        {
            wsManager.SendVolumeDensity(minDensitySlider.value, maxDensitySlider.value);
        }
    }

    public void PopulateModelButtons(List<ModelMetadata> models, WebSocketClientManager wsManagerRef)
    {
        ClearDynamicButtons();
        if (modelButtonsContainer == null || modelButtonPrefab == null) return;
        foreach (var meta in models)
        {
            GameObject btn = Instantiate(modelButtonPrefab, modelButtonsContainer);
            btn.name = meta.modelID;
            AssetTile tile = btn.GetComponent<AssetTile>();

            if (tile != null)
            {
                Sprite icon = null;
                if (!string.IsNullOrEmpty(meta.thumbnailBase64))
                    icon = wsManagerRef.Base64ToSprite(meta.thumbnailBase64);

                if (icon == null) icon = defaultThumbnail;

                tile.Setup(meta.modelID, meta.displayName, meta.modelType, meta.fileSize, icon, currentActiveModelID == meta.modelID);
                if (visitedModelIds.Contains(meta.modelID)) tile.SetVisited();
            }
            var dragComp = btn.AddComponent<DraggableModelIcon>();
            dragComp.ModelID = meta.modelID;
            dragComp.OnModelDropped = (id) => { wsManagerRef.OnLoadModelSelected(id); MarkModelAsVisited(id); };
            dynamicModelButtons.Add(btn);
        }
        RefreshSelectionHighlights(currentActiveModelID);
    }

    private void MarkModelAsVisited(string id)
    {
        if (!visitedModelIds.Contains(id)) visitedModelIds.Add(id);
        foreach (GameObject btn in dynamicModelButtons)
        {
            if (btn.name == id) { AssetTile tile = btn.GetComponent<AssetTile>(); if (tile != null) tile.SetVisited(); break; }
        }
    }

    public void RefreshSelectionHighlights(string activeID)
    {
        currentActiveModelID = activeID;
        foreach (GameObject btn in dynamicModelButtons)
        {
            if (btn == null) continue;
            Transform border = btn.transform.Find("SelectionBorder");
            if (border != null) border.gameObject.SetActive(btn.name == currentActiveModelID);
        }
    }

    private void ClearDynamicButtons() { foreach (GameObject btn in dynamicModelButtons) if (btn != null) Destroy(btn); dynamicModelButtons.Clear(); }
    private void OnUndoButtonPressed() { if (historyManager != null) historyManager.Undo(); }
    private void OnRedoButtonPressed() { if (historyManager != null) historyManager.Redo(); }
    private void OnResetCropButtonPressed() { if (cuttingPlaneManager != null) cuttingPlaneManager.ResetCrop(); }
    private void OnResetViewButtonPressed() { if (mockedModelController != null) mockedModelController.ResetState(); }

    private void OnToggleAxesPressed()
    {
        currentAxesState = !currentAxesState;
        if (mockedModelController != null) mockedModelController.SetAxesVisibility(currentAxesState);
        if (wsManager != null) wsManager.SendToggleAxes(currentAxesState);
    }

    private void ToggleInfoPanel() { if (infoPanel != null) { bool isOpening = !infoPanel.activeSelf; infoPanel.SetActive(isOpening); if (inputManagerRef != null) inputManagerRef.enabled = !isOpening; } }
    private void HideAllPanelsAndPopups() { if (connectionPanel != null) connectionPanel.SetActive(false); if (mainMenuPanel != null) mainMenuPanel.SetActive(false); if (modelViewPanel != null) modelViewPanel.SetActive(false); if (infoPanel != null) infoPanel.SetActive(false); }
    private void DeactivateInteractionSystems() { if (mockedModelController != null) { mockedModelController.SetModelVisibility(false); mockedModelController.gameObject.SetActive(false); } if (inputManagerRef != null) inputManagerRef.enabled = false; }
    public void ShowConnectionPanel() { HideAllPanelsAndPopups(); DeactivateInteractionSystems(); if (connectionPanel != null) connectionPanel.SetActive(true); }
    public void ShowLoadingScreenOrMinimalStatus() { HideAllPanelsAndPopups(); if (connectionPanel != null) connectionPanel.SetActive(true); DeactivateInteractionSystems(); }
    public void ShowMainMenuPanel() { HideAllPanelsAndPopups(); DeactivateInteractionSystems(); if (mainMenuPanel != null) mainMenuPanel.SetActive(true); }

    public void ShowModelViewPanel()
    {
        HideAllPanelsAndPopups();
        if (modelViewPanel != null) modelViewPanel.SetActive(true);
        StartCoroutine(InitializeModelViewRoutine());

        // 1. Reset Internal State
        currentAxesState = true;

        // 2. Reset Proxy Controller
        if (mockedModelController != null)
        {
            mockedModelController.gameObject.SetActive(true);
            mockedModelController.SetModelVisibility(true);
            mockedModelController.EnsureAxisVisualsAreCreated();
            mockedModelController.SetAxesVisibility(true); // Force Axes On
        }

        // 3. Reset Input
        if (inputManagerRef != null)
        {
            inputManagerRef.enabled = true;
            inputManagerRef.SetInteractionEnabled(true); // Force Input On
        }

        // 4. Reset Density Panel
        if (densityControlPanel != null) densityControlPanel.SetActive(false);
        if (minDensitySlider != null) minDensitySlider.SetValueWithoutNotify(0f);
        if (maxDensitySlider != null) maxDensitySlider.SetValueWithoutNotify(1f);

        // 5. Send Reset Sync to Server (in case server was left with axes off)
        if (wsManager != null) wsManager.SendToggleAxes(true);
    }
    private IEnumerator InitializeModelViewRoutine()
    {
        yield return null; // Wait one frame for the build to activate objects

        currentAxesState = true;

        if (mockedModelController != null)
        {
            mockedModelController.gameObject.SetActive(true);
            mockedModelController.SetModelVisibility(true);
            mockedModelController.ResetState(); // This calls EnsureAxisVisualsAreCreated
            mockedModelController.SetAxesVisibility(true);
        }

        if (inputManagerRef != null)
        {
            inputManagerRef.enabled = true;
            inputManagerRef.SetInteractionEnabled(true);
        }

        if (wsManager != null) wsManager.SendToggleAxes(true);
    }

}