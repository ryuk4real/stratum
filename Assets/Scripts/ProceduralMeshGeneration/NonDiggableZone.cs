using UnityEngine;

/// <summary>
/// Marks a collider as indestructible. Used for boundary walls.
/// </summary>
[RequireComponent(typeof(Collider))]
public class NonDiggableZone : MonoBehaviour
{
    [Header("Gizmo Settings")]
    [SerializeField] private Color gizmoColor = new Color(1f, 0.2f, 0.2f, 0.25f);
    [SerializeField] private Color wireGizmoColor = new Color(1f, 0.2f, 0.2f, 0.8f);
    [SerializeField] private bool drawGizmo = true;

    private void OnDrawGizmos()
    {
        if (!drawGizmo) return;

        Collider col = GetComponent<Collider>();
        
        if (col == null) return;

        if (col is not BoxCollider box)
        {
            Debug.LogError("NonDiggableZone can only be placed on BoxColliders");
            return;
        }

        Matrix4x4 oldMatrix = Gizmos.matrix;
        Color oldColor = Gizmos.color;

        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = gizmoColor;
        Gizmos.DrawCube(box.center, box.size);

        Gizmos.color = wireGizmoColor;
        Gizmos.DrawWireCube(box.center, box.size);

        Gizmos.matrix = oldMatrix;
        Gizmos.color = oldColor;
    }
}
