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

    public Mineral Mineral => mineral;
    public bool IsDiscovered => isDiscovered;
    public int CountCollected => countCollected;

    public MineralCollectionEntry(Mineral mineral)
    {
        this.mineral = mineral;
        this.isDiscovered = false;
        this.countCollected = 0;
    }

    public void MarkDiscovered(float timestamp = 0f)
    {
        isDiscovered = true;
    }

    public void IncrementCollected()
    {
        countCollected++;
    }
}
