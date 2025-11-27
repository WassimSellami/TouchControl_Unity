using UnityEngine;

public abstract class ModelData : ScriptableObject
{
    public string modelID;
    public string displayName;
    [TextArea] public string description;
    public Sprite thumbnail;
    public Vector3 boundsSize = Vector3.one;
}


