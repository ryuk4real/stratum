using UnityEngine;
using UnityEngine.InputSystem;

namespace ProceduralMeshGeneration
{
    // Demo component to test terrain digging
    public class PlayerTerrainDigger : MonoBehaviour
    {
        [Header("Dig Parameters")]
        [SerializeField, Min(0f)] private float digRadius = 2.5f;
        [SerializeField, Min(0f)] private float digStrength = 2.0f;
        [SerializeField, Min(0f)] private float maxReachDistance = 20.0f;
        [SerializeField] private LayerMask terrainLayerMask = ~0;
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

            if (Physics.Raycast(ray, out RaycastHit hit, maxReachDistance, terrainLayerMask))
            {
                if (TerrainManager.Instance != null)
                {
                    TerrainManager.Instance.ModifyTerrain(hit.point, digRadius, digStrength);
                }
            }
        }

        // Method to dig at a specific point
        public void DigAtPoint(Vector3 worldPoint, float radius, float strength)
        {
            if (TerrainManager.Instance != null)
            {
                TerrainManager.Instance.ModifyTerrain(worldPoint, radius, strength);
            }
        }
    }
}
