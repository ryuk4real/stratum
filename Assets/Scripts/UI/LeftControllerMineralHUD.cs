using TMPro;
using UnityEngine;

/// <summary>
/// Displays a World Space HUD on top of the player's left VR controller.
/// Automatically hides when a mineral is grabbed, and reactivates upon release.
/// </summary>
[DisallowMultipleComponent]
public class LeftControllerMineralHUD : MonoBehaviour
{
    [Header("Detection Settings")]
    [Tooltip("Max distance from controller to detect an exposed mineral when not currently held.")]
    [SerializeField] private float proximityDetectionRadius = 0.4f;
    [Tooltip("Layer mask for detecting minerals.")]
    [SerializeField] private LayerMask mineralLayerMask;

    [Header("Display Text")]
    [SerializeField] private string unknownText = "Unknown";
    [SerializeField] private string idleText = "...";
    [SerializeField] private string sublabelIdle = "SCANNER READY";
    [SerializeField] private string sublabelDiscovered = "IDENTIFIED";
    [SerializeField] private string sublabelUndiscovered = "UNANALYZED SAMPLE";
    [SerializeField] private string sublabelCollected = "COLLECTED";
    [SerializeField] private float collectedDisplayDuration = 2.0f;

    [Header("Colors")]
    [SerializeField] private Color discoveredColor = new Color(0.2f, 0.95f, 0.85f, 1f);
    [SerializeField] private Color unknownColor = new Color(1f, 0.75f, 0.25f, 1f);
    [SerializeField] private Color idleColor = new Color(0.6f, 0.7f, 0.8f, 0.5f);
    [SerializeField] private Color collectedColor = new Color(0.2f, 0.95f, 0.85f, 1f);

    [Header("UI References")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text mineralNameText;
    [SerializeField] private TMP_Text statusSubText;

    private Canvas hudCanvas;
    private OVROverlayCanvas ovrOverlayCanvas;
    private bool isCanvasVisible = true;

    // Runtime state
    private readonly Collider[] overlapHits = new Collider[16];
    private MineralBehaviour currentTargetMineral;
    private string collectedMineralName = "";
    private float collectedUntilTime = 0f;

    private void Awake()
    {
        if (hudCanvas == null)
        {
            hudCanvas = GetComponent<Canvas>();
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        ovrOverlayCanvas = GetComponent<OVROverlayCanvas>();
    }

    private void OnEnable()
    {
        CollectionManager.OnAnyMineralCollected += HandleMineralCollected;
    }

    private void OnDisable()
    {
        CollectionManager.OnAnyMineralCollected -= HandleMineralCollected;
    }

    private void HandleMineralCollected(Mineral mineral)
    {
        if (mineral == null) return;
        collectedMineralName = mineral.MineralName;
        collectedUntilTime = Time.time + collectedDisplayDuration;
    }

    private void LateUpdate()
    {
        // Hide scanner canvas while grabbing a mineral and reactivate when released
        if (MineralBehaviour.IsAnyMineralGrabbed)
        {
            SetCanvasVisible(false);
            return;
        }

        SetCanvasVisible(true);

        if (Time.time < collectedUntilTime)
        {
            DisplayCollected();
        }
        else
        {
            UpdateMineralTarget();
            UpdateDisplay();
        }
    }

    private void SetCanvasVisible(bool visible)
    {
        if (isCanvasVisible == visible) return;
        isCanvasVisible = visible;

        if (hudCanvas != null)
        {
            hudCanvas.enabled = visible;
        }

        if (ovrOverlayCanvas != null)
        {
            ovrOverlayCanvas.enabled = visible;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 0.45f : 0f;
        }
    }

    private void UpdateMineralTarget()
    {
        currentTargetMineral = null;

        // Proximity scan for exposed or extracted mineral near left hand
        Vector3 scanOrigin = transform.position;
        int count = Physics.OverlapSphereNonAlloc(scanOrigin, proximityDetectionRadius, overlapHits, mineralLayerMask);

        float closestDist = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            Collider col = overlapHits[i];
            if (col == null) continue;

            MineralBehaviour mb = col.GetComponentInParent<MineralBehaviour>();
            if (mb == null || mb.IsCollected) continue;

            // Don't identify buried minerals
            if (mb.State == MineralExtractionState.Buried) continue;

            float d = Vector3.Distance(scanOrigin, mb.transform.position);
            if (d < closestDist)
            {
                closestDist = d;
                currentTargetMineral = mb;
            }
        }
    }

    private void UpdateDisplay()
    {
        if (mineralNameText == null) return;

        if (currentTargetMineral != null && currentTargetMineral.MineralData != null)
        {
            Mineral data = currentTargetMineral.MineralData;
            bool isDiscovered = CollectionManager.Instance != null && CollectionManager.Instance.IsDiscovered(data.Id);

            if (isDiscovered)
            {
                mineralNameText.text = data.MineralName;
                mineralNameText.color = discoveredColor;
                if (statusSubText != null)
                {
                    statusSubText.text = sublabelDiscovered;
                    statusSubText.color = discoveredColor;
                }
            }
            else
            {
                mineralNameText.text = unknownText;
                mineralNameText.color = unknownColor;
                if (statusSubText != null)
                {
                    statusSubText.text = sublabelUndiscovered;
                    statusSubText.color = unknownColor;
                }
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, 1f, Time.deltaTime * 6f);
            }
        }
        else
        {
            // Idle state
            mineralNameText.text = idleText;
            mineralNameText.color = idleColor;
            if (statusSubText != null)
            {
                statusSubText.text = sublabelIdle;
                statusSubText.color = idleColor;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, 0.45f, Time.deltaTime * 3f);
            }
        }
    }

    private void DisplayCollected()
    {
        if (mineralNameText == null) return;

        mineralNameText.text = string.IsNullOrEmpty(collectedMineralName) ? idleText : collectedMineralName;
        mineralNameText.color = collectedColor;

        if (statusSubText != null)
        {
            statusSubText.text = sublabelCollected;
            statusSubText.color = collectedColor;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, 1f, Time.deltaTime * 6f);
        }
    }
}
