using System;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// Manages the player's mineral collection
/// </summary>
[DisallowMultipleComponent]
public class CollectionManager : MonoBehaviour, IManager
{
    public static CollectionManager Instance { get; private set; }

    [Header("Settings")]
    [Tooltip("If true, automatically initializes the manager on Start if not already initialized.")]
    [SerializeField] private bool initializeOnStart = true;

    [Tooltip("Optional database of available minerals. If left empty, minerals are loaded from Resources/Minerals.")]
    [SerializeField] private List<Mineral> availableMinerals = new List<Mineral>();

    [Header("Collection State")]
    [Tooltip("List of all collection entries tracked by the manager. Visible in Inspector for monitoring.")]
    [SerializeField] private List<MineralCollectionEntry> collectionEntries = new List<MineralCollectionEntry>();

    // Fast lookup dictionaries
    private readonly Dictionary<uint, MineralCollectionEntry> entriesById = new Dictionary<uint, MineralCollectionEntry>();
    private readonly Dictionary<Mineral, MineralCollectionEntry> entriesByMineral = new Dictionary<Mineral, MineralCollectionEntry>();

    private bool isInitialized = false;

    // Events for laboratory UI and game systems
    public event Action<Mineral> OnMineralDiscovered;
    public event Action<Mineral, int> OnMineralCollected;
    public event Action OnCollectionChanged;

    public bool IsInitialized => isInitialized;

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
        if (initializeOnStart && !isInitialized)
        {
            Initialize();
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
        if (isInitialized) return;

        LoadMineralsDatabase();
        RebuildLookupTables();

        isInitialized = true;
        Debug.Log($"[CollectionManager] Initialized with {collectionEntries.Count} minerals ({DiscoveredCount} discovered, {UndiscoveredCount} undiscovered).");
    }

    // Frees resources and clears lookup data.
    public void Free()
    {
        entriesById.Clear();
        entriesByMineral.Clear();
        isInitialized = false;
    }

    // Loads mineral ScriptableObjects from Resources if not assigned.
    private void LoadMineralsDatabase()
    {
        if (availableMinerals == null || availableMinerals.Count == 0)
        {
            Mineral[] loaded = Resources.LoadAll<Mineral>("Minerals");
            availableMinerals = new List<Mineral>(loaded);
        }

        // Sort minerals by ID for consistent ordering
        availableMinerals.Sort((a, b) =>
        {
            if (a == null && b == null) return 0;
            if (a == null) return 1;
            if (b == null) return -1;
            return a.Id.CompareTo(b.Id);
        });

        // Match existing entries in collectionEntries or add new ones
        var existingMap = new Dictionary<Mineral, MineralCollectionEntry>();
        for (int i = 0; i < collectionEntries.Count; i++)
        {
            var entry = collectionEntries[i];
            if (entry != null && entry.Mineral != null && !existingMap.ContainsKey(entry.Mineral))
            {
                existingMap.Add(entry.Mineral, entry);
            }
        }

        collectionEntries.Clear();
        for (int i = 0; i < availableMinerals.Count; i++)
        {
            Mineral m = availableMinerals[i];
            if (m == null) continue;

            if (existingMap.TryGetValue(m, out var existingEntry))
            {
                collectionEntries.Add(existingEntry);
            }
            else
            {
                collectionEntries.Add(new MineralCollectionEntry(m));
            }
        }
    }

    // Rebuilds fast lookup dictionaries from the entries list.
    private void RebuildLookupTables()
    {
        entriesById.Clear();
        entriesByMineral.Clear();

        for (int i = 0; i < collectionEntries.Count; i++)
        {
            var entry = collectionEntries[i];
            if (entry == null || entry.Mineral == null) continue;

            entriesByMineral[entry.Mineral] = entry;
            entriesById[entry.Mineral.Id] = entry;
        }
    }

    // Sets a mineral as discovered / collected.
    public bool DiscoverMineral(Mineral mineral) => CollectMineral(mineral);
    public bool DiscoverMineral(MineralBehaviour mineralBehaviour) => CollectMineral(mineralBehaviour);

    // Collects a mineral instance from a MineralBehaviour
    public bool CollectMineral(MineralBehaviour mineralBehaviour)
    {
        if (mineralBehaviour == null) return false;
        return CollectMineral(mineralBehaviour.MineralData);
    }

    // Collects a mineral by ScriptableObject reference.
    public bool CollectMineral(Mineral mineral)
    {
        if (mineral == null)
        {
            return false;
        }

        if (!isInitialized)
        {
            Initialize();
        }

        if (!entriesByMineral.TryGetValue(mineral, out var entry))
        {
            if (!entriesById.TryGetValue(mineral.Id, out entry))
            {
                // Register dynamically if not previously found in database
                entry = new MineralCollectionEntry(mineral);
                collectionEntries.Add(entry);
                entriesByMineral[mineral] = entry;
                entriesById[mineral.Id] = entry;
            }
        }

        bool wasAlreadyDiscovered = entry.IsDiscovered;
        entry.MarkDiscovered(Time.time);
        entry.IncrementCollected();

        if (!wasAlreadyDiscovered)
        {
            Debug.Log($"[CollectionManager] <color=cyan>NEW MINERAL DISCOVERED:</color> {mineral.MineralName} (ID: {mineral.Id})!");
            OnMineralDiscovered?.Invoke(mineral);
        }

        Debug.Log($"[CollectionManager] Collected {mineral.MineralName}. Total collected: {entry.CountCollected}");
        OnMineralCollected?.Invoke(mineral, entry.CountCollected);
        OnCollectionChanged?.Invoke();

        return true;
    }

    public IReadOnlyList<MineralCollectionEntry> Entries => collectionEntries;

    public int TotalMineralsCount => collectionEntries.Count;

    public int DiscoveredCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < collectionEntries.Count; i++)
            {
                if (collectionEntries[i].IsDiscovered) count++;
            }
            return count;
        }
    }

    public int UndiscoveredCount => TotalMineralsCount - DiscoveredCount;

    public float DiscoveryPercentage
    {
        get
        {
            if (TotalMineralsCount == 0) return 0f;
            return ((float)DiscoveredCount / TotalMineralsCount) * 100f;
        }
    }

    public List<Mineral> GetAllMinerals()
    {
        var list = new List<Mineral>(collectionEntries.Count);
        for (int i = 0; i < collectionEntries.Count; i++)
        {
            if (collectionEntries[i].Mineral != null)
                list.Add(collectionEntries[i].Mineral);
        }
        return list;
    }

    public List<Mineral> GetDiscoveredMinerals()
    {
        var list = new List<Mineral>();
        for (int i = 0; i < collectionEntries.Count; i++)
        {
            if (collectionEntries[i].IsDiscovered && collectionEntries[i].Mineral != null)
                list.Add(collectionEntries[i].Mineral);
        }
        return list;
    }

    public List<Mineral> GetUndiscoveredMinerals()
    {
        var list = new List<Mineral>();
        for (int i = 0; i < collectionEntries.Count; i++)
        {
            if (!collectionEntries[i].IsDiscovered && collectionEntries[i].Mineral != null)
                list.Add(collectionEntries[i].Mineral);
        }
        return list;
    }

    public bool IsDiscovered(Mineral mineral)
    {
        if (mineral == null) return false;
        return entriesByMineral.TryGetValue(mineral, out var entry) && entry.IsDiscovered;
    }

    public bool IsDiscovered(uint mineralId)
    {
        return entriesById.TryGetValue(mineralId, out var entry) && entry.IsDiscovered;
    }

    public int GetCollectedCount(Mineral mineral)
    {
        if (mineral == null) return 0;
        return entriesByMineral.TryGetValue(mineral, out var entry) ? entry.CountCollected : 0;
    }

    public int GetCollectedCount(uint mineralId)
    {
        return entriesById.TryGetValue(mineralId, out var entry) ? entry.CountCollected : 0;
    }

    public Mineral GetMineralById(uint mineralId)
    {
        return entriesById.TryGetValue(mineralId, out var entry) ? entry.Mineral : null;
    }

    public MineralCollectionEntry GetEntry(Mineral mineral)
    {
        if (mineral == null) return null;
        entriesByMineral.TryGetValue(mineral, out var entry);
        return entry;
    }

    public MineralCollectionEntry GetEntry(uint mineralId)
    {
        entriesById.TryGetValue(mineralId, out var entry);
        return entry;
    }
}
