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

    [Header("Controller References")]
    [SerializeField]
    private MockedModelController mockedModelController;
    [SerializeField]
    private InputManager inputManagerRef;

    [Header("Buttons in ModelViewPanel")]
    [SerializeField]
    private Button backButtonToMainMenu;
    [SerializeField]
    private Button ResetViewButton;
    [SerializeField]
    private Button CyclePresetViewButton;


    void Awake()
    {
        if (inputManagerRef == null)
        {
            inputManagerRef = FindObjectOfType<InputManager>();
            if (inputManagerRef == null) Debug.LogError("[UIManager] InputManager not found in scene!");
        }

        HideAllPanelsAndPopups();
    }

    void Start()
    {
        if (backButtonToMainMenu != null)
        {
            backButtonToMainMenu.onClick.AddListener(ShowMainMenuPanel);
        }
        else
        {
            Debug.LogWarning("[UIManager] backButtonToMainMenu is not assigned.");
        }

        if (ResetViewButton != null)
        {
            ResetViewButton.onClick.AddListener(OnResetButtonPressed);
        }
        else
        {
            Debug.LogWarning("[UIManager] ResetViewButton is not assigned.");
        }

        if (CyclePresetViewButton != null)
        {
            CyclePresetViewButton.onClick.AddListener(OnCycleViewButtonPressed);
        }
        else
        {
            Debug.LogWarning("[UIManager] CyclePresetViewButton is not assigned.");
        }
    }

    private void HideAllPanelsAndPopups()
    {
        if (connectionPanel != null) connectionPanel.SetActive(false);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (modelViewPanel != null) modelViewPanel.SetActive(false);
    }

    private void DeactivateInteractionSystems()
    {
        // This method can be kept for future use if needed.
    }

    public void ShowConnectionPanel()
    {
        HideAllPanelsAndPopups();
        DeactivateInteractionSystems();
        if (connectionPanel != null) connectionPanel.SetActive(true);
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
            mockedModelController.EnsureAxisVisualsAreCreated();
        }
        else
        {
            Debug.LogWarning("[UIManager] MockedModelController is null, cannot activate model visuals.");
        }
    }

    private void OnResetButtonPressed()
    {
        if (mockedModelController != null)
        {
            mockedModelController.ResetState();
            Debug.Log("[UIManager] Model state reset.");
        }
        else
        {
            Debug.LogWarning("[UIManager] MockedModelController is null, cannot reset model state.");
        }
    }

    private void OnCycleViewButtonPressed()
    {
        if (mockedModelController != null)
        {
            mockedModelController.CyclePresetView();
            Debug.Log("[UIManager] Model preset view cycled.");
        }
        else
        {
            Debug.LogWarning("[UIManager] MockedModelController is null, cannot cycle preset view.");
        }
    }
}