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

        if (!string.IsNullOrEmpty(data.modelFilePath) && File.Exists(data.modelFilePath))
        {
            string ext = Path.GetExtension(data.modelFilePath).ToLower();
            if (ext == ".obj")
            {
                loadedObject = ObjLoader.Load(data.modelFilePath);
            }
        }

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

        VolumeDataset dataset = null;
        IImageFileImporter nativeImporter = null;

        string ext = Path.GetExtension(filePath).ToLower();
        if (ext == ".nii" || filePath.ToLower().EndsWith(".nii.gz")) nativeImporter = ImporterFactory.CreateImageFileImporter(ImageFileFormat.NIFTI);
        else if (ext == ".nrrd" || ext == ".nhdr") nativeImporter = ImporterFactory.CreateImageFileImporter(ImageFileFormat.NRRD);

        if (nativeImporter != null)
        {
            dataset = nativeImporter.Import(filePath);
        }
        else
        {
            RawDatasetImporter rawImporter = new RawDatasetImporter(
                filePath, data.dimX, data.dimY, data.dimZ, data.contentFormat, data.endianness, data.bytesToSkip
            );
            dataset = rawImporter.Import();
        }

        if (dataset == null) return null;

        VolumeRenderedObject volObj = VolumeObjectFactory.CreateObject(dataset);
        volObj.transform.SetParent(parent, false);
        volObj.transform.localPosition = Vector3.zero;

        GameObject rendererObj = volObj.transform.GetChild(0).gameObject;
        Renderer rend = rendererObj.GetComponent<Renderer>();

        if (rend != null)
        {
            Vector3 rawSize = rend.bounds.size;
            float maxDim = Mathf.Max(rawSize.x, rawSize.y, rawSize.z);
            if (maxDim > 0f)
            {
                volObj.transform.localScale = Vector3.one * (1.0f / maxDim);
            }
        }

        if (volMaterial != null && rend != null)
        {
            Texture generated3DTexture = rend.material.GetTexture("_DataTex");
            rend.material = new Material(volMaterial);

            if (generated3DTexture != null)
            {
                rend.material.SetTexture("_DataTex", generated3DTexture);
            }

            rend.material.SetVector("_PlanePos", new Vector3(-10, -10, -10));
            rend.material.SetVector("_PlaneNormal", Vector3.up);
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