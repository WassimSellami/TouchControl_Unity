using UnityEngine;

public class CAVE2DisplayTest : MonoBehaviour
{
    [SerializeField] private Camera frontCamera;
    [SerializeField] private Camera rightCamera;
    [SerializeField] private Camera leftCamera;
    [SerializeField] private Transform rigOrigin;

    private void Start()
    {
        //for (int i = 0; i < Display.displays.Length; i++)
        //{
        //    if (i < 3)
        //    {
        //        Display.displays[i].Activate();
        //    }
        //}

        if (frontCamera != null)
        {
            frontCamera.targetDisplay = 0;
            if (rigOrigin != null)
            {
                frontCamera.transform.SetParent(rigOrigin, false);
                frontCamera.transform.localPosition = Vector3.zero;
            }
            frontCamera.transform.localRotation = Quaternion.Euler(0, 0f, 0);

            frontCamera.fieldOfView = Camera.HorizontalToVerticalFieldOfView(90f, frontCamera.aspect);
        }

        if (rightCamera != null)
        {
            rightCamera.targetDisplay = 1;
            if (rigOrigin != null)
            {
                rightCamera.transform.SetParent(rigOrigin, false);
                rightCamera.transform.localPosition = Vector3.zero;
            }
            rightCamera.transform.localRotation = Quaternion.Euler(0, 90f, 0);

            rightCamera.fieldOfView = Camera.HorizontalToVerticalFieldOfView(90f, rightCamera.aspect);
        }

        if (leftCamera != null)
        {
            leftCamera.targetDisplay = 1;
            if (rigOrigin != null)
            {
                leftCamera.transform.SetParent(rigOrigin, false);
                leftCamera.transform.localPosition = Vector3.zero;
            }
            leftCamera.transform.localRotation = Quaternion.Euler(0, -90f, 0);

            leftCamera.fieldOfView = Camera.HorizontalToVerticalFieldOfView(90f, leftCamera.aspect);
        }
    }
}