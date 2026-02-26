using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.IO;
using System.Collections.Generic;
using UnityVolumeRendering;
public class ServerModelUIPanel : MonoBehaviour
{
    [Header("UI Main Panels")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private GameObject serverListRoot;
    [SerializeField] private GameObject confirmationPopup;
    [Header("Add Model Form Fields")]
[SerializeField] private TMP_InputField displayNameInput;
    [SerializeField] private TMP_Dropdown typeDropdown;
    [SerializeField] private TMP_Text filePathText;
    [SerializeField] private TMP_Text fileSizeText;
    [SerializeField] private TMP_Text statusText;

    [Header("Volumetric Configuration")]
    [SerializeField] private GameObject volumetricContainer;
    [SerializeField] private TMP_InputField dimXInput;
    [SerializeField] private TMP_InputField dimYInput;
    [SerializeField] private TMP_InputField dimZInput;

    [Header("Assets & References")]
    [SerializeField] private Transform serverListContainer;
    [SerializeField] private GameObject assetTilePrefab;
    [SerializeField] private Sprite plusIconSprite;
    [SerializeField] private Sprite defaultThumbnailSprite;
    [SerializeField] private ModelController modelController;

    [Header("Buttons")]
    [SerializeField] private Button closePanelButton;
    [SerializeField] private Button selectFileButton;
    [SerializeField] private Button selectThumbnailButton;
    [SerializeField] private Button addModelButton;
    [SerializeField] private Button confirmDeleteButton;
    [SerializeField] private Button cancelDeleteButton;

    private string currentFilePath;
    private string currentThumbnailPath;
    private string pendingDeleteID;

    private int detectedSkip = 0;
    private DataContentFormat detectedFormat = DataContentFormat.Uint8;
    private Endianness detectedEndian = Endianness.LittleEndian;

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
        InitializeUI();
        RefreshList();
    }

    private void InitializeUI()
    {
        if (panelRoot) panelRoot.SetActive(false);
        if (volumetricContainer) volumetricContainer.SetActive(false);
        if (confirmationPopup) confirmationPopup.SetActive(false);

        closePanelButton.onClick.AddListener(ClosePanel);
        selectFileButton.onClick.AddListener(SelectFile);
        selectThumbnailButton.onClick.AddListener(SelectThumbnail);
        addModelButton.onClick.AddListener(AddModel);
        confirmDeleteButton.onClick.AddListener(ConfirmDeletion);
        cancelDeleteButton.onClick.AddListener(() => confirmationPopup.SetActive(false));


        dimXInput.onValueChanged.AddListener(_ => AutoAnalyzeFormat());
        dimYInput.onValueChanged.AddListener(_ => AutoAnalyzeFormat());
        dimZInput.onValueChanged.AddListener(_ => AutoAnalyzeFormat());
    }

    private void SelectFile()
    {
        bool isVol = (typeDropdown.value == Constants.VOLUMETRIC_DROPDOWN_INDEX);
        string filter = isVol ? "Volume Files\0*.nii;*.nii.gz;*.nrrd;*.nhdr;*.dcm;*.raw;*.dat;*.vol;*.vgi\0All\0*.*\0\0" : "OBJ\0*.obj\0All\0*.*\0\0";

        string path = FileBrowserHelper.OpenFile("Select File", filter);
        if (string.IsNullOrEmpty(path)) return;

        currentFilePath = path;
        filePathText.text = Path.GetFileName(path);
        fileSizeText.text = $"{(new FileInfo(path).Length / 1024f / 1024f):F2} MB";

        if (isVol)
        {
            string ext = Path.GetExtension(path).ToLower();
            IImageFileImporter nativeImporter = null;

            if (ext == ".nii" || path.ToLower().EndsWith(".nii.gz")) nativeImporter = ImporterFactory.CreateImageFileImporter(ImageFileFormat.NIFTI);
            else if (ext == ".nrrd" || ext == ".nhdr") nativeImporter = ImporterFactory.CreateImageFileImporter(ImageFileFormat.NRRD);
            if (nativeImporter != null)
            {
                statusText.text = $"Native UVR format detected: {Path.GetExtension(path).ToUpper()}";
                volumetricContainer.SetActive(false);
                detectedSkip = 0;
                detectedFormat = DataContentFormat.Uint8;
            }
            else
            {
                volumetricContainer.SetActive(true);
                AnalyzeVolumeFile(path);
            }
        }
    }

    private void AnalyzeVolumeFile(string path)
    {
        string ext = Path.GetExtension(path).ToLower();
        long totalBytes = new FileInfo(path).Length;

        if (ext == ".dat" && TryParseDatText(path)) return;
        if (TryGuessDimensions(totalBytes)) return;

        statusText.text = "Format unknown. Please enter dimensions manually.";
    }

    private bool TryParseDatText(string path)
    {
        try
        {
            string content = File.ReadAllText(path);
            if (content.Contains("Resolution"))
            {
                string[] lines = content.Split('\n');
                foreach (string line in lines)
                {
                    if (line.StartsWith("Resolution"))
                    {
                        var parts = line.Split(new[] { ':', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        SetDimensionFields(int.Parse(parts[1]), int.Parse(parts[2]), int.Parse(parts[3]));
                        statusText.text = "Detected .DAT Text Metadata";
                        return true;
                    }
                }
            }
        }
        catch { }
        return false;
    }

    private bool TryGuessDimensions(long length)
    {
        int[] cubes = { 128, 256, 512, 1024 };
        foreach (int s in cubes)
        {
            long v8 = (long)s * s * s;
            long v16 = v8 * 2;
            if (length >= v8 && length < v8 + 2048) { SetDimensionFields(s, s, s); return true; }
            if (length >= v16 && length < v16 + 2048) { SetDimensionFields(s, s, s); return true; }
        }
        return false;
    }

    private void AutoAnalyzeFormat()
    {
        if (string.IsNullOrEmpty(currentFilePath)) return;
        if (!int.TryParse(dimXInput.text, out int x) || !int.TryParse(dimYInput.text, out int y) || !int.TryParse(dimZInput.text, out int z)) return;

        long totalBytes = new FileInfo(currentFilePath).Length;
        long voxelCount = (long)x * y * z;

        if (totalBytes >= voxelCount * 2)
        {
            detectedFormat = DataContentFormat.Uint16;
            detectedSkip = (int)(totalBytes - (voxelCount * 2));
        }
        else
        {
            detectedFormat = DataContentFormat.Uint8;
            detectedSkip = (int)(totalBytes - voxelCount);
        }

        if (detectedSkip < 0) detectedSkip = 0;
        statusText.text = $"Auto-Config: {detectedFormat}, Skip: {detectedSkip} bytes";
    }

    private void SetDimensionFields(int x, int y, int z)
    {
        dimXInput.text = x.ToString();
        dimYInput.text = y.ToString();
        dimZInput.text = z.ToString();
        AutoAnalyzeFormat();
    }

    private void AddModel()
    {
        if (string.IsNullOrEmpty(displayNameInput.text))
        {
            statusText.text = "<color=red>Error: Enter a Display Name</color>";
            return;
        }

        if (string.IsNullOrEmpty(currentFilePath))
        {
            statusText.text = "<color=red>Error: No file selected</color>";
            return;
        }

        ModelData newData;
        if (typeDropdown.value == Constants.POLYGONAL_DROPDOWN_INDEX)
        {
            var p = ScriptableObject.CreateInstance<PolygonalModelData>();
            p.modelFilePath = currentFilePath;
            newData = p;
        }
        else
        {
            var v = ScriptableObject.CreateInstance<VolumetricModelData>();
            v.rawFilePath = currentFilePath;

            if (volumetricContainer.activeSelf)
            {
                if (!int.TryParse(dimXInput.text, out int x) ||
                    !int.TryParse(dimYInput.text, out int y) ||
                    !int.TryParse(dimZInput.text, out int z) ||
                    x <= 0 || y <= 0 || z <= 0)
                {
                    statusText.text = "<color=red>Error: Dimensions X, Y, Z must be greater than 0</color>";
                    return;
                }

                v.dimX = x;
                v.dimY = y;
                v.dimZ = z;
                v.contentFormat = detectedFormat;
                v.bytesToSkip = detectedSkip;
                v.endianness = detectedEndian;

                long fileSize = new FileInfo(currentFilePath).Length;
                long requiredBytes = (long)x * y * z * (detectedFormat == DataContentFormat.Uint16 ? 2 : 1);
                if (fileSize < (requiredBytes + detectedSkip))
                {
                    statusText.text = "<color=red>Error: File too small for these dimensions</color>";
                    return;
                }
            }
            else
            {
                v.dimX = 1;
                v.dimY = 1;
                v.dimZ = 1;
                v.bytesToSkip = 0;
            }

            newData = v;
        }

        newData.modelID = Guid.NewGuid().ToString();
        newData.displayName = displayNameInput.text;
        newData.thumbnail = LoadThumbnail(currentThumbnailPath);

        modelController.RegisterRuntimeModel(newData, fileSizeText.text, currentThumbnailPath);
        ClosePanel();
        RefreshList();
    }

    private void SelectThumbnail()
    {
        string path = FileBrowserHelper.OpenFile("Select Thumbnail", "Images\0*.png;*.jpg;*.jpeg\0\0");
        if (!string.IsNullOrEmpty(path)) { currentThumbnailPath = path; }
    }

    private Sprite LoadThumbnail(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
        byte[] b = File.ReadAllBytes(path);
        Texture2D t = new Texture2D(2, 2);
        t.LoadImage(b);
        return Sprite.Create(t, new Rect(0, 0, t.width, t.height), Vector2.one * 0.5f);
    }

    private void RefreshList() => PopulateServerList(modelController.GetAllModelsMetadata().models);

    public void PopulateServerList(IEnumerable<ModelMetadata> models)
    {
        foreach (Transform child in serverListContainer) Destroy(child.gameObject);
        foreach (var meta in models)
        {
            var tile = Instantiate(assetTilePrefab, serverListContainer).GetComponent<AssetTile>();
            Sprite icon = Base64ToSprite(meta.thumbnailBase64) ?? defaultThumbnailSprite;
            tile.Setup(meta.modelID, meta.displayName, meta.modelType, meta.fileSize, icon, false);
        }
        Instantiate(assetTilePrefab, serverListContainer).GetComponent<AssetTile>().SetupAsAddButton(plusIconSprite);
    }

    public void SetListVisibility(bool isVisible)
    {
        if (serverListRoot != null) serverListRoot.SetActive(isVisible);
    }

    private Sprite Base64ToSprite(string base64)
    {
        if (string.IsNullOrEmpty(base64)) return null;
        try
        {
            byte[] bytes = Convert.FromBase64String(base64);
            Texture2D tex = new Texture2D(2, 2);
            tex.LoadImage(bytes);
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.one * 0.5f);
        }
        catch { return null; }
    }

    private void ConfirmDeletion()
    {
        if (!string.IsNullOrEmpty(pendingDeleteID)) modelController.RemoveModel(pendingDeleteID);
        confirmationPopup.SetActive(false);
        RefreshList();
    }

    private void OpenPanel() { panelRoot.SetActive(true); ResetFields(); }
    public void ClosePanel() => panelRoot.SetActive(false);
    private void HandleTileSelection(string id) => AssetTile.TriggerSelectionEvent(id);
    private void ShowDeleteConfirmation(string id, Vector2 pos) { pendingDeleteID = id; confirmationPopup.SetActive(true); }

    private void ResetFields()
    {
        displayNameInput.text = "";
        currentFilePath = "";
        currentThumbnailPath = "";
        filePathText.text = "None";
        statusText.text = "Select a file...";

        dimXInput.text = "256";
        dimYInput.text = "256";
        dimZInput.text = "256";
        detectedSkip = 0;
        detectedFormat = DataContentFormat.Uint8;
    }
}