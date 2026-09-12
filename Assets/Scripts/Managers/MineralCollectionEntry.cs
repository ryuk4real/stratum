using System;
using UnityEngine;

/// <summary>
/// Serializable data model representing the collection status of a specific mineral.
/// </summary>
[System.Serializable]
public class MineralCollectionEntry
{
    [Tooltip("Reference to the mineral ScriptableObject definition.")]
    [SerializeField] private Mineral mineral;

    [Tooltip("Whether this mineral has been discovered and collected by the player.")]
    [SerializeField] private bool isDiscovered = false;

    [Tooltip("Total amount of times this mineral has been collected.")]
    [SerializeField] private int countCollected = 0;

    [Tooltip("Game time when the mineral was first discovered (0 if not yet discovered).")]
    [SerializeField] private float firstDiscoveredTime = 0f;

    public Mineral Mineral => mineral;
    public bool IsDiscovered => isDiscovered;
    public int CountCollected => countCollected;
    public float FirstDiscoveredTime => firstDiscoveredTime;

    public MineralCollectionEntry(Mineral mineral)
    {
        this.mineral = mineral;
        this.isDiscovered = false;
        this.countCollected = 0;
        this.firstDiscoveredTime = 0f;
    }

    public void MarkDiscovered(float timestamp)
    {
        isDiscovered = true;
        if (firstDiscoveredTime <= 0f)
        {
            firstDiscoveredTime = timestamp;
        }
    }

    public void IncrementCollected()
    {
        countCollected++;
    }

    public void Reset()
    {
        isDiscovered = false;
        countCollected = 0;
        firstDiscoveredTime = 0f;
    }
}
