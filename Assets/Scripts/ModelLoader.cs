using UnityEngine;
using UnityVolumeRendering;
using System.IO;

public static class ModelLoader
{
    public static GameObject Load(ModelData data, Transform parent, Material volMaterial)
    {
        if (data is PolygonalModelData poly) return LoadPolygonal(poly, parent);
        if (data is VolumetricModelData vol) return LoadVolumetric(vol, parent, volMaterial);
        return null;
    }

    private static GameObject LoadPolygonal(PolygonalModelData data, Transform parent)
    {
        GameObject loadedObject = null;

        // Priority 1: Runtime File Path (OBJ)
        if (!string.IsNullOrEmpty(data.modelFilePath) && File.Exists(data.modelFilePath))
        {
            string ext = Path.GetExtension(data.modelFilePath).ToLower();
            if (ext == ".obj")
            {
                loadedObject = ObjLoader.Load(data.modelFilePath);
            }
            else
            {
                Debug.LogWarning("Runtime loading only supports .obj for now.");
            }
        }

        // Priority 2: Prefab (Editor assigned)
        if (loadedObject == null && data.prefab != null)
        {
            loadedObject = Object.Instantiate(data.prefab);
        }

        if (loadedObject != null)
        {
            loadedObject.transform.SetParent(parent, false);
            return loadedObject;
        }

        return null;
    }

    private static GameObject LoadVolumetric(VolumetricModelData data, Transform parent, Material volMaterial)
    {
        string filePath = ResolvePath(data.rawFilePath);

        if (!File.Exists(filePath))
        {
            Debug.LogError($"[ModelLoader] Volume file NOT found: {filePath}");
            return null;
        }

        RawDatasetImporter importer = new RawDatasetImporter(
            filePath, data.dimX, data.dimY, data.dimZ, data.contentFormat, data.endianness, data.bytesToSkip
        );

        VolumeDataset dataset = importer.Import();
        if (dataset == null) return null;

        VolumeRenderedObject volObj = VolumeObjectFactory.CreateObject(dataset);
        volObj.transform.SetParent(parent, false);
        volObj.transform.localPosition = Vector3.zero;

        GameObject rendererObj = volObj.transform.GetChild(0).gameObject;

        if (volMaterial != null)
        {
            Renderer rend = rendererObj.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.material = new Material(volMaterial);
                rend.material.SetVector("_PlanePos", new Vector3(-10, -10, -10));
                rend.material.SetVector("_PlaneNormal", Vector3.up);
            }
        }

        if (rendererObj.GetComponent<BoxCollider>() == null)
            rendererObj.AddComponent<BoxCollider>();

        return volObj.gameObject;
    }

    private static string ResolvePath(string rawPath)
    {
        if (Path.IsPathRooted(rawPath) && File.Exists(rawPath)) return rawPath;
        string fileName = Path.GetFileName(rawPath);
        string streamingPath = Path.Combine(Application.streamingAssetsPath, fileName);
        if (File.Exists(streamingPath)) return streamingPath;
        return rawPath;
    }
}