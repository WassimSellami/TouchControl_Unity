using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public static class InteractionUtility
{
    public static IEnumerator ShakeCoroutine(Transform target, Quaternion startRotation, Vector3 wiggleAxis)
    {
        while (target != null)
        {
            float angle = Mathf.Sin(Time.time * Constants.WIGGLE_SPEED) * Constants.WIGGLE_ANGLE;
            target.localRotation = startRotation * Quaternion.AngleAxis(angle, wiggleAxis);
            yield return null;
        }
    }
    public static void PositionIcon(Image icon, Vector2 screenPoint, RectTransform uiCanvasRect, Camera canvasCamera, bool applyOffset = true)
    {
        if (icon == null || uiCanvasRect == null)
            return;

        float offsetPx = applyOffset ? (Screen.height * Constants.ICON_VERTICAL_OFFSET_PERCENT) : 0f;
        Vector2 adjustedScreenPoint = new Vector2(screenPoint.x, screenPoint.y + offsetPx);

        icon.gameObject.SetActive(true);

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