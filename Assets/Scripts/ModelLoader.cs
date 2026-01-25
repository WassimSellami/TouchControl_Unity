using UnityEngine;
using UnityVolumeRendering;
using UnityEngine.Networking;
using System.IO;

public static class ModelLoader
{
    public static GameObject Load(ModelData data, Transform parent)
    {
        if (data is VolumetricModelData vol) return LoadVolumetric(vol, parent);
        if (data is PolygonalModelData poly) return LoadPrefab(poly, parent);
        return null;
    }

    private static GameObject LoadPrefab(PolygonalModelData data, Transform parent)
    {
        return data.prefab == null ? null : Object.Instantiate(data.prefab, parent);
    }

    private static GameObject LoadVolumetric(VolumetricModelData data, Transform parent)
    {
        string filePath = data.rawFilePath;

        if (Application.platform == RuntimePlatform.Android)
        {
            string fileName = Path.GetFileName(data.rawFilePath);
            string persistentPath = Path.Combine(Application.persistentDataPath, fileName);

            if (!File.Exists(persistentPath))
            {
                string sourcePath = Application.streamingAssetsPath + "/" + fileName;
                UnityWebRequest request = UnityWebRequest.Get(sourcePath);
                var operation = request.SendWebRequest();
                while (!operation.isDone) { }

                if (request.result == UnityWebRequest.Result.Success)
                {
                    File.WriteAllBytes(persistentPath, request.downloadHandler.data);
                }
                else
                {
                    Debug.LogError($"[ModelLoader] Failed to extract volumetric data on Android: {request.error}");
                    return null;
                }
            }
            filePath = persistentPath;
        }
        else
        {
            if (!File.Exists(filePath))
            {
                string fileName = Path.GetFileName(filePath);
                string streamingPath = Path.Combine(Application.streamingAssetsPath, fileName);

                if (File.Exists(streamingPath))
                {
                    filePath = streamingPath;
                }
                else
                {
                    Debug.LogError($"[ModelLoader] File not found at '{data.rawFilePath}' OR '{streamingPath}'");
                    return null;
                }
            }
        }

        var importer = new RawDatasetImporter(
            filePath, data.dimX, data.dimY, data.dimZ,
            data.contentFormat, data.endianness, data.bytesToSkip
        );

        VolumeDataset dataset = importer.Import();
        if (dataset == null)
        {
            Debug.LogError($"[ModelLoader] Failed to import dataset from: {filePath}");
            return null;
        }

        VolumeRenderedObject volObj = VolumeObjectFactory.CreateObject(dataset);
        volObj.gameObject.transform.SetParent(parent, false);
        return volObj.gameObject;
    }
}