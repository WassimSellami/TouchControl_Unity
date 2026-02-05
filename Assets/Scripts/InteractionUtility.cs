using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public static class InteractionUtility
{
    /// <summary>
    /// Core logic for the wiggle/shake effect.
    /// </summary>
    /// <param name="target">The transform to rotate.</param>
    /// <param name="startRotation">The original local rotation to wiggle around.</param>
    /// <param name="wiggleAxis">The axis of rotation (e.g., Vector3.up).</param>
    public static IEnumerator ShakeCoroutine(Transform target, Quaternion startRotation, Vector3 wiggleAxis)
    {
        while (target != null)
        {
            float angle = Mathf.Sin(Time.time * Constants.WIGGLE_SPEED) * Constants.WIGGLE_ANGLE;
            target.localRotation = startRotation * Quaternion.AngleAxis(angle, wiggleAxis);
            yield return null;
        }
    }
    /// <summary>
    /// Positions a UI icon at a screen point with a standardized vertical offset.
    /// </summary>
    /// <param name="icon">The Image component to position.</param>
    /// <param name="screenPoint">The base screen position (e.g. from mouse or WorldToScreenPoint).</param>
    /// <param name="uiCanvasRect">The RectTransform of the parent Canvas.</param>
    /// <param name="canvasCamera">The camera used for the canvas (null if ScreenSpaceOverlay).</param>
    public static void PositionIcon(Image icon, Vector2 screenPoint, RectTransform uiCanvasRect, Camera canvasCamera)
    {
        if (icon == null || uiCanvasRect == null) return;

        // Calculate offset based on screen height
        float offsetPx = Screen.height * Constants.ICON_VERTICAL_OFFSET_PERCENT;
        Vector2 adjustedScreenPoint = new Vector2(screenPoint.x, screenPoint.y + offsetPx);

        icon.gameObject.SetActive(true);

        // Handle different Canvas render modes
        if (icon.canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            icon.transform.position = adjustedScreenPoint;
        }
        else
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                uiCanvasRect,
                adjustedScreenPoint,
                canvasCamera,
                out Vector2 localPoint);
            icon.rectTransform.anchoredPosition = localPoint;
        }
    }
}