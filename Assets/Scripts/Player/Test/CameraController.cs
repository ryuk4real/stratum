using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    [SerializeField] private float mouseSensitivity = 0.15f;
    [SerializeField] private Transform playerBody;

    private float xRotation = 0f;

    void Start()
    {
        // Lock cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (playerBody == null && transform.parent != null)
        {
            playerBody = transform.parent;
        }

        // Avoid roll/pitch on player body, keeping only horizontal y-axis rotation
        if (playerBody != null)
        {
            playerBody.rotation = Quaternion.Euler(0f, playerBody.eulerAngles.y, 0f);
        }

        // Initialize vertical camera rotation from current orientation
        xRotation = transform.localEulerAngles.x;
        if (xRotation > 180f) xRotation -= 360f;
    }

    void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        Vector2 mouseDelta = mouse.delta.ReadValue() * mouseSensitivity;

        // Camera rotates vertically (pitch)
        xRotation -= mouseDelta.y;
        xRotation = Mathf.Clamp(xRotation, -89f, 89f);
        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        // Player body rotates horizontally (yaw)
        if (playerBody != null)
        {
            playerBody.Rotate(Vector3.up * mouseDelta.x, Space.World);
        }
    }
}
