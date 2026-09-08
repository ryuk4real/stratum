using UnityEngine;
using UnityEngine.InputSystem;

// Demo component to test terrain digging
public class PlayerTerrainDigger : MonoBehaviour
{
    [Header("Dig Parameters")]
    [SerializeField, Min(0f)] private float digRadius = 2.5f;
    [SerializeField, Min(0f)] private float digStrength = 2.0f;
    [SerializeField, Min(0f)] private float maxReachDistance = 20.0f;

    [Header("Targeting & Layers")]
    [Tooltip("Only colliders on layers included in this mask can be dug.")]
    [SerializeField] private LayerMask terrainLayerMask;
    [Tooltip("Layers that block the digging raycast (Default is Everything, so walls and boundary colliders stop the ray).")]
    [SerializeField] private LayerMask raycastBlockersMask = ~0;

    [SerializeField] private Camera playerCamera;

    private void OnValidate()
    {
        if (digRadius < 0f) digRadius = 0f;
        if (digStrength < 0f) digStrength = 0f;
        if (maxReachDistance < 0f) maxReachDistance = 0f;
    }

    private void Start()
    {
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        // Default to 'Terrain' layer if not set
        if (terrainLayerMask.value == 0)
        {
            int terrainLayer = LayerMask.NameToLayer("Terrain");
            if (terrainLayer != -1)
            {
                terrainLayerMask = 1 << terrainLayer;
            }
        }
    }

    private void Update()
    {
        if (playerCamera == null) return;

        // Left click to dig
        if (Mouse.current != null && Mouse.current.leftButton.isPressed)
        {
            PerformDigAction();
        }
    }

    private void PerformDigAction()
    {
        Ray ray = Cursor.lockState == CursorLockMode.Locked
            ? playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f))
            : playerCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

        // Raycast against all blocking geometry (walls, colliders, boundaries, terrain)
        if (Physics.Raycast(ray, out RaycastHit hit, maxReachDistance, raycastBlockersMask))
        {
            // Can dig only if the hit collider's layer is in terrainLayerMask
            if (((1 << hit.collider.gameObject.layer) & terrainLayerMask.value) != 0)
            {
                if (!IsDigNearNonDiggable(hit.point, digRadius))
                {
                    TerrainManager.Instance.ModifyTerrain(hit.point, digRadius, digStrength);

                }
            }
        }
    }

    private bool IsDigNearNonDiggable(Vector3 point, float radius)
    {
        Collider[] colliders = Physics.OverlapSphere(point, radius);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i].GetComponent<NonDiggableZone>() != null)
            {
                return true;
            }
        }
        return false;
    }

    // Method to dig at a specific point
    public void DigAtPoint(Vector3 worldPoint, float radius, float strength)
    {
        if (IsDigNearNonDiggable(worldPoint, radius)) return;

        if (TerrainManager.Instance != null)
        {
            TerrainManager.Instance.ModifyTerrain(worldPoint, radius, strength);
        }
    }
}
