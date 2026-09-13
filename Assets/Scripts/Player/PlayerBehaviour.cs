using UnityEngine;

/// <summary>
/// Manages player behavior in VR environment
/// </summary>
public class PlayerBehaviour : MonoBehaviour
{
    [Header("Components")]
    [Tooltip("Reference to the CharacterController.")]
    [SerializeField] private CharacterController characterController;

    [Tooltip("Reference to OVRCameraRig.")]
    [SerializeField] private OVRCameraRig cameraRig;

    [Header("Crouch Parameters")]
    [Tooltip("How much the player lowers down when crouching.")]
    [SerializeField, Min(0.2f)] private float crouchHeightOffset = 0.6f;

    [Tooltip("Transition speed into and out of crouch.")]
    [SerializeField, Min(1f)] private float crouchTransitionSpeed = 6f;

    // Internal state
    private bool isCrouched = false;
    private float currentCrouchOffset = 0f;
    private float targetCrouchOffset = 0f;
    private Vector3 initialTrackingSpaceLocalPos;
    private float standingCharacterHeight = 1.5f;
    private Vector3 initialCharacterCenter = new Vector3(0, 0.75f, 0);

    private void Awake()
    {
        if (characterController != null)
        {
            standingCharacterHeight = characterController.height;
            initialCharacterCenter = characterController.center;
        }

        if (cameraRig != null && cameraRig.trackingSpace != null)
        {
            initialTrackingSpaceLocalPos = cameraRig.trackingSpace.localPosition;
        }
    }

    private void Update()
    {
        HandleCrouchInput();
    }

    private void HandleCrouchInput()
    {
        if (OVRInput.GetDown(OVRInput.Button.PrimaryThumbstick))
        {
            SetCrouch(!isCrouched);
        }

        UpdateCrouchTransition();
    }

    private void UpdateCrouchTransition()
    {
        currentCrouchOffset = Mathf.MoveTowards(
            currentCrouchOffset,
            targetCrouchOffset,
            crouchTransitionSpeed * Time.deltaTime
        );

        // Lower the camera tracking space smoothly
        if (cameraRig != null && cameraRig.trackingSpace != null)
        {
            Vector3 pos = initialTrackingSpaceLocalPos;
            pos.y -= currentCrouchOffset;
            cameraRig.trackingSpace.localPosition = pos;
        }

        // Shrink the CharacterController collider smoothly keeping the bottom grounded
        if (characterController != null)
        {
            float targetHeight = Mathf.Max(0.5f, standingCharacterHeight - currentCrouchOffset);
            characterController.height = targetHeight;

            Vector3 center = initialCharacterCenter;
            center.y = targetHeight * 0.5f;
            characterController.center = center;
        }
    }

    public void SetCrouch(bool crouch)
    {
        isCrouched = crouch;
        targetCrouchOffset = isCrouched ? crouchHeightOffset : 0f;
    }
}
