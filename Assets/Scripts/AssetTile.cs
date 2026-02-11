using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class AssetTile : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image thumbnailImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text typeText;

    public string ModelID { get; private set; }

    public void Setup(string id, string displayName, string type, Sprite icon, bool isSelected)
    {
        ModelID = id;
        if (nameText) nameText.text = displayName;
        if (typeText) typeText.text = type;
        if (thumbnailImage) thumbnailImage.sprite = icon;
    }

    public void SetVisited()
    {
        if (nameText != null)
        {
            nameText.color = new Color(0.6f, 0.2f, 1.0f);
        }
    }

    public static event System.Action<string> OnTileSelected;
    public static void TriggerSelectionEvent(string id) => OnTileSelected?.Invoke(id);
}
