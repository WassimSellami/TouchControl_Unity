using UnityEngine;
using System.Collections.Generic;

public class CropBoxController : MonoBehaviour
{
    [Header("Target Model")]
    public Transform targetModel;

    [Header("Visuals")]
    public float handleSize = 0.1f;
    public Material boundingBoxMaterial;
    public Material handleMaterial;

    private Vector3 boxMin = -Vector3.one * 0.5f;
    private Vector3 boxMax = Vector3.one * 0.5f;

    private List<GameObject> handles = new List<GameObject>();
    private LineRenderer lineRenderer;
    private bool visualsCreated = false;

    public enum HandleDirection { MinX, MaxX, MinY, MaxY, MinZ, MaxZ }

    void Awake()
    {
        SetupLineRenderer();
    }

    void OnEnable()
    {
        EnsureVisualsCreated();
        UpdateVisuals();
    }

    void EnsureVisualsCreated()
    {
        if (!visualsCreated)
        {
            if (targetModel == null)
            {
                Debug.LogError("CropBoxController: Target Model not assigned! Cannot create visuals.");
                enabled = false;
                return;
            }
            CreateHandles(); // Line renderer is already setup in Awake
            visualsCreated = true;
        }
    }


    void SetupLineRenderer()
    {
        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null) lineRenderer = gameObject.AddComponent<LineRenderer>();
        if (boundingBoxMaterial == null)
        {
            Shader unlitShader = Shader.Find("Unlit/Color");
            if (unlitShader) boundingBoxMaterial = new Material(unlitShader) { color = Color.yellow };
            else boundingBoxMaterial = new Material(Shader.Find("Legacy Shaders/Diffuse")) { color = Color.yellow };
        }
        lineRenderer.material = boundingBoxMaterial;
        lineRenderer.startWidth = 0.01f;
        lineRenderer.endWidth = 0.01f;
        lineRenderer.positionCount = 24;
        lineRenderer.useWorldSpace = false;
    }

    void CreateHandles()
    {
        if (handles.Count > 0) return; // Already created

        if (handleMaterial == null)
        {
            Shader unlitShader = Shader.Find("Unlit/Color");
            if (unlitShader) handleMaterial = new Material(unlitShader) { color = Color.cyan };
            else handleMaterial = new Material(Shader.Find("Legacy Shaders/Diffuse")) { color = Color.cyan };
        }

        for (int i = 0; i < 6; i++)
        {
            GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            handle.name = "CropHandle_" + ((HandleDirection)i).ToString();
            handle.transform.SetParent(this.transform);
            handle.transform.localScale = Vector3.one * handleSize;
            Renderer rend = handle.GetComponent<Renderer>();
            if (rend != null) rend.material = handleMaterial;

            SphereCollider sc = handle.GetComponent<SphereCollider>();
            if (sc == null) sc = handle.AddComponent<SphereCollider>();
            sc.radius = 0.5f; // Default sphere primitive radius

            HandleIdentifier id = handle.AddComponent<HandleIdentifier>();
            id.direction = (HandleDirection)i;
            handles.Add(handle);
        }
    }

    public void SetBounds(Vector3 newMin, Vector3 newMax)
    {
        boxMin = Vector3.Min(newMin, newMax); // Ensure min is actually min
        boxMax = Vector3.Max(newMin, newMax); // Ensure max is actually max
        UpdateVisuals();
    }

    public Vector3 GetMinBounds() { return boxMin; }
    public Vector3 GetMaxBounds() { return boxMax; }

    public void UpdateVisuals()
    {
        if (targetModel == null || !targetModel.gameObject.activeInHierarchy || lineRenderer == null || handles.Count < 6)
        {
            if (targetModel != null && targetModel.gameObject.activeInHierarchy && handles.Count < 6 && visualsCreated)
            {
                Debug.LogWarning("CropBoxController: Handles were missing, recreating.");
                CreateHandles(); // Attempt to recreate if visuals were supposed to be created but handles are missing
            }
            else if (!visualsCreated && targetModel != null && targetModel.gameObject.activeInHierarchy)
            {
                EnsureVisualsCreated(); // Handles might not have been created if OnEnable was called before targetModel was ready
            }
            else return;
        }
        if (handles.Count < 6) return; // Still not ready

        this.transform.position = targetModel.position;
        this.transform.rotation = targetModel.rotation;

        Vector3 size = boxMax - boxMin;

        handles[(int)HandleDirection.MinX].transform.localPosition = new Vector3(boxMin.x, boxMin.y + size.y / 2, boxMin.z + size.z / 2);
        handles[(int)HandleDirection.MaxX].transform.localPosition = new Vector3(boxMax.x, boxMin.y + size.y / 2, boxMin.z + size.z / 2);
        handles[(int)HandleDirection.MinY].transform.localPosition = new Vector3(boxMin.x + size.x / 2, boxMin.y, boxMin.z + size.z / 2);
        handles[(int)HandleDirection.MaxY].transform.localPosition = new Vector3(boxMin.x + size.x / 2, boxMax.y, boxMin.z + size.z / 2);
        handles[(int)HandleDirection.MinZ].transform.localPosition = new Vector3(boxMin.x + size.x / 2, boxMin.y + size.y / 2, boxMin.z);
        handles[(int)HandleDirection.MaxZ].transform.localPosition = new Vector3(boxMin.x + size.x / 2, boxMin.y + size.y / 2, boxMax.z);

        Vector3 p0 = boxMin; Vector3 p1 = new Vector3(boxMax.x, boxMin.y, boxMin.z);
        Vector3 p2 = new Vector3(boxMax.x, boxMax.y, boxMin.z); Vector3 p3 = new Vector3(boxMin.x, boxMax.y, boxMin.z);
        Vector3 p4 = new Vector3(boxMin.x, boxMin.y, boxMax.z); Vector3 p5 = new Vector3(boxMax.x, boxMin.y, boxMax.z);
        Vector3 p6 = boxMax; Vector3 p7 = new Vector3(boxMin.x, boxMax.y, boxMax.z);

        lineRenderer.SetPositions(new Vector3[] {
            p0, p1, p1, p2, p2, p3, p3, p0, p4, p5, p5, p6, p6, p7, p7, p4, p0, p4, p1, p5, p2, p6, p3, p7
        });
    }

    public void AdjustBound(HandleDirection direction, float worldValue)
    {
        Vector3 localValuePoint = transform.InverseTransformPoint(targetModel.TransformPoint(new Vector3(worldValue, worldValue, worldValue))); // Simplified, need axis specific
        // This method needs to be smarter: convert world drag point to local model space, then update specific bound.
        // For now, this is a placeholder. InputManager will directly modify boxMin/boxMax.
    }


    void LateUpdate()
    {
        if (targetModel != null && gameObject.activeInHierarchy && targetModel.gameObject.activeInHierarchy)
        {
            this.transform.position = targetModel.position;
            this.transform.rotation = targetModel.rotation;
        }
    }
}