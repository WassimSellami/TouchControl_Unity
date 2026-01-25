using UnityEngine;

public static class RenderUtils
{
    public static void ApplyVolumeMaterial(GameObject target, Material sourceMat, Vector3 planePos, Vector3 planeNormal)
    {
        Renderer rend = target.GetComponent<Renderer>();
        if (rend == null) rend = target.GetComponentInChildren<Renderer>();

        if (rend != null && sourceMat != null)
        {
            Material mat = new Material(sourceMat);

            Material existingMat = rend.sharedMaterial;
            if (existingMat != null)
            {
                if (existingMat.HasProperty("_DataTex")) mat.SetTexture("_DataTex", existingMat.GetTexture("_DataTex"));
                if (existingMat.HasProperty("_GradientTex")) mat.SetTexture("_GradientTex", existingMat.GetTexture("_GradientTex"));
                if (existingMat.HasProperty("_TFTex")) mat.SetTexture("_TFTex", existingMat.GetTexture("_TFTex"));
                if (existingMat.HasProperty("_MinVal")) mat.SetFloat("_MinVal", existingMat.GetFloat("_MinVal"));
                if (existingMat.HasProperty("_MaxVal")) mat.SetFloat("_MaxVal", existingMat.GetFloat("_MaxVal"));
            }

            mat.SetVector("_PlanePos", planePos);
            mat.SetVector("_PlaneNormal", planeNormal);

            rend.material = mat;
        }
    }
}