using System.Collections.Generic;
using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;

/// <summary>
/// Controls VR digging interactions on a pickaxe.
/// </summary>
[DisallowMultipleComponent]
public class PickaxeController : MonoBehaviour
{
    [Header("Dig Parameters")]
    [Tooltip("Radius of terrain deformation.")]
    [SerializeField, Min(0.2f)] private float digRadius = 1.0f;

    [Tooltip("Strength of terrain deformation per hit.")]
    [SerializeField, Min(0.1f)] private float digStrength = 0.8f;

    [Tooltip("Extra reach distance along the swing/blade direction to ensure comfortable hits.")]
    [SerializeField, Min(0f)] private float reachDistance = 0.45f;

    [Tooltip("Radius of the sweep sphere checking for terrain contact.")]
    [SerializeField, Min(0.01f)] private float sphereCastRadius = 0.12f;

    [Header("Swing Detection")]
    [Tooltip("Minimum velocity (m/s) required to trigger a dig.")]
    [SerializeField, Min(0.1f)] private float minSwingSpeed = 0.4f;

    [Tooltip("Minimum time (seconds) between consecutive dig hits to prevent multiple triggers in a single swing.")]
    [SerializeField, Min(0.05f)] private float hitCooldown = 0.25f;

    [Tooltip("If true, the pickaxe will only dig when held.")]
    [SerializeField] private bool requireGrabToDig = true;

    [Header("Targeting & Layers")]
    [Tooltip("Layer mask representing modifiable terrain.")]
    [SerializeField] private LayerMask terrainLayerMask;

    [Header("Blade Tips")]
    [Tooltip("Transforms representing the pickaxe blade tips. Must be assigned in the Inspector.")]
    [SerializeField] private Transform[] tipTransforms;

    [Header("References")]
    [Tooltip("Grabbable component used to verify if the tool is currently held.")]
    [SerializeField] private Grabbable grabbable;

    [Header("Audio")]
    [Tooltip("Audio source on the pickaxe used to play the hit sound.")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("Audio clip played when pickaxe hits the terrain.")]
    [SerializeField] private AudioClip hitSound;

    [Tooltip("Volume of the pickaxe hit sound.")]
    [SerializeField, Range(0f, 1f)] private float hitSoundVolume = 0.9f;

    [Header("Gizmos")]
    [Tooltip("Draw reach and collision gizmos in the Scene view.")]
    [SerializeField] private bool showGizmos = true;

    // Tip tracking state
    private List<Transform> activeTips = new List<Transform>();
    private Vector3[] previousTipPositions;
    private Vector3[] currentTipVelocities;
    private float recentPeakSpeed = 0f;
    private float nextAllowedHitTime = 0f;
    private Rigidbody pickaxeRigidbody;

    // Pickup cooldown state
    private float grabCooldownDuration = 0.4f;
    private float grabCooldownUntil = 0f;
    private bool wasGrabbedLastFrame = false;

    // Cache of pickaxe colliders to ignore during sweeps
    private HashSet<Collider> ownColliders = new HashSet<Collider>();

    private void Awake()
    {
        pickaxeRigidbody = GetComponent<Rigidbody>();

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        // Cache all own colliders to prevent collisions with player
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            ownColliders.Add(colliders[i]);
        }

        InitializeTips();
    }

    private void InitializeTips()
    {
        activeTips.Clear();

        for (int i = 0; i < tipTransforms.Length; i++)
        {
            if (tipTransforms[i] != null && !activeTips.Contains(tipTransforms[i]))
            {
                activeTips.Add(tipTransforms[i]);
            }
        }

        int count = activeTips.Count;
        previousTipPositions = new Vector3[count];
        currentTipVelocities = new Vector3[count];

        for (int i = 0; i < count; i++)
        {
            previousTipPositions[i] = activeTips[i].position;
            currentTipVelocities[i] = Vector3.zero;
        }
    }

    private void Update()
    {
        bool currentlyGrabbed = IsGrabbed();

        if (currentlyGrabbed && !wasGrabbedLastFrame)
        {
            OnPickedUp();
        }

        wasGrabbedLastFrame = currentlyGrabbed;

        CalculateTipVelocities();

        if (CanAttemptDig())
        {
            CheckTerrainSweeps();
        }
    }

    private void OnPickedUp()
    {
        // Enforce cooldown right after pickup, prevents digging right after grabbing
        grabCooldownUntil = Time.time + grabCooldownDuration;
        nextAllowedHitTime = Mathf.Max(nextAllowedHitTime, grabCooldownUntil);
        recentPeakSpeed = 0f;

        // Reset previous tip positions to prevent pickaxe velocity spikes when picking up
        for (int i = 0; i < activeTips.Count; i++)
        {
            if (activeTips[i] != null)
            {
                previousTipPositions[i] = activeTips[i].position;
                currentTipVelocities[i] = Vector3.zero;
            }
        }
    }

    private void CalculateTipVelocities()
    {
        float dt = Time.deltaTime;

        // If the frame time is too short, skip velocity calculation to prevent spikes
        if (dt <= 0.0001f) return;

        float maxInstantSpeed = 0f;

        for (int i = 0; i < activeTips.Count; i++)
        {
            if (activeTips[i] == null) continue;

            Vector3 currentPos = activeTips[i].position;
            Vector3 vel = (currentPos - previousTipPositions[i]) / dt;
            currentTipVelocities[i] = vel;
            previousTipPositions[i] = currentPos;

            float speed = vel.magnitude;
            if (speed > maxInstantSpeed)
            {
                maxInstantSpeed = speed;
            }
        }

        // Buffer recent peak speed so deceleration upon impact does not invalidate the swing
        if (maxInstantSpeed > recentPeakSpeed)
        {
            recentPeakSpeed = maxInstantSpeed;
        }
        else
        {
            recentPeakSpeed = Mathf.MoveTowards(recentPeakSpeed, maxInstantSpeed, dt * 6f);
        }
    }

    private bool IsGrabbed()
    {
        if (!requireGrabToDig) return true;

        // When picked up, the rigidbody is made kinematic by Grabbable in Meta SDK
        if (pickaxeRigidbody != null && pickaxeRigidbody.isKinematic) return true;

        // Also check if the grabbable detect any closed hands
        if (grabbable != null && grabbable.SelectingPointsCount > 0) return true;

        return false;
    }

    private bool CanAttemptDig()
    {
        if (Time.time < grabCooldownUntil) return false;
        if (Time.time < nextAllowedHitTime) return false;
        if (!IsGrabbed()) return false;
        if (recentPeakSpeed < minSwingSpeed) return false;

        return true;
    }

    // Checks for terrain collisions along the active swing trajectory for the two tips
    private void CheckTerrainSweeps()
    {
        for (int i = 0; i < activeTips.Count; i++)
        {
            if (activeTips[i] == null) continue;

            Vector3 currentPos = activeTips[i].position;
            Vector3 vel = currentTipVelocities[i];
            float speed = vel.magnitude;

            // Tip must have sufficient instantaneous swing speed to register as a hit
            if (speed < minSwingSpeed) continue;

            Vector3 swingDir = vel / speed;
            Vector3 prevPos = previousTipPositions[i];
            float dist = Vector3.Distance(prevPos, currentPos) + reachDistance;

            // Continuous sphere cast along the actual swing direction from previous to current position
            if (Physics.SphereCast(prevPos, sphereCastRadius, swingDir, out RaycastHit hit, dist, terrainLayerMask))
            {
                if (!ownColliders.Contains(hit.collider))
                {
                    // Must be striking into the terrain surface
                    if (Vector3.Dot(swingDir, -hit.normal) > 0.2f)
                    {
                        ExecuteDig(hit.point, hit.normal);
                        return;
                    }
                }
            }
        }
    }

    private void ExecuteDig(Vector3 worldPoint, Vector3 normal)
    {
        if (IsDigNearNonDiggable(worldPoint, digRadius))
        {
            return;
        }

        TerrainManager.Instance.ModifyTerrain(worldPoint, digRadius, digStrength);
        nextAllowedHitTime = Time.time + hitCooldown;

        PlayHitAudio();
    }

    private void PlayHitAudio()
    {
        if (audioSource != null && hitSound != null)
        {
            audioSource.PlayOneShot(hitSound, hitSoundVolume);
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

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos || tipTransforms == null) return;

        for (int i = 0; i < tipTransforms.Length; i++)
        {
            if (tipTransforms[i] == null) continue;
            Vector3 tipPos = tipTransforms[i].position;
            Vector3 bladeOutward = (tipPos - transform.position).normalized;

            // Tip contact sphere
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(tipPos, sphereCastRadius);

            // Reach extension along the pickaxe blade
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(tipPos, bladeOutward * reachDistance);
            Gizmos.DrawWireSphere(tipPos + bladeOutward * reachDistance, sphereCastRadius);
        }
    }
}
