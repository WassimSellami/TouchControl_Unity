using UnityEngine;
using UnityVolumeRendering;


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