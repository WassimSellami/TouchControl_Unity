using UnityEngine;

[CreateAssetMenu(fileName = "New Polygonal Model", menuName = "Models/Polygonal Model")]
public class PolygonalModelData : ModelData
{
    [Header("Runtime Loading (Priority)")]
    public string modelFilePath;

    [Header("Editor/Prefab Loading")]
    public GameObject prefab;
}