using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages the player's mineral collection.
/// </summary>
[DisallowMultipleComponent]
public class CollectionManager : MonoBehaviour, IManager
{
    public static CollectionManager Instance { get; private set; }

    [Header("Settings")]
    [Tooltip("If true, automatically unlocks all minerals on Start (for testing only).")]
    [SerializeField] private bool discoverAllOnStart = false;

    [Tooltip("Available minerals database. Populated in inspector or loaded from Resources/Minerals.")]
    [SerializeField] private List<Mineral> availableMinerals = new List<Mineral>();

    [Header("Display / Spawning")]
    [Tooltip("Default prefab to instantiate for minerals on display. If null, loads from Resources/Prefabs/Mineral.")]
    [SerializeField] private GameObject mineralPrefab;

    [Header("Collection State")]
    [Tooltip("List of all collection entries tracked by the manager.")]
    [SerializeField] private List<MineralCollectionEntry> collectionEntries = new List<MineralCollectionEntry>();

    // Fast lookup dictionary
    private readonly Dictionary<uint, MineralCollectionEntry> entriesById = new Dictionary<uint, MineralCollectionEntry>();

    public bool IsInitialized { get; private set; }

    // Events
    public event Action OnCollectionChanged;

    public int TotalMineralsCount
    {
        get
        {
            if (!IsInitialized) Initialize();
            return collectionEntries.Count;
        }
    }

    public int DiscoveredCount
    {
        get
        {
            if (!IsInitialized) Initialize();
            int count = 0;
            for (int i = 0; i < collectionEntries.Count; i++)
            {
                if (collectionEntries[i].IsDiscovered) count++;
            }
            return count;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (!IsInitialized)
        {
            Initialize();
        }

        if (discoverAllOnStart)
        {
            DiscoverAll();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // Initializes the manager, loading mineral assets and populating collection data.
    public void Initialize()
    {
        if (IsInitialized) return;

        LoadMineralsDatabase();
        RebuildLookupTables();

        IsInitialized = true;
        Debug.Log($"[CollectionManager] Initialized with {collectionEntries.Count} minerals ({DiscoveredCount} discovered).");
        OnCollectionChanged?.Invoke();
    }

    public void Free()
    {
        entriesById.Clear();
        IsInitialized = false;
    }

    private void LoadMineralsDatabase()
    {
        if (availableMinerals == null || availableMinerals.Count == 0)
        {
            Mineral[] loaded = Resources.LoadAll<Mineral>("Minerals");
            if (loaded == null || loaded.Length == 0)
                loaded = Resources.LoadAll<Mineral>("");

            if (loaded != null && loaded.Length > 0)
                availableMinerals = new List<Mineral>(loaded);
        }

        if (availableMinerals != null)
        {
            availableMinerals.Sort((a, b) =>
            {
                if (a == null && b == null) return 0;
                if (a == null) return 1;
                if (b == null) return -1;
                return a.Id.CompareTo(b.Id);
            });
        }

        // Preserve any existing entry states by mineral ID
        var existingMap = new Dictionary<uint, MineralCollectionEntry>();
        for (int i = 0; i < collectionEntries.Count; i++)
        {
            var entry = collectionEntries[i];
            if (entry != null && entry.Mineral != null && !existingMap.ContainsKey(entry.Mineral.Id))
            {
                existingMap.Add(entry.Mineral.Id, entry);
            }
        }

        collectionEntries.Clear();
        if (availableMinerals != null)
        {
            for (int i = 0; i < availableMinerals.Count; i++)
            {
                Mineral m = availableMinerals[i];
                if (m == null) continue;

                if (existingMap.TryGetValue(m.Id, out var existingEntry))
                    collectionEntries.Add(existingEntry);
                else
                    collectionEntries.Add(new MineralCollectionEntry(m));
            }
        }
    }

    private void RebuildLookupTables()
    {
        entriesById.Clear();

        for (int i = 0; i < collectionEntries.Count; i++)
        {
            var entry = collectionEntries[i];
            if (entry == null || entry.Mineral == null) continue;

            entriesById[entry.Mineral.Id] = entry;
        }
    }

    // Collects a mineral ScriptableObject reference
    public bool CollectMineral(Mineral mineral)
    {
        if (mineral == null) return false;

        if (!IsInitialized) Initialize();

        if (!entriesById.TryGetValue(mineral.Id, out var entry))
        {
            entry = new MineralCollectionEntry(mineral);
            collectionEntries.Add(entry);
            entriesById[mineral.Id] = entry;
        }

        bool wasAlreadyDiscovered = entry.IsDiscovered;
        entry.MarkDiscovered(Time.time);
        entry.IncrementCollected();

        if (!wasAlreadyDiscovered)
        {
            Debug.Log($"[CollectionManager] NEW MINERAL DISCOVERED: {mineral.MineralName} (ID: {mineral.Id})");
        }

        OnCollectionChanged?.Invoke();
        return true;
    }

    // Unlocks all minerals (for testing)
    public void DiscoverAll(int initialCollectionCount = 1)
    {
        if (!IsInitialized) Initialize();

        bool anyChanged = false;
        for (int i = 0; i < collectionEntries.Count; i++)
        {
            var entry = collectionEntries[i];
            if (entry == null || entry.Mineral == null) continue;

            if (!entry.IsDiscovered)
            {
                entry.MarkDiscovered(Time.time);
                anyChanged = true;
            }

            if (entry.CountCollected < initialCollectionCount)
            {
                int diff = initialCollectionCount - entry.CountCollected;
                for (int c = 0; c < diff; c++) entry.IncrementCollected();
                anyChanged = true;
            }
        }

        if (anyChanged)
        {
            OnCollectionChanged?.Invoke();
        }
    }

    public List<Mineral> GetAllMinerals()
    {
        if (!IsInitialized) Initialize();
        var list = new List<Mineral>(collectionEntries.Count);
        for (int i = 0; i < collectionEntries.Count; i++)
        {
            if (collectionEntries[i].Mineral != null)
                list.Add(collectionEntries[i].Mineral);
        }
        return list;
    }

    public bool IsDiscovered(uint mineralId)
    {
        if (!IsInitialized) Initialize();
        return entriesById.TryGetValue(mineralId, out var entry) && entry.IsDiscovered;
    }

    public Mineral GetMineralById(uint mineralId)
    {
        if (!IsInitialized) Initialize();
        return entriesById.TryGetValue(mineralId, out var entry) ? entry.Mineral : null;
    }

    public GameObject GetMineralPrefab(uint mineralId)
    {
        Mineral mineral = GetMineralById(mineralId);
        if (mineral == null) return null;

        return Resources.Load<GameObject>($"Prefabs/Mineral/{mineral.MineralName}") ?? mineralPrefab;
    }

    public GameObject SpawnDisplayMineral(uint mineralId, Transform spawnerTransform)
    {
        if (spawnerTransform == null) return null;

        Mineral mineral = GetMineralById(mineralId);
        if (mineral == null) return null;

        // Remove any previous spawned object
        for (int i = spawnerTransform.childCount - 1; i >= 0; i--)
        {
            Destroy(spawnerTransform.GetChild(i).gameObject);
        }

        GameObject prefab = GetMineralPrefab(mineralId);
        if (prefab == null) return null;

        GameObject instance = Instantiate(prefab, spawnerTransform.position, spawnerTransform.rotation, spawnerTransform);
        instance.name = $"{mineral.MineralName}_Display";
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one * 5;

        if (instance.TryGetComponent<MineralBehaviour>(out var mb))
        {
            mb.SetDisplayOnly(mineral);
        }

        return instance;
    }
}
