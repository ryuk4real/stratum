using System;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// Manager that controls the procedural volumetric terrain generation.
/// </summary>
public class TerrainManager : MonoBehaviour, IManager
{
    public static TerrainManager Instance { get; private set; }

    [Header("Compute Shader")]
    [SerializeField] private ComputeShader marchingCubesShader;

    [Header("Material & Physics")]
    [SerializeField] private Material terrainMaterial;
    [SerializeField] private PhysicsMaterial terrainPhysicsMaterial;
    [SerializeField] private int terrainLayer = 6;

    [Header("Chunk Grid Settings")]
    [SerializeField] private int chunkSize = 32;
    [SerializeField] private float voxelSize = 1.0f;
    [SerializeField] private Vector3Int chunkDimensions = new Vector3Int(2, 10, 2);

    [Header("Cavern & Room Settings")]
    [Tooltip("If true, automatically centers the starting cavern in the geometric center of the terrain to generate.")]
    [SerializeField] private bool centerCavernInTerrain = true;
    [Tooltip("Center position of the initial cavern room. If cavernCenterIsRelative is true, this is an offset relative to TerrainManager.")]
    [SerializeField] private Vector3 cavernCenter = new Vector3(32f, 160f, 32f);
    [Tooltip("If true, cavernCenter is relative to TerrainManager's transform position.")]
    [SerializeField] private bool cavernCenterIsRelative = true;
    [Tooltip("Width (X), Height (Y), Depth (Z) of the starting cavern room.")]
    [SerializeField] private Vector3 cavernSize = new Vector3(24f, 10f, 24f);

    [Header("Colors")]
    private Color cavernGizmoColor = Color.yellow;
    private Color terrainGizmoColor = Color.white;

    public bool CenterCavernInTerrainSetting
    {
        get => centerCavernInTerrain;
        set
        {
            centerCavernInTerrain = value;
            if (centerCavernInTerrain)
            {
                CenterCavernInTerrain();
            }
        }
    }

    public int ChunkSize => chunkSize;
    public float VoxelSize => voxelSize;
    public Vector3Int ChunkDimensions => chunkDimensions;
    public Vector3 CavernSize => cavernSize;

    [Header("Rock & Wall Noise Settings")]
    [SerializeField] private float isoLevel = 0.0f;
    [SerializeField] private float noiseFrequency = 0.04f;
    [SerializeField] private float terrainHeightVariation = 2.0f;

    [Header("Lifecycle & Auto-Init")]
    [SerializeField] private bool initializeOnStart = true;

    [Header("Gizmos & Debug")]
    [Tooltip("If true, gizmos are drawn in the Scene view even when TerrainManager is not selected or is disabled.")]
    [SerializeField] private bool alwaysDrawGizmos = true;
    [SerializeField] private bool drawCavernGizmo = true;
    [SerializeField] private bool drawTerrainBoundsGizmo = true;

    // Data structures to store the chunks and buffers
    private readonly Dictionary<Vector3Int, TerrainChunk> chunks = new Dictionary<Vector3Int, TerrainChunk>();
    private ComputeBuffer triangleBuffer;
    private ComputeBuffer counterBuffer;
    private ComputeBuffer triTableBuffer;
    private ComputeBuffer edgeTableBuffer;
    private int maxTrianglesPerChunk;

    // Kernel IDs
    private int kernelGenerateDensity;
    private int kernelMarchingCubes;
    private int kernelModifyDensity;

    private bool isInitialized = false;

    private void OnValidate()
    {
        if (chunkSize < 1) chunkSize = 1;
        if (voxelSize < 0.01f) voxelSize = 0.01f;
        chunkDimensions = Vector3Int.Max(chunkDimensions, Vector3Int.one);

        if (centerCavernInTerrain)
        {
            CenterCavernInTerrain();
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
        if (initializeOnStart && !isInitialized)
        {
            Initialize();
        }
    }


    /// Initialize the manager, allocate GPU buffers and generate all chunks in memory
    public void Initialize()
    {
        if (terrainLayer == 0)
        {
            int layerFromName = LayerMask.NameToLayer("Terrain");
            if (layerFromName != -1)
            {
                terrainLayer = layerFromName;
            }
        }

        if (terrainMaterial != null)
        {
            float totalHeight = chunkDimensions.y * chunkSize * voxelSize;
            float topY = transform.position.y + totalHeight;
            if (terrainMaterial.HasProperty("_SurfaceBaseHeight"))
                terrainMaterial.SetFloat("_SurfaceBaseHeight", topY);
            if (terrainMaterial.HasProperty("_NoiseFrequency"))
                terrainMaterial.SetFloat("_NoiseFrequency", noiseFrequency);
            if (terrainMaterial.HasProperty("_TerrainHeightVariation"))
                terrainMaterial.SetFloat("_TerrainHeightVariation", terrainHeightVariation);
        }

        if (centerCavernInTerrain)
        {
            CenterCavernInTerrain();
        }

        kernelGenerateDensity = marchingCubesShader.FindKernel("GenerateDensity");
        kernelMarchingCubes = marchingCubesShader.FindKernel("MarchingCubes");
        kernelModifyDensity = marchingCubesShader.FindKernel("ModifyDensity");

        // Max triangles per voxel = 5. Total = chunkSize^3 * 5
        maxTrianglesPerChunk = chunkSize * chunkSize * chunkSize * 5;
        int stride = sizeof(float) * 6 * 3; // 3 vertices * (Vector3 pos + Vector3 normal)
        
        triangleBuffer = new ComputeBuffer(maxTrianglesPerChunk, stride, ComputeBufferType.Default);
        counterBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Default);

        // Load triangulation tables to GPU buffers
        edgeTableBuffer = new ComputeBuffer(256, sizeof(int));
        edgeTableBuffer.SetData(MarchingCubesTables.EdgeTable);

        triTableBuffer = new ComputeBuffer(4096, sizeof(int));
        triTableBuffer.SetData(MarchingCubesTables.TriangulationTable);

        GenerateAllChunks();
        isInitialized = true;
        Debug.Log($"[TerrainManager] Successfully initialized. Generated {chunks.Count} chunks.");
    }

    /// Generates the initial grid of chunks and computes their density and continuous mesh
    private void GenerateAllChunks()
    {
        // Create chunks in a grid
        for (int x = 0; x < chunkDimensions.x; x++)
        {
            for (int y = 0; y < chunkDimensions.y; y++)
            {
                for (int z = 0; z < chunkDimensions.z; z++)
                {
                    Vector3Int coord = new Vector3Int(x, y, z);
                    CreateChunk(coord);
                }
            }
        }

        Vector3 worldCavernCenter = GetWorldCavernCenter();

        // Generate the density field for all chunks
        foreach (var chunk in chunks.Values)
        {
            chunk.GenerateDensityGPU(
                marchingCubesShader,
                kernelGenerateDensity,
                worldCavernCenter,
                cavernSize,
                noiseFrequency,
                terrainHeightVariation
            );
        }

        // Perform Marching Cubes
        foreach (var chunk in chunks.Values)
        {
            chunk.Polygonise(
                marchingCubesShader,
                kernelMarchingCubes,
                triangleBuffer,
                counterBuffer,
                triTableBuffer,
                edgeTableBuffer,
                maxTrianglesPerChunk,
                isoLevel,
                worldCavernCenter,
                cavernSize,
                noiseFrequency,
                terrainHeightVariation
            );
        }
    }

    private TerrainChunk CreateChunk(Vector3Int coord)
    {
        GameObject chunkObj = new GameObject($"Chunk_{coord.x}_{coord.y}_{coord.z}");
        chunkObj.layer = terrainLayer;
        chunkObj.transform.SetParent(transform, false);
        chunkObj.transform.localPosition = new Vector3(
            coord.x * chunkSize * voxelSize,
            coord.y * chunkSize * voxelSize,
            coord.z * chunkSize * voxelSize
        );
        chunkObj.transform.localRotation = Quaternion.identity;
        chunkObj.transform.localScale = Vector3.one;

        TerrainChunk chunk = chunkObj.AddComponent<TerrainChunk>();
        chunk.InitializeChunk(coord, chunkSize, voxelSize, terrainMaterial, terrainPhysicsMaterial);
        chunks[coord] = chunk;
        return chunk;
    }

    /// Enables or disables the collisions on a specific chunk
    public void SetChunkColliderEnabled(Vector3Int coord, bool isColliderEnabled)
    {
        if (chunks.TryGetValue(coord, out TerrainChunk chunk))
        {
            chunk.SetColliderEnabled(isColliderEnabled);
        }
    }

    // Modifies the terrain at a given world position
    public void ModifyTerrain(Vector3 worldPosition, float radius, float strength)
    {
        if (!isInitialized) return;

        // Terrain can only be eroded (negative delta)
        if (strength <= 0f) return;
        float delta = -strength;

        List<TerrainChunk> affectedChunks = new List<TerrainChunk>();

        // Find all chunks affected by the digging area
        foreach (var chunk in chunks.Values)
        {
            bool modified = chunk.ModifyDensity(worldPosition, radius, delta, marchingCubesShader, kernelModifyDensity);
            if (modified)
            {
                affectedChunks.Add(chunk);
            }
        }

        Vector3 worldCavernCenter = GetWorldCavernCenter();

        // Regenerate mesh for all affected chunks
        foreach (var chunk in affectedChunks)
        {
            chunk.Polygonise(
                marchingCubesShader,
                kernelMarchingCubes,
                triangleBuffer,
                counterBuffer,
                triTableBuffer,
                edgeTableBuffer,
                maxTrianglesPerChunk,
                isoLevel,
                worldCavernCenter,
                cavernSize,
                noiseFrequency,
                terrainHeightVariation
            );
        }
    }

    // Set the visibility of a single chunk given its coordinate index
    public void SetChunkVisibility(Vector3Int coord, bool visible)
    {
        if (chunks.TryGetValue(coord, out TerrainChunk chunk))
        {
            chunk.SetVisibility(visible);
        }
    }

    // Get a chunk based on its discrete coordinates
    public TerrainChunk GetChunk(Vector3Int coord)
    {
        chunks.TryGetValue(coord, out TerrainChunk chunk);
        return chunk;
    }

    // Get the chunk corresponding to a given world position
    public TerrainChunk GetChunkAtWorldPos(Vector3 worldPos)
    {
        Vector3 localPos = worldPos - transform.position;
        int cx = Mathf.FloorToInt(localPos.x / (chunkSize * voxelSize));
        int cy = Mathf.FloorToInt(localPos.y / (chunkSize * voxelSize));
        int cz = Mathf.FloorToInt(localPos.z / (chunkSize * voxelSize));
        return GetChunk(new Vector3Int(cx, cy, cz));
    }

    // Release all GPU resources, destroy chunk gameobjects and reset state
    public void Free()
    {
        foreach (var chunk in chunks.Values)
        {
            if (chunk != null)
            {
                chunk.Dispose();
                if (chunk.gameObject != null)
                {
                    Destroy(chunk.gameObject);
                }
            }
        }
        chunks.Clear();

        if (triangleBuffer != null)
        {
            triangleBuffer.Release();
            triangleBuffer = null;
        }

        if (counterBuffer != null)
        {
            counterBuffer.Release();
            counterBuffer = null;
        }

        if (triTableBuffer != null)
        {
            triTableBuffer.Release();
            triTableBuffer = null;
        }

        if (edgeTableBuffer != null)
        {
            edgeTableBuffer.Release();
            edgeTableBuffer = null;
        }

        isInitialized = false;
    }

    private void OnDestroy()
    {
        Free();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    public Vector3 GetWorldCavernCenter()
    {
        return cavernCenterIsRelative ? transform.position + cavernCenter : cavernCenter;
    }

    // Centers the cavern room in the geometric center of the terrain to generate.
    [ContextMenu("Center Cavern In Terrain")]
    [ContextMenu("Center Cavern In Volume")]
    public void CenterCavernInTerrain()
    {
        Vector3 totalSize = new Vector3(
            chunkDimensions.x * chunkSize * voxelSize,
            chunkDimensions.y * chunkSize * voxelSize,
            chunkDimensions.z * chunkSize * voxelSize
        );
        cavernCenter = totalSize * 0.5f;
        cavernCenterIsRelative = true;
    }

    public void CenterCavernInVolume()
    {
        CenterCavernInTerrain();
    }

    // Draw gizmos in the Scene view
    private void OnDrawGizmos()
    {
        if (alwaysDrawGizmos)
        {
            DrawGizmosInternal();
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!alwaysDrawGizmos)
        {
            DrawGizmosInternal();
        }
    }

    private void DrawGizmosInternal()
    {
        if (drawTerrainBoundsGizmo)
        {
            Gizmos.color = terrainGizmoColor;
            Vector3 size = new Vector3(
                chunkDimensions.x * chunkSize * voxelSize,
                chunkDimensions.y * chunkSize * voxelSize,
                chunkDimensions.z * chunkSize * voxelSize
            );
            Gizmos.DrawWireCube(transform.position + size * 0.5f, size);
        }

        if (drawCavernGizmo)
        {
            // Draw initial cavern boundaries
            Gizmos.color = cavernGizmoColor;
            Gizmos.DrawWireCube(GetWorldCavernCenter(), cavernSize);
        }
    }
}
