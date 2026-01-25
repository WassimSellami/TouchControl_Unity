using UnityEngine;
using UnityVolumeRendering;
using System.IO;

public class VolumetricSceneController : MonoBehaviour
{
    [Header("Data Settings")]
    public string rawFilePath = "C:/Data/Head.raw";
    public int dimX = 512;
    public int dimY = 512;
    public int dimZ = 512;
    public DataContentFormat contentFormat = DataContentFormat.Int16;
    public Endianness endianness = Endianness.LittleEndian;
    public int bytesToSkip = 0;

    [Header("Slice Settings")]
    public Material sliceMaterial;
    public float separationDistance = 0.2f;

    private VolumeRenderedObject loadedVolumeObj;

    void Start()
    {
        LoadVolume();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            ExecuteSlice();
        }
    }

    void LoadVolume()
    {
        if (!File.Exists(rawFilePath)) return;

        RawDatasetImporter importer = new RawDatasetImporter(
            rawFilePath, dimX, dimY, dimZ, contentFormat, endianness, bytesToSkip
        );

        VolumeDataset dataset = importer.Import();
        loadedVolumeObj = VolumeObjectFactory.CreateObject(dataset);

        loadedVolumeObj.transform.SetParent(transform);
        loadedVolumeObj.transform.localPosition = Vector3.zero;

        if (sliceMaterial != null)
        {
            Renderer rend = loadedVolumeObj.GetComponent<Renderer>();
            if (rend == null) rend = loadedVolumeObj.GetComponentInChildren<Renderer>();
            if (rend != null) rend.sharedMaterial = sliceMaterial;
        }

        Renderer r2 = loadedVolumeObj.GetComponent<Renderer>();
        if (r2 == null) r2 = loadedVolumeObj.GetComponentInChildren<Renderer>();
        if (r2 != null)
        {
            r2.material.SetVector("_PlaneNormal", Vector3.zero);
            r2.material.SetVector("_PlanePos", new Vector3(-100, -100, -100));
        }
    }

    void ExecuteSlice()
    {
        if (loadedVolumeObj == null) return;

        GameObject originalGO = loadedVolumeObj.gameObject;
        GameObject rightGO = Instantiate(originalGO, transform);
        GameObject leftGO = Instantiate(originalGO, transform);

        rightGO.name = "Volume_Right";
        leftGO.name = "Volume_Left";

        Vector3 slicePointLocal = new Vector3(0.5f, 0.5f, 0.5f);
        Vector3 sliceNormalLocal = Vector3.right;

        ApplyVolumeCut(rightGO, slicePointLocal, sliceNormalLocal);
        ApplyVolumeCut(leftGO, slicePointLocal, -sliceNormalLocal);

        Vector3 worldDir = rightGO.transform.right.normalized;

        rightGO.transform.position += worldDir * separationDistance;
        leftGO.transform.position -= worldDir * separationDistance;

        originalGO.SetActive(false);
        loadedVolumeObj = null;
    }

    void ApplyVolumeCut(GameObject rootObj, Vector3 pointLocal, Vector3 normalLocal)
    {
        Renderer rend = rootObj.GetComponent<Renderer>();
        if (rend == null) rend = rootObj.GetComponentInChildren<Renderer>();
        if (rend == null) return;

        Material mat = new Material(rend.sharedMaterial);
        mat.SetVector("_PlanePos", pointLocal);
        mat.SetVector("_PlaneNormal", normalLocal);
        rend.material = mat;
    }
}
