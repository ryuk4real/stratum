using System;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// Manager that controls the procedural volumetric terrain generation.
/// </summary>
public class TerrainManager : MonoBehaviour, IManager
{
    public static TerrainManager Instance { get; private set; }

    public bool IsInitialized => isInitialized;

    [Header("Compute Shader")]
    [SerializeField] private ComputeShader marchingCubesShader;

    [Header("Material & Physics")]
    [SerializeField] private Material terrainMaterial;
    [SerializeField] private PhysicsMaterial terrainPhysicsMaterial;
    [SerializeField] private LayerMask terrainLayer;
    [SerializeField] private LayerMask mineralLayer;

    [Header("Mineral Generation Settings")]
    [Tooltip("If true, minerals are generated procedural in chunks when visited by the player.")]
    [SerializeField] private bool generateMinerals = true;
    [Tooltip("Reference to the player Transform. If null, automatically tracked at runtime.")]
    [SerializeField] private Transform playerTransform;
    [Tooltip("Percentage chance for a deposit to be a Vein vs Single (e.g. 40% means 40% veins and 60% singles).")]
    [SerializeField, Range(0f, 100f)] private float veinPercentage = 40f;
    [Tooltip("Min and Max number of mineral deposits (veins or singles) per chunk.")]
    [SerializeField] private Vector2Int mineralDepositsPerChunk = new Vector2Int(2, 5);
    [Tooltip("Dispersion radius for minerals belonging to the same vein.")]
    [SerializeField, Min(0.1f)] private float veinSpreadRadius = 2.0f;
    [Tooltip("Available minerals in the world. If empty, loaded from Resources.")]
    [SerializeField] private Mineral[] availableMinerals;

    [Header("Chunk Grid Settings")]
    [SerializeField] private int chunkSize = 8;
    [SerializeField] private float voxelSize = 0.5f;
    [SerializeField] private Vector3Int chunkDimensions = new Vector3Int(14, 25, 14);

    [Header("Cavern & Room Settings")]
    [Tooltip("If true, automatically centers the starting cavern in the geometric center of the terrain to generate.")]
    [SerializeField] private bool centerCavernInTerrain = false;
    [Tooltip("Center position of the initial cavern room. If cavernCenterIsRelative is true, this is an offset relative to TerrainManager.")]
    [SerializeField] private Vector3 cavernCenter = new Vector3(160f, 275f, 160f);
    [Tooltip("If true, cavernCenter is relative to TerrainManager's transform position.")]
    [SerializeField] private bool cavernCenterIsRelative = false;
    [Tooltip("Width (X), Height (Y), Depth (Z) of the starting cavern room.")]
    [SerializeField] private Vector3 cavernSize = new Vector3(20f, 7f, 20f);

    [Header("Colors")]
    private Color cavernGizmoColor = Color.yellow;
    private Color terrainGizmoColor = Color.white;

    [Header("Cavern Wall & Ceiling Noise Settings")]
    [Tooltip("Amplitude of the Perlin noise on the wall exposed to the terrain.")]
    [SerializeField] private float wallNoiseAmplitude = 1.0f;
    [Tooltip("Frequency of the Perlin noise on the wall exposed to the terrain.")]
    [SerializeField] private float wallNoiseFrequency = 0.25f;
    [Tooltip("Amplitude of the Perlin noise on the cavern ceiling.")]
    [SerializeField] private float ceilingNoiseAmplitude = 1.2f;
    [Tooltip("Frequency of the Perlin noise on the cavern ceiling.")]
    [SerializeField] private float ceilingNoiseFrequency = 0.25f;

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
    // Compute Buffers & Kernel IDs
    private ComputeBuffer triangleBuffer;
    private ComputeBuffer counterBuffer;
    private ComputeBuffer triTableBuffer;
    private ComputeBuffer edgeTableBuffer;
    private int maxTrianglesPerChunk;

    private int kernelGenerateDensity;
    private int kernelMarchingCubes;
    private int kernelModifyDensity;

    private readonly Dictionary<string, GameObject> mineralPrefabCache = new Dictionary<string, GameObject>();
    private int cachedMinRarity = 1;
    private int cachedMaxRarity = 8;

    private bool isInitialized = false;

    private void OnValidate()
    {
        if (chunkSize < 1) chunkSize = 1;
        if (voxelSize < 0.01f) voxelSize = 0.01f;
        chunkDimensions = Vector3Int.Max(chunkDimensions, Vector3Int.one);

        if (mineralDepositsPerChunk.x < 0) mineralDepositsPerChunk.x = 0;
        if (mineralDepositsPerChunk.y < mineralDepositsPerChunk.x) mineralDepositsPerChunk.y = mineralDepositsPerChunk.x;
        if (veinSpreadRadius < 0.1f) veinSpreadRadius = 0.1f;

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

    private void Update()
    {
        if (!isInitialized || !generateMinerals) return;

        TrackPlayerChunk();
    }

    // Initialize the manager, allocate GPU buffers and generate all chunks in memory
    public void Initialize()
    {
        if (isInitialized) return;

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

        UpdateRarityRange();
        GenerateAllChunks();
        isInitialized = true;
        Debug.Log($"[TerrainManager] Successfully initialized. Generated {chunks.Count} chunks.");

        if (generateMinerals)
        {
            TrackPlayerChunk();
        }
    }

    private void UpdateRarityRange()
    {
        if (availableMinerals == null || availableMinerals.Length == 0)
        {
            availableMinerals = Resources.LoadAll<Mineral>("Minerals");
        }

        if (availableMinerals != null && availableMinerals.Length > 0)
        {
            cachedMinRarity = int.MaxValue;
            cachedMaxRarity = int.MinValue;
            for (int i = 0; i < availableMinerals.Length; i++)
            {
                if (availableMinerals[i] == null) continue;
                int r = (int)availableMinerals[i].Rarity;
                if (r < cachedMinRarity) cachedMinRarity = r;
                if (r > cachedMaxRarity) cachedMaxRarity = r;
            }
            if (cachedMinRarity > cachedMaxRarity)
            {
                cachedMinRarity = 1;
                cachedMaxRarity = 8;
            }
        }
    }

    // Generates the initial grid of chunks and computes their density and continuous mesh
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
                terrainHeightVariation,
                wallNoiseAmplitude,
                wallNoiseFrequency,
                ceilingNoiseAmplitude,
                ceilingNoiseFrequency
            );
        }

        // Perform Marching Cubes only for chunks that intersect the starting cavern
        float maxMargin = Mathf.Max(wallNoiseAmplitude, ceilingNoiseAmplitude) + terrainHeightVariation + 3f;
        Bounds cavernBounds = new Bounds(worldCavernCenter, cavernSize + Vector3.one * (maxMargin * 2f));

        foreach (var chunk in chunks.Values)
        {
            if (chunk.WorldBounds.Intersects(cavernBounds))
            {
                chunk.Polygonise(
                    marchingCubesShader,
                    kernelMarchingCubes,
                    triangleBuffer,
                    counterBuffer,
                    triTableBuffer,
                    edgeTableBuffer,
                    maxTrianglesPerChunk,
                    isoLevel
                );
            }
        }
    }

    private static int LayerMaskToLayer(LayerMask mask, string fallbackName, int fallbackLayer)
    {
        int val = mask.value;
        if (val <= 0)
        {
            int named = LayerMask.NameToLayer(fallbackName);
            return named != -1 ? named : fallbackLayer;
        }
        int layer = 0;
        while ((val & 1) == 0 && layer < 31)
        {
            val >>= 1;
            layer++;
        }
        return layer;
    }

    private TerrainChunk CreateChunk(Vector3Int coord)
    {
        GameObject chunkObj = new GameObject($"Chunk_{coord.x}_{coord.y}_{coord.z}");
        chunkObj.layer = LayerMaskToLayer(terrainLayer, "Terrain", 6);
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

    // Modifies the terrain at a given world position
    public void ModifyTerrain(Vector3 worldPosition, float radius, float strength)
    {
        if (!isInitialized || strength <= 0f) return;
        float delta = -strength;

        // Calculate chunk bounding box intersecting the spherical dig area
        float chunkWorldSpan = chunkSize * voxelSize;
        Vector3 minPos = (worldPosition - Vector3.one * radius) - transform.position;
        Vector3 maxPos = (worldPosition + Vector3.one * radius) - transform.position;

        int minX = Mathf.Clamp(Mathf.FloorToInt(minPos.x / chunkWorldSpan), 0, chunkDimensions.x - 1);
        int maxX = Mathf.Clamp(Mathf.FloorToInt(maxPos.x / chunkWorldSpan), 0, chunkDimensions.x - 1);
        int minY = Mathf.Clamp(Mathf.FloorToInt(minPos.y / chunkWorldSpan), 0, chunkDimensions.y - 1);
        int maxY = Mathf.Clamp(Mathf.FloorToInt(maxPos.y / chunkWorldSpan), 0, chunkDimensions.y - 1);
        int minZ = Mathf.Clamp(Mathf.FloorToInt(minPos.z / chunkWorldSpan), 0, chunkDimensions.z - 1);
        int maxZ = Mathf.Clamp(Mathf.FloorToInt(maxPos.z / chunkWorldSpan), 0, chunkDimensions.z - 1);

        List<TerrainChunk> affectedChunks = new List<TerrainChunk>();

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    if (chunks.TryGetValue(new Vector3Int(x, y, z), out TerrainChunk chunk))
                    {
                        if (chunk.ModifyDensity(worldPosition, radius, delta, marchingCubesShader, kernelModifyDensity))
                        {
                            affectedChunks.Add(chunk);
                        }
                    }
                }
            }
        }

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
                isoLevel
            );
        }

        if (affectedChunks.Count > 0)
        {
            // Restrict mineral exposure check strictly to the single chunk where the dig occurred
            TerrainChunk digChunk = GetChunkAtWorldPos(worldPosition);
            if (digChunk != null)
            {
                // check radius immediately around the excavated hole
                float checkRadius = radius * 1.25f;
                int minLayerIdx = LayerMaskToLayer(mineralLayer, "Mineral", 9);
                LayerMask mineralMask = mineralLayer.value != 0 ? mineralLayer : (LayerMask)(1 << minLayerIdx);
                Collider[] hitMinerals = Physics.OverlapSphere(worldPosition, checkRadius, mineralMask);
                for (int i = 0; i < hitMinerals.Length; i++)
                {
                    // Only process minerals belonging to this single targeted chunk
                    if (hitMinerals[i].transform.IsChildOf(digChunk.transform) || digChunk.WorldBounds.Contains(hitMinerals[i].transform.position))
                    {
                        if (hitMinerals[i].TryGetComponent<MineralBehaviour>(out var mineral))
                        {
                            mineral.CheckExposure();
                        }
                    }
                }
            }
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

    // Checks if a world position is inside solid volumetric terrain (density > isoLevel + thresholdOffset).
    public bool IsPointInSolidTerrain(Vector3 worldPos, float thresholdOffset = 0.1f)
    {
        TerrainChunk chunk = GetChunkAtWorldPos(worldPos);
        if (chunk == null) return false;

        return chunk.IsPointInSolidTerrain(worldPos, isoLevel + thresholdOffset);
    }

    // Tracks the chunk where the player is currently located and triggers mineral generation if not already generated.
    private void TrackPlayerChunk()
    {
        if (playerTransform == null)
        {
            FindPlayer();
            if (playerTransform == null) return;
        }

        TerrainChunk currentChunk = GetChunkAtWorldPos(playerTransform.position);
        if (currentChunk != null && !currentChunk.isMineralGenerated)
        {
            GenerateMineralsInChunk(currentChunk);
            currentChunk.isMineralGenerated = true;
        }
    }

    private void FindPlayer()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
            return;
        }
    }

    // Generates mineral veins or single mineral inside the solid terrain of the given chunk
    public void GenerateMineralsInChunk(TerrainChunk chunk)
    {
        if (chunk == null || !generateMinerals) return;

        // Ensure available minerals are loaded
        if (availableMinerals == null || availableMinerals.Length == 0)
        {
            availableMinerals = Resources.LoadAll<Mineral>("Minerals");
            if (availableMinerals == null || availableMinerals.Length == 0)
            {
                Debug.LogWarning("No Mineral ScriptableObjects found in availableMinerals or Resources/Minerals.");
                return;
            }
        }

        // Read chunk density data to locate solid rock voxels
        float[] densities = chunk.GetDensityData();
        if (densities == null) return;

        // Collect solid voxels strictly inside terrain, away from air and chunk borders
        float solidThreshold = isoLevel + 0.8f;
        List<Vector3Int> solidVoxels = chunk.GetSolidVoxelIndices(densities, solidThreshold, margin: 2);
        if (solidVoxels.Count == 0)
        {
            // Chunk has no solid interior
            return;
        }

        // Calculate chunk normalized distance from spawn cavern
        Vector3 cavernCenterWorld = GetWorldCavernCenter();
        float distFromSpawn = Vector3.Distance(chunk.WorldBounds.center, cavernCenterWorld);
        float maxDist = GetMaxTerrainDistance(cavernCenterWorld);
        float normalizedDist = Mathf.Clamp01(distFromSpawn / Mathf.Max(1f, maxDist));

        int depositCount = UnityEngine.Random.Range(mineralDepositsPerChunk.x, mineralDepositsPerChunk.y + 1);

        for (int d = 0; d < depositCount; d++)
        {
            if (solidVoxels.Count == 0) break;

            // Pick mineral based on rarity vs distance from spawn
            Mineral selectedMineral = SelectMineralForDistance(normalizedDist, cachedMinRarity, cachedMaxRarity);
            if (selectedMineral == null) continue;

            // Determine if vein or single
            bool isVein = (UnityEngine.Random.value * 100f) < veinPercentage;
            int mineralCount = 1;
            if (isVein)
            {
                mineralCount = UnityEngine.Random.Range((int)selectedMineral.MinVeinAmount, (int)selectedMineral.MaxVeinAmount + 1);
                mineralCount = Mathf.Max(1, mineralCount);
            }

            // Pick a random solid voxel as the origin of the deposit
            int voxelIdx = UnityEngine.Random.Range(0, solidVoxels.Count);
            Vector3Int originVoxel = solidVoxels[voxelIdx];
            Vector3 originPos = chunk.VoxelCoordToWorldPos(originVoxel);

            // Spawn first mineral at origin
            SpawnMineralInstance(chunk, selectedMineral, originPos);

            // If vein, spawn remaining clustered nearby inside solid terrain
            for (int i = 1; i < mineralCount; i++)
            {
                Vector3 candidatePos = originPos;
                bool foundSolidSpot = false;

                // Attempt to find a nearby point that is also inside solid terrain
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    Vector3 offset = UnityEngine.Random.insideUnitSphere * veinSpreadRadius;
                    Vector3 testPos = originPos + offset;
                    if (chunk.IsPointInTerrain(testPos, densities, solidThreshold))
                    {
                        candidatePos = testPos;
                        foundSolidSpot = true;
                        break;
                    }
                }

                if (foundSolidSpot)
                {
                    SpawnMineralInstance(chunk, selectedMineral, candidatePos);
                }
            }
        }
    }

    // Selects a mineral to spawn based on its rarity relative to normalized distance from the spawn cavern.
    // Rarer minerals have very low probability near spawn and become prominent with distance.
    private Mineral SelectMineralForDistance(float normalizedDist, int minRarity, int maxRarity)
    {
        if (availableMinerals == null || availableMinerals.Length == 0) return null;

        // Target rarity smoothly increases with distance from spawn
        float targetRarity = Mathf.Lerp(minRarity, maxRarity, normalizedDist);

        List<Mineral> candidates = new List<Mineral>();
        List<float> weights = new List<float>();

        float rarityRange = Mathf.Max(1f, maxRarity - minRarity);

        for (int i = 0; i < availableMinerals.Length; i++)
        {
            Mineral m = availableMinerals[i];
            if (m == null) continue;

            float r = m.Rarity;

            // Common minerals maintain a strong natural base abundance everywhere,
            // while rare minerals have a very low baseline probability near spawn.
            float commonnessFactor = 1f - ((r - minRarity) / rarityRange); // 1.0 for most common, 0.0 for rarest
            float baseWeight = Mathf.Lerp(0.01f, 0.45f, commonnessFactor);

            // Minerals closer to the chunk's target rarity receive a bonus
            float diff = Mathf.Abs(r - targetRarity);
            float distanceBonus = Mathf.Exp(-0.5f * (diff * diff) / 4.0f);

            float weight = baseWeight + distanceBonus;

            candidates.Add(m);
            weights.Add(weight);
        }

        // Fallback to all valid minerals if gating filtered everything out
        if (candidates.Count == 0)
        {
            for (int i = 0; i < availableMinerals.Length; i++)
            {
                if (availableMinerals[i] != null) candidates.Add(availableMinerals[i]);
            }
            if (candidates.Count == 0) return null;
            return candidates[UnityEngine.Random.Range(0, candidates.Count)];
        }

        // Weighted random selection
        float totalWeight = 0f;
        for (int i = 0; i < weights.Count; i++) totalWeight += weights[i];

        float roll = UnityEngine.Random.value * totalWeight;
        float cumulative = 0f;
        for (int i = 0; i < candidates.Count; i++)
        {
            cumulative += weights[i];
            if (roll <= cumulative)
            {
                return candidates[i];
            }
        }

        return candidates[candidates.Count - 1];
    }

    private void SpawnMineralInstance(TerrainChunk chunk, Mineral mineral, Vector3 worldPos)
    {
        if (mineral == null) return;

        if (!mineralPrefabCache.TryGetValue(mineral.MineralName, out GameObject prefabToUse))
        {
            prefabToUse = Resources.Load<GameObject>($"Prefabs/Mineral/{mineral.MineralName}");
            if (prefabToUse != null)
            {
                mineralPrefabCache[mineral.MineralName] = prefabToUse;
            }
        }

        if (prefabToUse == null) return;

        // Add a slight random jitter within half a voxel so minerals don't align on a grid
        float jitter = voxelSize * 0.35f;
        Vector3 jitterOffset = new Vector3(
            UnityEngine.Random.Range(-jitter, jitter),
            UnityEngine.Random.Range(-jitter, jitter),
            UnityEngine.Random.Range(-jitter, jitter)
        );
        Vector3 finalPos = worldPos + jitterOffset;
        Quaternion rotation = UnityEngine.Random.rotation;

        GameObject obj = Instantiate(prefabToUse, finalPos, rotation, chunk.transform);
        obj.name = $"{mineral.MineralName}_{chunk.chunkCoord.x}_{chunk.chunkCoord.y}_{chunk.chunkCoord.z}";
        obj.layer = LayerMaskToLayer(mineralLayer, "Mineral", 9);

        // Ensure kinematic state immediately 
        if (obj.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = true;
        }

        // Apply scale within mineral's scale range
        Vector2 scaleRange = mineral.ScaleRange;
        if (scaleRange.x <= 0f) scaleRange.x = 1f;
        if (scaleRange.y < scaleRange.x) scaleRange.y = scaleRange.x;
        float scale = UnityEngine.Random.Range(scaleRange.x, scaleRange.y);
        obj.transform.localScale = Vector3.one * scale;

        // Configure MineralBehaviour
        if (obj.TryGetComponent<MineralBehaviour>(out var mb))
        {
            mb.SetMineralData(mineral);
        }
    }

    private float GetMaxTerrainDistance(Vector3 referencePos)
    {
        Vector3 terrainSize = new Vector3(
            chunkDimensions.x * chunkSize * voxelSize,
            chunkDimensions.y * chunkSize * voxelSize,
            chunkDimensions.z * chunkSize * voxelSize
        );
        Vector3 min = transform.position;
        Vector3 max = transform.position + terrainSize;

        // Test 8 corners of terrain bounding box
        float maxDist = 0f;
        Vector3[] corners = new Vector3[8]
        {
            new Vector3(min.x, min.y, min.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(min.x, max.y, max.z),
            new Vector3(max.x, max.y, max.z)
        };

        for (int i = 0; i < 8; i++)
        {
            float d = Vector3.Distance(referencePos, corners[i]);
            if (d > maxDist) maxDist = d;
        }

        return Mathf.Max(maxDist, 1f);
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

        mineralPrefabCache.Clear();
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
