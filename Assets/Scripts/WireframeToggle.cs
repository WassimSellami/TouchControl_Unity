using UnityEngine;

[RequireComponent(typeof(Camera))]
public class WireframeMode : MonoBehaviour
{
    void OnPreRender()
    {
        GL.wireframe = true;
    }

    void OnPostRender()
    {
        GL.wireframe = false;
    }
}
