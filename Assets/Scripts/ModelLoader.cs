using UnityEngine;
using UnityVolumeRendering;
using System.IO;
using UnityEngine.Networking;

public static class ModelLoader
{
    public static GameObject Load(ModelData data, Transform parent, Material volMaterial)
    {
        if (data is PolygonalModelData poly) return LoadPrefab(poly, parent);
        if (data is VolumetricModelData vol) return LoadVolumetric(vol, parent, volMaterial);
        return null;
    }

    private static GameObject LoadPrefab(PolygonalModelData data, Transform parent)
    {
        return data.prefab == null ? null : Object.Instantiate(data.prefab, parent);
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
        string persistentPath = Path.Combine(Application.persistentDataPath, fileName);

#if UNITY_ANDROID && !UNITY_EDITOR
        if (!File.Exists(persistentPath))
        {
            var request = UnityWebRequest.Get(streamingPath);
            request.SendWebRequest();
            while (!request.isDone) { }
            if (request.result == UnityWebRequest.Result.Success)
                File.WriteAllBytes(persistentPath, request.downloadHandler.data);
        }
        return persistentPath;
#else
        if (File.Exists(streamingPath)) return streamingPath;
        return rawPath;
#endif
    }
}