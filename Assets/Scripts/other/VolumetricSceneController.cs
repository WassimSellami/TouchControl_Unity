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

    [Header("Interaction Settings")]
    public Material sliceMaterial;
    public float separationDistance = 0.4f;
    public float minSwipeDistance = 20f;
    public float zoomSensitivity = 0.01f;
    public float scaleMin = 0.1f;
    public float scaleMax = 5.0f;

    private VolumeRenderedObject loadedVolumeObj;
    private Vector2 touchStartPos;

    void Start()
    {
        LoadVolume();
    }

    void Update()
    {
        HandleInput();
    }

    private void HandleInput()
    {
        if (Input.touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
            {
                touchStartPos = touch.position;
            }
            else if (touch.phase == TouchPhase.Ended)
            {
                Vector2 touchEndPos = touch.position;
                Vector2 swipeVector = touchEndPos - touchStartPos;
                if (swipeVector.magnitude > minSwipeDistance)
                {
                    TryExecuteSlice(touchStartPos, touchEndPos, swipeVector);
                }
            }
        }
        else if (Input.touchCount == 2)
        {
            Touch touch0 = Input.GetTouch(0);
            Touch touch1 = Input.GetTouch(1);

            Vector2 touch0PrevPos = touch0.position - touch0.deltaPosition;
            Vector2 touch1PrevPos = touch1.position - touch1.deltaPosition;

            float prevDistance = (touch0PrevPos - touch1PrevPos).magnitude;
            float currentDistance = (touch0.position - touch1.position).magnitude;

            float zoomAmount = currentDistance - prevDistance;
            ProcessZoom(zoomAmount);
        }
    }

    private void ProcessZoom(float zoomAmount)
    {
        float scaleChange = 1.0f + zoomAmount * zoomSensitivity;
        Vector3 newScale = transform.localScale * scaleChange;

        newScale.x = Mathf.Clamp(newScale.x, scaleMin, scaleMax);
        newScale.y = Mathf.Clamp(newScale.y, scaleMin, scaleMax);
        newScale.z = Mathf.Clamp(newScale.z, scaleMin, scaleMax);

        transform.localScale = newScale;
    }

    void LoadVolume()
    {
        if (!File.Exists(rawFilePath)) return;

        RawDatasetImporter importer = new RawDatasetImporter(
            rawFilePath, dimX, dimY, dimZ, contentFormat, endianness, bytesToSkip
        );

        VolumeDataset dataset = importer.Import();
        loadedVolumeObj = VolumeObjectFactory.CreateObject(dataset);

        GameObject rendererObj = loadedVolumeObj.transform.GetChild(0).gameObject;
        if (rendererObj.GetComponent<BoxCollider>() == null)
            rendererObj.AddComponent<BoxCollider>();

        loadedVolumeObj.transform.SetParent(transform);
        loadedVolumeObj.transform.localPosition = Vector3.zero;

        ApplyMaterialRecursive(loadedVolumeObj.gameObject);
    }

    void ApplyMaterialRecursive(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        foreach (Renderer rend in renderers)
        {
            if (sliceMaterial != null) rend.sharedMaterial = sliceMaterial;
            rend.sharedMaterial.SetVector("_PlanePos", new Vector3(-10, -10, -10));
            rend.sharedMaterial.SetVector("_PlaneNormal", Vector3.up);
        }
    }

    void TryExecuteSlice(Vector2 start, Vector2 end, Vector2 swipeVector)
    {
        if (loadedVolumeObj == null) return;

        Vector2 midPoint = (start + end) / 2f;
        Ray ray = Camera.main.ScreenPointToRay(midPoint);
        UnityEngine.RaycastHit hit;

        if (Physics.Raycast(ray, out hit))
        {
            ExecuteSliceAtPoint(hit.point, swipeVector, hit.collider.gameObject);
        }
    }

    void ExecuteSliceAtPoint(Vector3 worldHitPoint, Vector2 swipeVector, GameObject hitTarget)
    {
        GameObject originalGO = loadedVolumeObj.gameObject;
        Vector2 swipeDir = swipeVector.normalized;
        Vector2 cutDir2D = new Vector2(-swipeDir.y, swipeDir.x);

        Vector3 worldNormal = (Camera.main.transform.right * cutDir2D.x) + (Camera.main.transform.up * cutDir2D.y);
        worldNormal.Normalize();

        Vector3 localHitPos = hitTarget.transform.InverseTransformPoint(worldHitPoint);
        Vector3 textureSpacePos = localHitPos + new Vector3(0.5f, 0.5f, 0.5f);

        GameObject partA = Instantiate(originalGO, transform);
        GameObject partB = Instantiate(originalGO, transform);

        partA.name = "Volume_Side_A";
        partB.name = "Volume_Side_B";

        ApplyCutToHierarchy(partA, textureSpacePos, worldNormal, false);
        ApplyCutToHierarchy(partB, textureSpacePos, worldNormal, true);

        partA.transform.position += worldNormal * separationDistance;
        partB.transform.position -= worldNormal * separationDistance;

        originalGO.SetActive(false);
        loadedVolumeObj = null;
    }

    void ApplyCutToHierarchy(GameObject root, Vector3 texturePoint, Vector3 worldNormal, bool invertNormal)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        foreach (Renderer rend in renderers)
        {
            Vector3 localNormal = rend.transform.InverseTransformDirection(worldNormal);
            if (invertNormal) localNormal = -localNormal;

            Material mat = new Material(rend.sharedMaterial);
            mat.SetVector("_PlanePos", texturePoint);
            mat.SetVector("_PlaneNormal", localNormal);
            rend.material = mat;
        }
    }
}