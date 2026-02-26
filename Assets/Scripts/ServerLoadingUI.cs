using UnityEngine;
using TMPro;

public class ServerLoadingUI : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text statusText;

    public void Show(string modelName)
    {
        panel.SetActive(true);
        statusText.text = $"Loading: {modelName}...";
    }

    public void Hide()
    {
        panel.SetActive(false);
    }
}