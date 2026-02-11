using UnityEngine;
using UnityEngine.UI;

public class DragProxyManager : MonoBehaviour
{
    [Header("Assign in Inspector")]
    public Image proxyImage;
    public RectTransform proxyRect;

    private Canvas parentCanvas;

    void Awake()
    {
        parentCanvas = GetComponent<Canvas>();
        if (proxyImage != null) proxyImage.gameObject.SetActive(false);
    }

    public void StartDrag(Sprite icon)
    {
        if (proxyImage == null) return;

        proxyImage.sprite = icon;
        proxyImage.gameObject.SetActive(true);
        proxyImage.transform.SetAsLastSibling();
    }

    public void UpdateDragPosition(Vector2 screenPosition)
    {
        if (proxyImage == null || parentCanvas == null) return;

        Vector2 localPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentCanvas.transform as RectTransform,
            screenPosition,
            parentCanvas.worldCamera,
            out localPos);

        proxyRect.anchoredPosition = localPos;
    }

    public void EndDrag()
    {
        if (proxyImage != null) proxyImage.gameObject.SetActive(false);
    }
}
