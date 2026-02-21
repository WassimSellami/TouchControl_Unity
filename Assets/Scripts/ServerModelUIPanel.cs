using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityVolumeRendering;

public class ServerModelUIPanel : MonoBehaviour
{
    [Header("UI Main Panels")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private GameObject serverListRoot;
    [SerializeField] private GameObject confirmationPopup;

    [Header("Add Model Form Fields")]
    [SerializeField] private TMP_InputField displayNameInput;
    [SerializeField] private TMP_InputField descriptionInput;
    [SerializeField] private TMP_Dropdown typeDropdown;
    [SerializeField] private TMP_Text filePathText;
    [SerializeField] private TMP_Text fileSizeText;
    [SerializeField] private TMP_Text thumbnailPathText;

    [Header("Volumetric Specifics")]
    [SerializeField] private GameObject volumetricContainer;
    [SerializeField] private TMP_InputField dimXInput;
    [SerializeField] private TMP_InputField dimYInput;
    [SerializeField] private TMP_InputField dimZInput;

    [Header("Server List Configuration")]
    [SerializeField] private Transform serverListContainer;
    [SerializeField] private GameObject assetTilePrefab;
    [SerializeField] private Sprite plusIconSprite;
    [SerializeField] private Sprite defaultThumbnailSprite;


    [Header("Buttons")]
    [SerializeField] private Button closePanelButton;
    [SerializeField] private Button selectFileButton;
    [SerializeField] private Button selectThumbnailButton;
    [SerializeField] private Button addModelButton;

    [Header("Confirmation Popup Buttons")]
    [SerializeField] private Button confirmDeleteButton;
    [SerializeField] private Button cancelDeleteButton;

    [Header("System References")]
    [SerializeField] private ModelController modelController;

    private string currentFilePath;
    private string currentThumbnailPath;
    private string pendingDeleteID;

    private void OnEnable()
    {
        AssetTile.OnRightClicked += ShowDeleteConfirmation;
        AssetTile.OnAddTileClicked += OpenPanel;
        AssetTile.OnTileSelected += HandleTileSelection;
    }

    private void OnDisable()
    {
        AssetTile.OnRightClicked -= ShowDeleteConfirmation;
        AssetTile.OnAddTileClicked -= OpenPanel;
        AssetTile.OnTileSelected -= HandleTileSelection;
    }

    void Start()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        if (volumetricContainer != null) volumetricContainer.SetActive(false);
        if (confirmationPopup != null) confirmationPopup.SetActive(false);

        closePanelButton.onClick.AddListener(ClosePanel);
        selectFileButton.onClick.AddListener(SelectFile);
        selectThumbnailButton.onClick.AddListener(SelectThumbnail);
        addModelButton.onClick.AddListener(AddModel);
        typeDropdown.onValueChanged.AddListener(OnTypeChanged);

        confirmDeleteButton.onClick.AddListener(ConfirmDeletion);
        cancelDeleteButton.onClick.AddListener(() => confirmationPopup.SetActive(false));

        PopulateServerList(modelController.GetAllModelsMetadata().models);
    }

    public void SetListVisibility(bool isVisible)
    {
        if (serverListRoot != null) serverListRoot.SetActive(isVisible);
    }

    // --- LIST POPULATION ---
    public void PopulateServerList(IEnumerable<ModelMetadata> models)
    {
        if (serverListContainer == null || assetTilePrefab == null) return;
        foreach (Transform child in serverListContainer) Destroy(child.gameObject);

        foreach (var meta in models)
        {
            GameObject go = Instantiate(assetTilePrefab, serverListContainer);
            AssetTile tile = go.GetComponent<AssetTile>();
            if (tile != null)
            {
                // Try to convert the base64 string to a sprite
                Sprite icon = Base64ToSprite(meta.thumbnailBase64);

                // FALLBACK: If the icon is null (no image provided), use the default
                if (icon == null)
                {
                    icon = defaultThumbnailSprite;
                }

                tile.Setup(meta.modelID, meta.displayName, meta.modelType, meta.fileSize, icon, false);
            }
        }

        // Add the "+" Tile at the end
        GameObject addGo = Instantiate(assetTilePrefab, serverListContainer);
        AssetTile addTile = addGo.GetComponent<AssetTile>();
        if (addTile != null) addTile.SetupAsAddButton(plusIconSprite);
    }

    private void HandleTileSelection(string id)
    {
        AssetTile.TriggerSelectionEvent(id);
    }

    private void ShowDeleteConfirmation(string modelID, Vector2 mousePos)
    {
        pendingDeleteID = modelID;
        if (confirmationPopup != null) confirmationPopup.SetActive(true);
    }

    private void ConfirmDeletion()
    {
        if (!string.IsNullOrEmpty(pendingDeleteID))
        {
            modelController.RemoveModel(pendingDeleteID);
            pendingDeleteID = null;
        }
        confirmationPopup.SetActive(false);
    }

    private void OpenPanel() { panelRoot.SetActive(true); ResetFields(); }
    public void ClosePanel() { panelRoot.SetActive(false); }
    private void OnTypeChanged(int index) { if (volumetricContainer != null) volumetricContainer.SetActive(index == 1); }

    // --- FILE HELPERS ---
    private void SelectFile()
    {
        bool isVol = (typeDropdown.value == 1);
        string filter = isVol ? "Volume Files\0*.raw;*.dat;*.ini\0All Files\0*.*\0\0" : "OBJ Files\0*.obj\0All Files\0*.*\0\0";
        string path = FileBrowserHelper.OpenFile("Select Model File", filter);
        if (!string.IsNullOrEmpty(path))
        {
            currentFilePath = path;
            filePathText.text = Path.GetFileName(path);
            float mb = new FileInfo(path).Length / (1024f * 1024f);
            fileSizeText.text = $"{mb:F2} MB";
        }
    }

    private void SelectThumbnail()
    {
        string path = FileBrowserHelper.OpenFile("Select Thumbnail", "Images\0*.png;*.jpg;*.jpeg\0\0");
        if (!string.IsNullOrEmpty(path)) { currentThumbnailPath = path; thumbnailPathText.text = Path.GetFileName(path); }
    }

    private void AddModel()
    {
        if (string.IsNullOrEmpty(displayNameInput.text) || string.IsNullOrEmpty(currentFilePath)) return;
        Sprite thumb = null;
        if (!string.IsNullOrEmpty(currentThumbnailPath) && File.Exists(currentThumbnailPath))
        {
            byte[] b = File.ReadAllBytes(currentThumbnailPath);
            Texture2D t = new Texture2D(2, 2);
            t.LoadImage(b);
            thumb = Sprite.Create(t, new Rect(0, 0, t.width, t.height), Vector2.one * 0.5f);
        }

        ModelData newData = null;
        if (typeDropdown.value == 0)
        {
            PolygonalModelData p = ScriptableObject.CreateInstance<PolygonalModelData>();
            p.modelFilePath = currentFilePath; newData = p;
        }
        else
        {
            VolumetricModelData v = ScriptableObject.CreateInstance<VolumetricModelData>();
            v.rawFilePath = currentFilePath; int.TryParse(dimXInput.text, out v.dimX);
            int.TryParse(dimYInput.text, out v.dimY); int.TryParse(dimZInput.text, out v.dimZ);
            v.dimX = Mathf.Max(1, v.dimX); v.dimY = Mathf.Max(1, v.dimY); v.dimZ = Mathf.Max(1, v.dimZ);
            v.contentFormat = DataContentFormat.Uint8; v.endianness = Endianness.LittleEndian; newData = v;
        }

        newData.modelID = System.Guid.NewGuid().ToString();
        newData.displayName = displayNameInput.text;
        newData.description = descriptionInput.text;
        newData.thumbnail = thumb;

        modelController.RegisterRuntimeModel(newData, fileSizeText.text);
        ClosePanel();
    }

    private void ResetFields()
    {
        displayNameInput.text = ""; descriptionInput.text = ""; filePathText.text = "None"; thumbnailPathText.text = "None";
        fileSizeText.text = "0.00 MB"; currentFilePath = ""; currentThumbnailPath = ""; typeDropdown.value = 0; volumetricContainer.SetActive(false);
        dimXInput.text = "256"; dimYInput.text = "256"; dimZInput.text = "256";
    }

    private Sprite Base64ToSprite(string base64)
    {
        if (string.IsNullOrEmpty(base64)) return null; // Important for fallback
        try
        {
            byte[] bytes = System.Convert.FromBase64String(base64);
            Texture2D tex = new Texture2D(2, 2);
            tex.LoadImage(bytes);
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.one * 0.5f);
        }
        catch
        {
            return null; // Return null if the data is corrupted
        }
    }
}