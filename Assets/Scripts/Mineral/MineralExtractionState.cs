/// <summary>
/// Lifecycle state of a mineral in the terrain.
/// </summary>
public enum MineralExtractionState
{
    Buried,      // Mineral completely in the ground
    Extractable, // Mostly or completely uncovered, ready to be picked up by hand
    Extracted,   // Extracted by hand
    DisplayOnly  // Observation copy on display; grabbable but cannot be collected
}