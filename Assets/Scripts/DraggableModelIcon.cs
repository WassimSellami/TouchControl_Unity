using UnityEngine;
using UnityEngine.EventSystems;
using System;

public class DraggableModelIcon : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public string ModelID;
    public Action<string> OnModelDropped;

    private Vector3 originalPosition;
    private RectTransform rectTransform;
    private Canvas canvas;
    private CanvasGroup canvasGroup;
    private Vector2 velocity;
    private Vector2 lastPos;
    private bool isGliding = false;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    void Update()
    {
        if (isGliding)
        {
            transform.position += (Vector3)velocity * Time.deltaTime;
            velocity = Vector2.Lerp(velocity, Vector2.zero, Constants.MODEL_THUMBNAIL_GLIDE_FRICTION * Time.deltaTime);

            if (!IsOnScreen() || velocity.sqrMagnitude < Constants.MODEL_THUMBNAIL_RESET_VELOCITY)
            {
                if (!IsOnScreen()) OnModelDropped?.Invoke(ModelID);
                ResetPosition();
            }
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        isGliding = false;
        originalPosition = transform.position;
        lastPos = eventData.position;
        canvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (canvas == null) return;
        rectTransform.anchoredPosition += eventData.delta / canvas.scaleFactor;
        velocity = (eventData.position - lastPos) / Time.deltaTime;
        lastPos = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = true;
        isGliding = true;
        velocity = Vector2.ClampMagnitude(velocity, Constants.MODEL_THUMBNAIL_MAX_VELOCITY);
    }

    private void ResetPosition()
    {
        isGliding = false;
        transform.position = originalPosition;
        velocity = Vector2.zero;
    }

    private bool IsOnScreen()
    {
        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);
        return !(corners[2].x < 0 || corners[0].x > Screen.width || corners[1].y < 0 || corners[0].y > Screen.height);
    }
}