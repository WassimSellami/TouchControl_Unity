using System;
using UnityEngine;
public abstract class ModelData : ScriptableObject
{
    public string modelID;
    public string displayName;
    [TextArea] public string description;
    public Sprite thumbnail;
    public Vector3 boundsSize = Vector3.one;
    public string fileSize = "0 MB";
}
[Serializable]
public class ModelMetadata
{
    public string modelID;
    public string displayName;
    public string description;
    public string thumbnailBase64;
    public string modelType;
    public string fileSize;
}
[Serializable]
public class ModelMetadataList
{
    public ModelMetadata[] models;
}
