using UnityEngine;
using UnityVolumeRendering;

public abstract class ModelData : ScriptableObject
{
    public string modelID;
    public string displayName;
    [TextArea] public string description;
    public Sprite thumbnail;
}

[CreateAssetMenu(fileName = "New Volumetric Model", menuName = "Models/Volumetric Model")]
public class VolumetricModelData : ModelData
{
    public string rawFilePath;
    public int dimX = 128;
    public int dimY = 256;
    public int dimZ = 256;
    public DataContentFormat contentFormat = DataContentFormat.Uint8;
    public Endianness endianness = Endianness.LittleEndian;
    public int bytesToSkip = 0;
}

[CreateAssetMenu(fileName = "New Polygonal Model", menuName = "Models/Polygonal Model")]
public class PolygonalModelData : ModelData
{
    public GameObject prefab;
}
