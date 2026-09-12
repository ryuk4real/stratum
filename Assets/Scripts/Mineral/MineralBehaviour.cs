using System;
using System.Collections;
using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;

/// <summary>
/// Controls the mineral's physical behavior and hand extraction
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class MineralBehaviour : MonoBehaviour
{
    [Header("Mineral Data")]
    [SerializeField] private Mineral mineralData;

    [Header("Extraction Settings")]
    [Tooltip("Percentage of raycasts exposed to air required before the mineral can be grabbed by hand or controller.")]
    [SerializeField, Range(0.3f, 0.95f)] private float exposureThreshold = 0.75f;

    [Tooltip("Percentage of raycasts exposed to air at which the mineral is considered completely free from rock and falls due to gravity.")]
    [SerializeField, Range(0.85f, 1.0f)] private float fullExcavationThreshold = 1.0f;

    [Tooltip("Number of raycasts distributed around the mineral's volume.")]
    [SerializeField, Range(8, 64)] private int raycastCount = 20;

    [Tooltip("Raycast distance from the center.")]
    [SerializeField, Range(0.02f, 0.5f)] private float raycastDistance = 0.08f;

    [Header("VR Haptics")]
    [Tooltip("Trigger controller vibration when successfully extracting or collecting the mineral.")]
    [SerializeField] private bool enableHaptics = true;
    [SerializeField, Range(0.1f, 1f)] private float hapticStrength = 0.7f;
    [SerializeField, Range(0.05f, 0.5f)] private float hapticDuration = 0.2f;

    [Header("Debug & State")]
    [SerializeField] private MineralExtractionState state = MineralExtractionState.Buried;
    [Tooltip("Ratio of exposed raycasts.")]
    [SerializeField, Range(0f, 1f)] private float exposureRatio = 0f;
    [SerializeField] private bool isAnchoredToTerrain = true;
    [SerializeField] private bool isGrabbed = false;
    [SerializeField] private bool drawGizmos = true;

    // Components
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Collider mineralCollider;
    private Rigidbody mineralRigidbody;
    private Grabbable grabbable;
    private GrabInteractable grabInteractable;
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
    public bool IsGrabbed => isGrabbed || (grabbable != null && grabbable.SelectingPointsCount > 0);
    public bool IsHeldByController => IsGrabbed && OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.LTouch);
    public bool IsHeldByHand => IsGrabbed && !IsHeldByController;
    public bool IsCollected { get; private set; } = false;

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        mineralCollider = GetComponent<Collider>();
        mineralRigidbody = GetComponent<Rigidbody>();
        grabbable = GetComponent<Grabbable>();
        grabInteractable = GetComponent<GrabInteractable>();
        handGrabInteractable = GetComponent<HandGrabInteractable>();

        // Enforce initial buried state inside solid rock
        state = MineralExtractionState.Buried;
        isAnchoredToTerrain = true;
        mineralRigidbody.isKinematic = true;
        mineralRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        mineralRigidbody.interpolation = RigidbodyInterpolation.Interpolate;

        // Ensure interactions are disabled while buried so it cannot be grabbed through the rock
        SetInteractionsEnabled(false);

        ApplyMineralData();
        GenerateRaycastDirections();
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

    private void Update()
    {
        if (IsCollected || !IsGrabbed) return;

        // Left Controller Index Trigger to collect into inventory
        if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.LTouch))
        {
            CollectMineralInstance(OVRInput.Controller.LTouch);
        }
    }

    private void SetInteractionsEnabled(bool isEnabled)
    {
        if (grabbable != null) grabbable.enabled = isEnabled;
        if (grabInteractable != null) grabInteractable.enabled = isEnabled;
        if (handGrabInteractable != null) handGrabInteractable.enabled = isEnabled;
    }

    private void HandlePointerEvent(PointerEvent evt)
    {
        if (evt.Type == PointerEventType.Select)
        {
            // Disallow grab if still buried in rock
            if (state == MineralExtractionState.Buried)
            {
                return;
            }

            isGrabbed = true;

            // If extractable, extract it upon first grab
            if (state == MineralExtractionState.Extractable)
            {
                Extract();
            }
        }
        else if (evt.Type == PointerEventType.Unselect || evt.Type == PointerEventType.Cancel)
        {
            isGrabbed = (grabbable != null && grabbable.SelectingPointsCount > 0);

            if (!isGrabbed && state == MineralExtractionState.Extracted && mineralRigidbody != null && !IsCollected)
            {
                // Ensure physics takes over when dropped
                mineralRigidbody.isKinematic = false;
            }
        }
    }

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

            bool isSolidTerrain = hitTerrain;
            if (!isSolidTerrain)
            {
                isSolidTerrain = TerrainManager.Instance.IsPointInSolidTerrain(samplePoint);
            }

            raycastExposed[i] = !isSolidTerrain;
            if (raycastExposed[i])
            {
                exposedCount++;
            }
        }

        Physics.queriesHitBackfaces = prevBackfaces;
        exposureRatio = (float)exposedCount / raycastCount;

        // Mineral completely freed from rock -> fall free
        if (exposedCount == raycastCount || exposureRatio >= fullExcavationThreshold)
        {
            Extract();

            if (mineralRigidbody != null && !IsGrabbed)
            {
                mineralRigidbody.isKinematic = false;
            }
        }
        // Sufficiently exposed to be grabbed and extracted
        else if (exposureRatio >= exposureThreshold)
        {
            isAnchoredToTerrain = true;

            if (mineralRigidbody != null && !IsGrabbed)
            {
                mineralRigidbody.isKinematic = true;
            }

            SetState(MineralExtractionState.Extractable);
        }
        else // Still buried inside rock
        {
            isAnchoredToTerrain = true;

            if (mineralRigidbody != null && !IsGrabbed)
            {
                mineralRigidbody.isKinematic = true;
            }

            SetState(MineralExtractionState.Buried);
        }
    }

    private void SetState(MineralExtractionState newState)
    {
        if (state == MineralExtractionState.Extracted && newState != MineralExtractionState.Extracted)
        {
            return;
        }

        state = newState;

        switch (state)
        {
            case MineralExtractionState.Buried:
                if (mineralRigidbody != null) mineralRigidbody.isKinematic = true;
                // Disallow hand and controller grabs while buried
                SetInteractionsEnabled(false);
                break;

            case MineralExtractionState.Extractable:
                if (!IsGrabbed && mineralRigidbody != null) mineralRigidbody.isKinematic = isAnchoredToTerrain;
                // Allow hands and controllers to grab and extract
                SetInteractionsEnabled(true);
                break;

            case MineralExtractionState.Extracted:
                // Can be freely picked up from the ground by hand or controller
                SetInteractionsEnabled(true);
                break;
        }
    }

    public void Extract()
    {
        if (state == MineralExtractionState.Extracted) return;

        state = MineralExtractionState.Extracted;
        isAnchoredToTerrain = false;

        transform.SetParent(null);

        SetInteractionsEnabled(true);
        PlayExtractionFeedback();
    }


    // Collects the mineral: registers it in CollectionManager, triggers haptics, plays shrink animation and destroys it
    public void CollectMineralInstance(OVRInput.Controller hapticController = OVRInput.Controller.LTouch)
    {
        if (IsCollected) return;
        IsCollected = true;

        StartCoroutine(CollectAnimationRoutine(hapticController));
    }

    private IEnumerator CollectAnimationRoutine(OVRInput.Controller hapticController)
    {
        isGrabbed = false;
        PrepareForCollection();

        // Register in CollectionManager
        if (CollectionManager.Instance != null && mineralData != null)
        {
            CollectionManager.Instance.CollectMineral(mineralData);
        }

        if (enableHaptics)
        {
            StartCoroutine(TriggerHapticsRoutine(hapticController));
        }

        // Shrink animation
        Vector3 initialScale = transform.localScale;
        float duration = 1.0f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            transform.localScale = Vector3.Lerp(initialScale, Vector3.zero, t);
            yield return null;
        }

        transform.localScale = Vector3.zero;
        Destroy(gameObject);
    }

    public void PrepareForCollection()
    {
        IsCollected = true;
        SetInteractionsEnabled(false);

        if (mineralCollider != null) mineralCollider.enabled = false;
        if (mineralRigidbody != null)
        {
            mineralRigidbody.isKinematic = true;
            mineralRigidbody.detectCollisions = false;
        }
    }


    private void PlayExtractionFeedback()
    {
        if (enableHaptics)
        {
            StartCoroutine(TriggerHapticsRoutine(OVRInput.Controller.LTouch));
        }
    }

    private IEnumerator TriggerHapticsRoutine(OVRInput.Controller controller)
    {
        OVRInput.SetControllerVibration(hapticStrength, hapticStrength, controller);
        yield return new WaitForSeconds(hapticDuration);
        OVRInput.SetControllerVibration(0f, 0f, controller);
    }

    private void OnValidate()
    {
        if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
        if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
        if (mineralCollider == null) mineralCollider = GetComponent<Collider>();
        if (mineralRigidbody == null) mineralRigidbody = GetComponent<Rigidbody>();
        if (grabbable == null) grabbable = GetComponent<Grabbable>();
        if (grabInteractable == null) grabInteractable = GetComponent<GrabInteractable>();
        if (handGrabInteractable == null) handGrabInteractable = GetComponent<HandGrabInteractable>();

        ApplyMineralData();
        GenerateRaycastDirections();
    }

    public void SetMineralData(Mineral data)
    {
        mineralData = data;
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

    private void GenerateRaycastDirections()
    {
        localRayDirections = new Vector3[raycastCount];
        raycastExposed = new bool[raycastCount];

        float goldenRatioAngle = Mathf.PI * (3f - Mathf.Sqrt(5f));

        for (int i = 0; i < raycastCount; i++)
        {
            float y = 1f - (i / (float)(raycastCount - 1)) * 2f;
            float radiusAtY = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            float theta = goldenRatioAngle * i;

            float x = Mathf.Cos(theta) * radiusAtY;
            float z = Mathf.Sin(theta) * radiusAtY;

            localRayDirections[i] = new Vector3(x, y, z);
            raycastExposed[i] = false;
        }
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

