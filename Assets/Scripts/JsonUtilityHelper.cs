using UnityEngine;

[System.Serializable]
public class ModelTransformStateData
{
    public Vector3 localPosition;
    public Quaternion localRotation;
    public Vector3 localScale;
}

[System.Serializable]
public class ModelBoundsSizeData
{
    public Vector3 size;
}

[System.Serializable]
public class ClientCameraStateData
{
    public Vector3 position;
    public Quaternion rotation;
}

[System.Serializable]
public class VisualCropPlaneData
{
    public Vector3 position;
    public Vector3 normal;
    public float scale;
}

[System.Serializable]
public class LineData
{
    public Vector3 start;
    public Vector3 end;
}

[System.Serializable]
public class SliceActionData
{
    public string actionID;
    public Vector3 planePoint;
    public Vector3 planeNormal;
    public float separationFactor;
    public string[] targetPartIDs;
}

[System.Serializable]
public class DestroyActionData
{
    public string actionID;
    public string targetPartID;
    public Vector3 worldPosition;
}

[System.Serializable]
public class ShowSliceIconData
{
    public Vector3 worldPosition;
}