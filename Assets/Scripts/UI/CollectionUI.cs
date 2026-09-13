using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls the mineral collection in the Laboratory
/// </summary>
[DisallowMultipleComponent]
public class CollectionUI : MonoBehaviour
{
    [Header("Header")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private string defaultTitle = "Collecion";

    [Header("Grid View")]
    [SerializeField] private RectTransform gridView;
    [SerializeField] private Transform gridContainer;
    [SerializeField] private GameObject gridItemPrefab;
    [SerializeField] private TMP_Text progressCountText;

    [Header("Detail View")]
    [SerializeField] private RectTransform detailView;
    [SerializeField] private TMP_Text detailMineralName;
    [SerializeField] private TMP_Text detailMineralDescription;
    [SerializeField] private Image detailMineralIcon;
    [SerializeField] private Button spawnMineralButton;
    [SerializeField] private Button backButton;
    [SerializeField] private ScrollRect detailDescriptionScrollRect;

    [Header("Spawner")]
    [Tooltip("Transform of the MineralSpawner object where display minerals are spawned.")]
    [SerializeField] private Transform mineralSpawner;

    // Runtime state
    private readonly List<MineralGridItemUI> spawnedItems = new List<MineralGridItemUI>();
    private readonly Dictionary<string, Sprite> iconCache = new Dictionary<string, Sprite>();
    private Mineral currentSelectedMineral;

    private void Awake()
    {
        if (titleText != null && string.IsNullOrEmpty(titleText.text))
        {
            titleText.text = defaultTitle;
        }

        if (backButton != null)
        {
            backButton.onClick.AddListener(ShowGridView);
        }

        if (spawnMineralButton != null)
        {
            spawnMineralButton.onClick.AddListener(OnSpawnMineralClicked);
        }

        if (detailDescriptionScrollRect == null && detailMineralDescription != null)
        {
            detailDescriptionScrollRect = detailMineralDescription.GetComponentInParent<ScrollRect>();
        }
    }

    private void Start()
    {
        ShowGridView();

        if (CollectionManager.Instance != null)
        {
            CollectionManager.Instance.OnCollectionChanged += RefreshGrid;
        }

        RefreshGrid();
    }

    private void OnDestroy()
    {
        if (CollectionManager.Instance != null)
        {
            CollectionManager.Instance.OnCollectionChanged -= RefreshGrid;
        }
    }

    public void RefreshGrid()
    {
        List<Mineral> minerals = CollectionManager.Instance != null 
            ? CollectionManager.Instance.GetAllMinerals() 
            : new List<Mineral>(Resources.LoadAll<Mineral>("Minerals"));

        if (minerals == null) return;

        // Spawn or reuse items
        for (int i = 0; i < minerals.Count; i++)
        {
            if (i >= spawnedItems.Count)
            {
                if (gridItemPrefab == null || gridContainer == null) break;
                GameObject itemObj = Instantiate(gridItemPrefab, gridContainer);
                itemObj.name = $"GridSlot_{i}";
                MineralGridItemUI ui = itemObj.GetComponent<MineralGridItemUI>() ?? itemObj.AddComponent<MineralGridItemUI>();
                spawnedItems.Add(ui);
            }

            Mineral mineral = minerals[i];
            bool isDiscovered = CollectionManager.Instance != null && CollectionManager.Instance.IsDiscovered(mineral.Id);
            Sprite icon = isDiscovered ? GetMineralIcon(mineral) : null;

            spawnedItems[i].gameObject.SetActive(true);
            spawnedItems[i].Setup(mineral, isDiscovered, icon, ShowDetailView);
        }

        // Hide any surplus slots
        for (int i = minerals.Count; i < spawnedItems.Count; i++)
        {
            spawnedItems[i].gameObject.SetActive(false);
        }

        UpdateProgressText();
    }

    public void UpdateProgressText()
    {
        if (progressCountText == null) return;

        int discovered = CollectionManager.Instance != null ? CollectionManager.Instance.DiscoveredCount : 0;
        int total = CollectionManager.Instance != null ? CollectionManager.Instance.TotalMineralsCount : 0;

        progressCountText.text = $"Discovered: {discovered}/{total}";
    }

    public void ShowGridView()
    {
        if (gridView != null) gridView.gameObject.SetActive(true);
        if (detailView != null) detailView.gameObject.SetActive(false);
        currentSelectedMineral = null;
    }

    public void ShowDetailView(Mineral mineral)
    {
        if (mineral == null) return;
        currentSelectedMineral = mineral;

        if (gridView != null) gridView.gameObject.SetActive(false);
        if (detailView != null) detailView.gameObject.SetActive(true);

        if (detailMineralName != null)
        {
            detailMineralName.text = mineral.MineralName;
        }

        if (detailMineralDescription != null)
        {
            detailMineralDescription.text = string.IsNullOrEmpty(mineral.Description)
                ? "No mineral description available."
                : mineral.Description;
        }

        if (detailMineralIcon != null)
        {
            Sprite icon = GetMineralIcon(mineral);
            detailMineralIcon.sprite = icon;
            detailMineralIcon.gameObject.SetActive(icon != null);
        }

        ResetDescriptionScroll();
    }

    public void ResetDescriptionScroll()
    {
        if (detailDescriptionScrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            detailDescriptionScrollRect.verticalNormalizedPosition = 1f;
        }
    }

    public Sprite GetMineralIcon(Mineral mineral)
    {
        if (mineral == null) return null;

        string key = mineral.MineralName;
        if (iconCache.TryGetValue(key, out Sprite cached) && cached != null)
        {
            return cached;
        }

        Sprite sprite = Resources.Load<Sprite>($"Icons/{key}") ?? Resources.Load<Sprite>($"Icons/{mineral.name}");
        if (sprite != null)
        {
            iconCache[key] = sprite;
        }
        return sprite;
    }

    public void OnSpawnMineralClicked()
    {
        if (currentSelectedMineral == null || mineralSpawner == null) return;

        CollectionManager.Instance.SpawnDisplayMineral(currentSelectedMineral.Id, mineralSpawner);
    }
}
