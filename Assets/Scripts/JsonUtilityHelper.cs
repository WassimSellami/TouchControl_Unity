using UnityEngine;

public static class JsonUtilityHelper
{
    [System.Serializable]
    public class ModelTransformStateData
    {
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;
    }
    private struct Vector3Wrapper { public float x; public float y; public float z; }
    [System.Serializable]
    private struct QuaternionWrapper { public float x; public float y; public float z; public float w; }

    public static string ToJson(Vector3 vector)
    {
        Vector3Wrapper wrapper = new Vector3Wrapper { x = vector.x, y = vector.y, z = vector.z };
        return JsonUtility.ToJson(wrapper);
    }

    public static Vector3 FromJsonVector3(string json)
    {
        Vector3Wrapper wrapper = JsonUtility.FromJson<Vector3Wrapper>(json);
        return new Vector3(wrapper.x, wrapper.y, wrapper.z);
    }

    public static string ToJson(Quaternion quaternion)
    {
        QuaternionWrapper wrapper = new QuaternionWrapper { x = quaternion.x, y = quaternion.y, z = quaternion.z, w = quaternion.w };
        return JsonUtility.ToJson(wrapper);
    }

    public static Quaternion FromJsonQuaternion(string json)
    {
        QuaternionWrapper wrapper = JsonUtility.FromJson<QuaternionWrapper>(json);
        return new Quaternion(wrapper.x, wrapper.y, wrapper.z, wrapper.w);
    }
}

[System.Serializable]
public class CameraStateData
{
    public Vector3 position;
    public Quaternion rotation;
    public float orthoSize;
}