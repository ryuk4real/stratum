using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central manager that controls the lifecycle and initialization order of all singleton managers.
/// Ensures all singleton managers are initialized before UI or gameplay scripts run.
/// </summary>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Singleton Managers")]
    [SerializeField] private CollectionManager collectionManager;
    [SerializeField] private TerrainManager terrainManager;

    private readonly List<IManager> managers = new List<IManager>();
    public bool IsInitialized { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        InitializeManagers();
    }

    private void InitializeManagers()
    {
        if (IsInitialized) return;

        if (collectionManager == null) collectionManager = CollectionManager.Instance ?? FindAnyObjectByType<CollectionManager>();
        if (terrainManager == null) terrainManager = TerrainManager.Instance ?? FindAnyObjectByType<TerrainManager>();

        managers.Clear();
        if (collectionManager != null) managers.Add(collectionManager);
        if (terrainManager != null) managers.Add(terrainManager);

        for (int i = 0; i < managers.Count; i++)
        {
            try
            {
                managers[i].Initialize();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameManager] Error initializing {managers[i].GetType().Name}: {ex.Message}");
            }
        }

        IsInitialized = true;
        Debug.Log($"[GameManager] Successfully initialized {managers.Count} managers.");
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            for (int i = 0; i < managers.Count; i++)
            {
                managers[i]?.Free();
            }
            managers.Clear();
            Instance = null;
        }
    }
}
