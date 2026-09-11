using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;

/// <summary>
/// Controls the mineral's physical behavior and hand extraction
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(Collider))]
[RequireComponent(typeof(Rigidbody), typeof(Grabbable), typeof(HandGrabInteractable))]
public class MineralBehaviour : MonoBehaviour
{
    [Header("Mineral Data")]
    [SerializeField] private Mineral mineralData;

    [Header("Extraction Settings")]

    [Tooltip("Percentage of raycasts exposed to air required before the mineral can be grabbed by hand.")]
    [SerializeField, Range(0.3f, 0.95f)] private float exposureThreshold = 0.75f;

    [Tooltip("Percentage of raycasts exposed to air at which the mineral is considered completely free from rock and falls due to gravity.")]
    [SerializeField, Range(0.85f, 1.0f)] private float fullExcavationThreshold = 1.0f;

    [Tooltip("Number of raycasts distributed around the mineral's volume.")]
    [SerializeField, Range(8, 64)] private int raycastCount = 20;

    [Tooltip("Raycast distance from the center.")]
    [SerializeField, Range(0.02f, 0.5f)] private float raycastDistance = 0.08f;

    [Header("VR Haptics")]
    [Tooltip("Trigger controller vibration when successfully extracting the mineral.")]
    [SerializeField] private bool enableHaptics = true;
    [SerializeField, Range(0.1f, 1f)] private float hapticStrength = 0.7f;
    [SerializeField, Range(0.05f, 0.5f)] private float hapticDuration = 0.2f;


    [Header("Debug & State")]
    [SerializeField] private MineralExtractionState state = MineralExtractionState.Buried;
    [Tooltip("Ratio of exposed raycasts.")]
    [SerializeField, Range(0f, 1f)] private float exposureRatio = 0f;
    [SerializeField] private bool isAnchoredToTerrain = true;
    [SerializeField] private bool drawGizmos = true;

    // Components
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Collider mineralCollider;
    private Rigidbody mineralRigidbody;
    private Grabbable grabbable;
    private HandGrabInteractable handGrabInteractable;

    // Raycasts
    private Vector3[] localRayDirections;
    private bool[] raycastExposed;
    private readonly RaycastHit[] raycastHits = new RaycastHit[8];

    // Public getters
    public Mineral MineralData => mineralData;
    public MineralExtractionState State => state;
    public bool IsExtracted => state == MineralExtractionState.Extracted;
    public bool IsExtractable => state == MineralExtractionState.Extractable;
    public bool IsAnchoredToTerrain => isAnchoredToTerrain;
    public float ExposureRatio => exposureRatio;

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        mineralCollider = GetComponent<Collider>();
        mineralRigidbody = GetComponent<Rigidbody>();
        grabbable = GetComponent<Grabbable>();
        handGrabInteractable = GetComponent<HandGrabInteractable>();

        // Enforce kinematic and buried state
        state = MineralExtractionState.Buried;
        isAnchoredToTerrain = true;
        mineralRigidbody.isKinematic = true;
        mineralRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        mineralRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        grabbable.ForceKinematicDisabled = true;

        ApplyMineralData();
        GenerateRaycastDirections();

        SetInteractionEnabled(false);
    }

    private void Start()
    {
        
    }

    private void OnEnable()
    {
        if (grabbable != null)
        {
            grabbable.WhenPointerEventRaised += HandlePointerEvent;
        }
    }

    private void OnDisable()
    {
        if (grabbable != null)
        {
            grabbable.WhenPointerEventRaised -= HandlePointerEvent;
        }
    }

    private void HandlePointerEvent(PointerEvent evt)
    {
        if (evt.Type == PointerEventType.Select)
        {
            if (state == MineralExtractionState.Extractable)
            {
                Extract();
            }
        }
        else if (evt.Type == PointerEventType.Unselect || evt.Type == PointerEventType.Cancel)
        {
            // When the hand releases the mineral, make it fully dynamic (non-kinematic)
            if (state == MineralExtractionState.Extracted || !isAnchoredToTerrain)
            {
                if (mineralRigidbody != null) mineralRigidbody.isKinematic = false;
            }
        }
    }

    private void OnValidate()
    {
        if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
        if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
        if (mineralCollider == null) mineralCollider = GetComponent<Collider>();
        if (mineralRigidbody == null) mineralRigidbody = GetComponent<Rigidbody>();
        if (grabbable == null) grabbable = GetComponent<Grabbable>();
        if (handGrabInteractable == null) handGrabInteractable = GetComponent<HandGrabInteractable>();

        ApplyMineralData();
        GenerateRaycastDirections();
    }


    public void SetMineralData(Mineral data)
    {
        mineralData = data;
        if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
        if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
        ApplyMineralData();
    }

    public void ApplyMineralData()
    {
        if (mineralData == null) return;

        if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
        if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();

        if (meshFilter != null && mineralData.Mesh != null)
        {
            meshFilter.sharedMesh = mineralData.Mesh;
        }

        if (meshRenderer != null && mineralData.Material != null)
        {
            meshRenderer.sharedMaterial = mineralData.Material;
        }
    }

    /// Distributes raycast directions evenly on a unit sphere using Fibonacci spiral.
    private void GenerateRaycastDirections()
    {
        localRayDirections = new Vector3[raycastCount];
        raycastExposed = new bool[raycastCount];

        // Golden spiral on sphere surface
        float goldenRatioAngle = Mathf.PI * (3f - Mathf.Sqrt(5f)); // ~2.399963 radians

        for (int i = 0; i < raycastCount; i++)
        {
            float y = 1f - (i / (float)(raycastCount - 1)) * 2f; // from 1 down to -1
            float radiusAtY = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            float theta = goldenRatioAngle * i;

            float x = Mathf.Cos(theta) * radiusAtY;
            float z = Mathf.Sin(theta) * radiusAtY;

            localRayDirections[i] = new Vector3(x, y, z);
            raycastExposed[i] = false;
        }
    }

    // Casts rays outward from the center against terrain to calculate current exposure.
    // Called when the player digs near the mineral with the pickaxe.
    public void CheckExposure()
    {
        if (state == MineralExtractionState.Extracted) return;

        if (localRayDirections == null || localRayDirections.Length != raycastCount)
        {
            GenerateRaycastDirections();
        }

        int exposedCount = 0;
        float scale = Mathf.Max(transform.lossyScale.x, Mathf.Max(transform.lossyScale.y, transform.lossyScale.z));
        float effectiveRayDistance = raycastDistance * scale;

        if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
        Vector3 localCenter = (meshFilter != null && meshFilter.sharedMesh != null) ? meshFilter.sharedMesh.bounds.center : Vector3.zero;
        Vector3 worldCenter = transform.TransformPoint(localCenter);

        bool prevBackfaces = Physics.queriesHitBackfaces;
        Physics.queriesHitBackfaces = true;

        for (int i = 0; i < raycastCount; i++)
        {
            Vector3 rayDir = transform.TransformDirection(localRayDirections[i]);
            Vector3 samplePoint = worldCenter + rayDir * effectiveRayDistance;

            // Raycast check against terrain collider (if a polygon is directly intercepted)
            int hitCount = Physics.RaycastNonAlloc(worldCenter, rayDir, raycastHits, effectiveRayDistance, ~0, QueryTriggerInteraction.Ignore);
            bool hitTerrain = false;
            for (int h = 0; h < hitCount; h++)
            {
                Collider col = raycastHits[h].collider;
                if (col == null || col == mineralCollider || col.transform.IsChildOf(transform)) continue;

                if (col.TryGetComponent<TerrainChunk>(out _) || col.GetComponentInParent<TerrainChunk>() != null || col.CompareTag("Terrain"))
                {
                    hitTerrain = true;
                    break;
                }
            }

            // We check the true volumetric density at the sample point so that solid rock is never mistaken for air.
            bool isSolidTerrain = hitTerrain;
            if (!isSolidTerrain)
            {
                isSolidTerrain = TerrainManager.Instance.IsPointInSolidTerrain(samplePoint);
            }

            // Direction is exposed to air only if it is not inside solid terrain
            raycastExposed[i] = !isSolidTerrain;
            if (raycastExposed[i])
            {
                exposedCount++;
            }
        }

        Physics.queriesHitBackfaces = prevBackfaces;

        exposureRatio = (float) exposedCount / raycastCount;

        // No raycast intercepts terrain, so mineral has fallen
        if (exposedCount == raycastCount || exposureRatio >= fullExcavationThreshold)
        {
            Extract();

            // Stops being kinematic so gravity causes it to drop
            if (mineralRigidbody != null && (grabbable == null || grabbable.SelectingPointsCount == 0))
            {
                mineralRigidbody.isKinematic = false;
            }
        }
        // Sufficiently exposed to be grabbed by hand, but still anchored to rock
        else if (exposureRatio >= exposureThreshold)
        {
            isAnchoredToTerrain = true;

            // Remains kinematic until grasped by the player hand
            if (mineralRigidbody != null && (grabbable == null || grabbable.SelectingPointsCount == 0))
            {
                mineralRigidbody.isKinematic = true;
            }

            grabbable.ForceKinematicDisabled = true;

            SetInteractionEnabled(true);
            SetState(MineralExtractionState.Extractable);
        }
        else // Mostly buried in rock
        {
            isAnchoredToTerrain = true;

            if (mineralRigidbody != null)
            {
                mineralRigidbody.isKinematic = true;
            }

            SetInteractionEnabled(false);
            SetState(MineralExtractionState.Buried);
        }
    }

    private void SetState(MineralExtractionState newState)
    {
        // Once extracted, the mineral can never revert to previous states
        if (state == MineralExtractionState.Extracted && newState != MineralExtractionState.Extracted)
        {
            return;
        }

        state = newState;

        switch (state)
        {
            case MineralExtractionState.Buried:
                if (mineralRigidbody != null)
                {
                    mineralRigidbody.isKinematic = true;
                }
                SetInteractionEnabled(false);
                break;

            case MineralExtractionState.Extractable:
                // If still anchored in rock, remain kinematic; if completely dug out, drops to the ground
                mineralRigidbody.isKinematic = isAnchoredToTerrain;
                SetInteractionEnabled(true);
                break;

            case MineralExtractionState.Extracted:
                // Handled in Extract()
                break;
        }
    }

    // Extracts the mineral when grabbed by hand
    public void Extract()
    {
        if (state == MineralExtractionState.Extracted) return;

        state = MineralExtractionState.Extracted;
        isAnchoredToTerrain = false;

        transform.SetParent(null);

        grabbable.ForceKinematicDisabled = true;

        // Keep grab interactions enabled
        SetInteractionEnabled(true);
        PlayExtractionFeedback();
    }

    private void SetInteractionEnabled(bool isEnabled)
    {
        if (grabbable != null) grabbable.enabled = isEnabled;
        if (handGrabInteractable != null) handGrabInteractable.enabled = isEnabled;
    }

    private void PlayExtractionFeedback()
    {
        if (enableHaptics)
        {
            StartCoroutine(TriggerHapticsRoutine());
        }
    }

    private IEnumerator TriggerHapticsRoutine()
    {
        OVRInput.SetControllerVibration(hapticStrength, hapticStrength, OVRInput.Controller.LTouch);

        yield return new WaitForSeconds(hapticDuration);

        OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.LTouch);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;

        if (localRayDirections == null || localRayDirections.Length != raycastCount)
        {
            GenerateRaycastDirections();
        }

        float scale = Mathf.Max(transform.lossyScale.x, Mathf.Max(transform.lossyScale.y, transform.lossyScale.z));
        float effectiveRayDistance = raycastDistance * scale;

        if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null) return;

        Vector3 localCenter = meshFilter.sharedMesh.bounds.center;
        Vector3 worldCenter = transform.TransformPoint(localCenter);

        for (int i = 0; i < localRayDirections.Length; i++)
        {
            Vector3 rayDir = transform.TransformDirection(localRayDirections[i]);
            bool exposed = (raycastExposed != null && i < raycastExposed.Length) && raycastExposed[i];

            Gizmos.color = exposed ? Color.green : Color.red;
            Gizmos.DrawRay(worldCenter, rayDir * effectiveRayDistance);
        }
    }
}
