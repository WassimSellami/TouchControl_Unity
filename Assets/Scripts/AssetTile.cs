using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems; // Required for detecting clicks
using System;

public class AssetTile : MonoBehaviour, IPointerClickHandler
{
    [Header("UI References")]
    [SerializeField] private Image thumbnailImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text typeText;
    [SerializeField] private TMP_Text sizeText;

    public string ModelID { get; private set; }
    private bool isAddButton = false;

    // Static events for the System to listen to
    public static event Action<string, Vector2> OnRightClicked; // For Delete popup
    public static event Action OnAddTileClicked;               // For Plus Icon

    // This is the specific one the error was complaining about:
    public static event Action<string> OnTileSelected;
    public static void TriggerSelectionEvent(string id) => OnTileSelected?.Invoke(id);

    public void Setup(string id, string displayName, string type, string size, Sprite icon, bool isSelected)
    {
        ModelID = id;
        isAddButton = false;
        if (nameText) nameText.text = displayName;
        if (typeText) typeText.text = type;
        if (sizeText) sizeText.text = size;
        if (thumbnailImage) thumbnailImage.sprite = icon;
    }

    public void SetupAsAddButton(Sprite plusIcon)
    {
        ModelID = "ADD_NEW";
        isAddButton = true;
        if (nameText) nameText.text = "Add New Model";
        if (typeText) typeText.text = "Action";
        if (sizeText) sizeText.text = "";
        if (thumbnailImage) thumbnailImage.sprite = plusIcon;
    }

    // This function runs whenever you click the tile (Left or Right)
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            // Only allow right-click delete on actual models, not the "+" tile
            if (!isAddButton)
                OnRightClicked?.Invoke(ModelID, eventData.position);
        }
        else if (eventData.button == PointerEventData.InputButton.Left)
        {
            if (isAddButton)
            {
                OnAddTileClicked?.Invoke();
            }
            else
            {
                // When left-clicked, trigger the selection event
                TriggerSelectionEvent(ModelID);
            }
        }
    }

    public void SetVisited()
    {
        if (nameText != null) nameText.color = new Color(0.6f, 0.2f, 1.0f);
    }
}