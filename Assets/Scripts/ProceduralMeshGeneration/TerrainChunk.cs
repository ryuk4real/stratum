using System;
using UnityEngine;

/// <summary>
/// Represents a single volumetric chunk of the terrain.
/// Manages local density, mesh generation via Marching Cubes and the collider.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class TerrainChunk : MonoBehaviour, IDisposable
{
    [Header("Chunk State")]
    public Vector3Int chunkCoord;
    public int chunkSize;
    public float voxelSize;
    public int numPointsPerAxis; // chunkSize + 1 per halo cells

    private ComputeBuffer densityBuffer;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private MeshCollider meshCollider;
    private Mesh mesh;

    private bool isInitialized = false;

    public Bounds WorldBounds { get; private set; }

    public void InitializeChunk(Vector3Int coord, int size, float vSize, Material material, PhysicsMaterial physMaterial = null)
    {
        chunkCoord = coord;
        chunkSize = size;
        voxelSize = vSize;
        numPointsPerAxis = chunkSize + 1;

        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        meshCollider = GetComponent<MeshCollider>();

        if (meshCollider != null)
        {
            // The collider is set to concave to support holes in the terrain
            meshCollider.convex = false;
            if (physMaterial != null)
            {
                meshCollider.sharedMaterial = physMaterial;
            }
        }

        if (mesh == null)
        {
            mesh = new Mesh();
            mesh.name = $"ChunkMesh_{coord.x}_{coord.y}_{coord.z}";
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            meshFilter.sharedMesh = mesh;
        }

        if (material != null)
        {
            meshRenderer.sharedMaterial = material;
        }

        int totalPoints = numPointsPerAxis * numPointsPerAxis * numPointsPerAxis;

        if (densityBuffer != null)
        {
            densityBuffer.Release();
        }
        
        densityBuffer = new ComputeBuffer(totalPoints, sizeof(float));

        Vector3 worldMin = GetWorldPosition();
        Vector3 worldSize = Vector3.one * (chunkSize * voxelSize);
        WorldBounds = new Bounds(worldMin + worldSize * 0.5f, worldSize);

        isInitialized = true;
    }

    public Vector3 GetWorldPosition()
    {
        return transform.position;
    }

    // Generates the initial density of the chunk on GPU for an underground quarry with a starting cavern
    public void GenerateDensityGPU(ComputeShader shader, int kernelGenerateDensity, Vector3 cavernCenter, Vector3 cavernSize, float noiseFreq, float variation)
    {
        if (!isInitialized || densityBuffer == null) return;

        Vector3 worldPos = GetWorldPosition();
        shader.SetBuffer(kernelGenerateDensity, "_DensityBuffer", densityBuffer);
        shader.SetInt("_NumPointsPerAxis", numPointsPerAxis);
        shader.SetFloat("_VoxelSize", voxelSize);
        shader.SetVector("_ChunkWorldOffset", worldPos);
        shader.SetVector("_CavernCenter", cavernCenter);
        shader.SetVector("_CavernSize", cavernSize);
        shader.SetFloat("_NoiseFrequency", noiseFreq);
        shader.SetFloat("_TerrainHeightVariation", variation);

        int threadsPerAxis = Mathf.CeilToInt(numPointsPerAxis / 4.0f); // Each thread group has 4x4x4 threads
        shader.Dispatch(kernelGenerateDensity, threadsPerAxis, threadsPerAxis, threadsPerAxis);
    }

    // Adds or removes density in a spherical region around the given world center position
    public bool ModifyDensity(Vector3 worldCenter, float radius, float delta, ComputeShader shader, int kernelModify)
    {
        if (!isInitialized || densityBuffer == null) return false;

        float maxExtent = (chunkSize + 1) * voxelSize;
        Bounds expandedBounds = new Bounds(WorldBounds.center, Vector3.one * (maxExtent + radius * 2f));
        if (!expandedBounds.Contains(worldCenter) && Vector3.Distance(WorldBounds.ClosestPoint(worldCenter), worldCenter) > radius)
        {
            return false;
        }

        Vector3 worldPos = GetWorldPosition();
        shader.SetBuffer(kernelModify, "_DensityBuffer", densityBuffer);
        shader.SetInt("_NumPointsPerAxis", numPointsPerAxis);
        shader.SetFloat("_VoxelSize", voxelSize);
        shader.SetVector("_ChunkWorldOffset", worldPos);
        shader.SetVector("_BrushCenter", worldCenter);
        shader.SetFloat("_BrushRadius", radius);
        shader.SetFloat("_BrushDelta", delta);

        int threadsPerAxis = Mathf.CeilToInt(numPointsPerAxis / 4.0f);
        shader.Dispatch(kernelModify, threadsPerAxis, threadsPerAxis, threadsPerAxis);
        return true;
    }

    // Executes the Marching Cubes algorithm on GPU and reconstructs the chunk mesh
    public void Polygonise(ComputeShader shader, int kernelMC, ComputeBuffer triangleBuffer, ComputeBuffer counterBuffer, ComputeBuffer triTableBuffer, ComputeBuffer edgeTableBuffer, int maxTriangles, float isoLevel, Vector3 cavernCenter, Vector3 cavernSize, float noiseFreq = 0.03f, float variation = 2f)
    {
        if (!isInitialized || densityBuffer == null) return;

        // Reset counter
        int[] resetCounter = new int[] { 0 };
        counterBuffer.SetData(resetCounter);

        Vector3 worldPos = GetWorldPosition();
        shader.SetBuffer(kernelMC, "_DensityBuffer", densityBuffer);
        shader.SetBuffer(kernelMC, "_Triangles", triangleBuffer);
        shader.SetBuffer(kernelMC, "_TriangleCounter", counterBuffer);
        shader.SetBuffer(kernelMC, "_TriTable", triTableBuffer);
        shader.SetBuffer(kernelMC, "_EdgeTable", edgeTableBuffer);
        shader.SetInt("_NumPointsPerAxis", numPointsPerAxis);
        shader.SetInt("_MaxTriangles", maxTriangles);
        shader.SetFloat("_VoxelSize", voxelSize);
        shader.SetVector("_ChunkWorldOffset", worldPos);
        shader.SetFloat("_IsoLevel", isoLevel);
        shader.SetVector("_CavernCenter", cavernCenter);
        shader.SetVector("_CavernSize", cavernSize);
        shader.SetFloat("_NoiseFrequency", noiseFreq);
        shader.SetFloat("_TerrainHeightVariation", variation);

        int threadsPerAxis = Mathf.CeilToInt(chunkSize / 4.0f);
        shader.Dispatch(kernelMC, threadsPerAxis, threadsPerAxis, threadsPerAxis);

        // Reads the number of actual triangles from the GPU
        counterBuffer.GetData(resetCounter);

        int numTriangles = Mathf.Min(resetCounter[0], maxTriangles);

        if (numTriangles <= 0)
        {
            mesh.Clear();
            if (meshCollider != null)
            {
                meshCollider.sharedMesh = null;
                meshCollider.enabled = false;
            }
            return;
        }

        // Reads the triangles from the GPU
        GPUTriangle[] triangles = new GPUTriangle[numTriangles];
        triangleBuffer.GetData(triangles, 0, 0, numTriangles);

        int numVertices = numTriangles * 3;
        Vector3[] vertices = new Vector3[numVertices];
        Vector3[] normals = new Vector3[numVertices];
        int[] meshIndices = new int[numVertices];

        for (int i = 0; i < numTriangles; i++)
        {
            int idx = i * 3;
            vertices[idx] = triangles[i].a.position;
            vertices[idx + 1] = triangles[i].b.position;
            vertices[idx + 2] = triangles[i].c.position;

            normals[idx] = triangles[i].a.normal;
            normals[idx + 1] = triangles[i].b.normal;
            normals[idx + 2] = triangles[i].c.normal;

            meshIndices[idx] = idx;
            meshIndices[idx + 1] = idx + 1;
            meshIndices[idx + 2] = idx + 2;
        }

        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetTriangles(meshIndices, 0);
        mesh.RecalculateBounds();

        if (meshCollider != null)
        {
            meshCollider.sharedMesh = mesh;
            meshCollider.enabled = true;
        }
    }

    public void SetColliderEnabled(bool isColliderEnabled)
    {
        if (meshCollider != null)
        {
            meshCollider.enabled = isColliderEnabled;
        }
    }

    public void SetVisibility(bool isVisible)
    {
        if (meshRenderer != null)
        {
            meshRenderer.enabled = isVisible;
        }
    }

    public bool IsVisible => meshRenderer != null && meshRenderer.enabled;

    public void Dispose()
    {
        if (densityBuffer != null)
        {
            densityBuffer.Release();
            densityBuffer = null;
        }

        if (mesh != null)
        {
            Destroy(mesh);
            mesh = null;
        }
    }

    private void OnDestroy()
    {
        Dispose();
    }
}
