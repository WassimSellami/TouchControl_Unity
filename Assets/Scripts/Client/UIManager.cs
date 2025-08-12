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
    private CameraController cameraController;
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
        Debug.Log("[UIManager] Initial panel setup completed.");
    }

    void Start()
    {
        if (backButtonToMainMenu != null)
        {
            backButtonToMainMenu.onClick.AddListener(ShowMainMenuPanel);
            Debug.Log("[UIManager] Back button to Main Menu listener hooked.");
        }
        else
        {
            Debug.LogWarning("[UIManager] backButtonToMainMenu is not assigned. Its functionality will not work.");
        }

        if (ResetViewButton != null)
        {
            ResetViewButton.onClick.AddListener(ResetCameraView);
            Debug.Log("[UIManager] Reset View Button listener hooked.");
        }
        else
        {
            Debug.LogWarning("[UIManager] ResetViewButton is not assigned. Its functionality will not work.");
        }

        if (CyclePresetViewButton != null)
        {
            CyclePresetViewButton.onClick.AddListener(CycleCameraPresetView);
            Debug.Log("[UIManager] Cycle Preset View Button listener hooked.");
        }
        else
        {
            Debug.LogWarning("[UIManager] CyclePresetViewButton is not assigned. Its functionality will not work.");
        }
    }

    private void HideAllPanelsAndPopups()
    {
        Debug.Log("[UIManager] Attempting to hide all UI panels and popups...");

        if (connectionPanel != null)
        {
            connectionPanel.SetActive(false);
            Debug.Log("[UIManager] Connection Panel set active(false).");
        }
        else Debug.LogWarning("[UIManager] Connection Panel reference is null, cannot hide.");

        if (mainMenuPanel != null)
        {
            mainMenuPanel.SetActive(false);
            Debug.Log("[UIManager] Main Menu Panel set active(false).");
        }
        else Debug.LogWarning("[UIManager] Main Menu Panel reference is null, cannot hide.");

        if (modelViewPanel != null)
        {
            modelViewPanel.SetActive(false);
            Debug.Log("[UIManager] Model View Panel set active(false).");
        }
        else Debug.LogWarning("[UIManager] Model View Panel reference is null, cannot hide.");

        Debug.Log("[UIManager] Finished attempt to hide all UI panels and popups.");
    }

    private void DeactivateInteractionSystems()
    {
        Debug.Log("[UIManager] Interactive systems deactivated (currently no assigned systems).");
    }

    public void ShowConnectionPanel()
    {
        HideAllPanelsAndPopups();
        DeactivateInteractionSystems();
        if (connectionPanel != null) connectionPanel.SetActive(true);
        else Debug.LogError("[UIManager] Connection Panel reference is null, cannot show.");
        Debug.Log("[UIManager] Displaying Connection Panel.");
    }

    public void ShowMainMenuPanel()
    {
        HideAllPanelsAndPopups();
        DeactivateInteractionSystems();
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        else Debug.LogError("[UIManager] Main Menu Panel reference is null, cannot show.");
        Debug.Log("[UIManager] Displaying Main Menu Panel.");
    }

    public void ShowModelViewPanel()
    {
        HideAllPanelsAndPopups();
        if (modelViewPanel != null) modelViewPanel.SetActive(true);
        else Debug.LogError("[UIManager] Model View Panel reference is null, cannot show.");

        if (mockedModelController != null)
        {
            mockedModelController.gameObject.SetActive(true);
            mockedModelController.EnsureAxisVisualsAreCreated();
            Debug.Log("[UIManager] Mocked model activated in Model View Panel.");
        }
        else
        {
            Debug.LogWarning("[UIManager] MockedModelController is null, cannot activate model visuals. Please assign it in Inspector.");
        }

        Debug.Log("[UIManager] Displaying Model View Panel.");
    }

    public void HandleResetViewAndModel()
    {
        if (cameraController != null) cameraController.ResetView();
        else Debug.LogWarning("[UIManager] CameraController is null, cannot reset view.");

        if (mockedModelController != null) mockedModelController.ResetState();
        else Debug.LogWarning("[UIManager] MockedModelController is null, cannot reset model state.");

        DeactivateInteractionSystems();
        Debug.Log("[UIManager] Resetting View and Model State (if controllers assigned).");
    }

    private void ResetCameraView()
    {
        if (cameraController != null)
        {
            cameraController.ResetView();
            Debug.Log("[UIManager] Camera view reset to initial state.");
        }
        else
        {
            Debug.LogWarning("[UIManager] CameraController is null, cannot reset camera view.");
        }
    }

    private void CycleCameraPresetView()
    {
        if (cameraController != null)
        {
            cameraController.CyclePresetView();
            Debug.Log("[UIManager] Camera preset view cycled.");
        }
        else
        {
            Debug.LogWarning("[UIManager] CameraController is null, cannot cycle preset view.");
        }
    }
}