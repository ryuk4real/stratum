using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Represents a single clickable mineral slot in the collection grid.
/// Shows "?" if undiscovered, or the mineral PNG icon if discovered.
/// </summary>
[RequireComponent(typeof(Button))]
public class MineralGridItemUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button button;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text questionMarkText;

    private Mineral mineral;
    private bool isDiscovered;
    private Action<Mineral> onDiscoveredClicked;

    private void Awake()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        button.onClick.AddListener(HandleClick);
    }

    // Configures the grid item for a specific mineral and discovery state.
    public void Setup(Mineral mineral, bool isDiscovered, Sprite iconSprite, Action<Mineral> onDiscoveredClicked)
    {
        this.mineral = mineral;
        this.isDiscovered = isDiscovered;
        this.onDiscoveredClicked = onDiscoveredClicked;

        if (isDiscovered)
        {
            if (iconImage != null)
            {
                iconImage.sprite = iconSprite;
                iconImage.gameObject.SetActive(iconSprite != null);
            }

            if (questionMarkText != null)
            {
                questionMarkText.gameObject.SetActive(false);
            }
        }
        else
        {
            if (iconImage != null)
            {
                iconImage.gameObject.SetActive(false);
            }

            if (questionMarkText != null)
            {
                questionMarkText.gameObject.SetActive(true);
                questionMarkText.text = "?";
            }
        }
    }

    private void HandleClick()
    {
        if (!isDiscovered || mineral == null)
        {
            // Undiscovered mineral so do nothing
            return;
        }

        onDiscoveredClicked?.Invoke(mineral);
    }
}
