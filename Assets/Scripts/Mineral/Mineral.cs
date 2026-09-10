using UnityEngine;

[CreateAssetMenu(fileName = "New Mineral", menuName = "Stratum/Mineral")]
public class Mineral : ScriptableObject
{
    [Header("Mineral Info")]
    [SerializeField] private uint id;
    [SerializeField] private string mineralName;
    [TextArea(2, 4)]
    [SerializeField] private string description;

    [Header("Stats")]
    [Tooltip("Rarity of the mineral in the world. Higher values means the mineral is more rare.")]
    [SerializeField] private uint rarity;

    [Tooltip("The maximum number of mineral per vein. Higher values means the mineral is more common in a vain.")]
    [SerializeField] private uint maxVeinAmount;

    [Tooltip("The minimum number of mineral per vein. Lower values means the mineral is more common in a vain.")]
    [SerializeField] private uint minVeinAmount;

    [Tooltip("The scale range of the mineral. The mineral will be spawned with a random scale within this range.")]
    [SerializeField] private Vector2 scaleRange = new Vector2(1.8f, 2.3f);

    [Header("Graphics")]
    [SerializeField] private Mesh mesh;
    [SerializeField] private Material material;

    // public getters
    public uint Id => id;
    public string MineralName => string.IsNullOrEmpty(mineralName) ? name : mineralName;
    public string Description => description;
    public uint Rarity => rarity;
    public uint MaxVeinAmount => maxVeinAmount;
    public uint MinVeinAmount => minVeinAmount;
    public Mesh Mesh => mesh;
    public Material Material => material;
    public Vector2 ScaleRange => scaleRange;
}