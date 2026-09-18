using System.Collections;
using UnityEngine;


/// <summary>
/// VR Splash Screen displayed at startup
/// </summary>
[DisallowMultipleComponent]
public class VRSplashScreen : MonoBehaviour
{
    [Header("Timing")]
    [Tooltip("Fade in duration in seconds.")]
    [SerializeField] private float fadeInDuration = 1.0f;

    [Tooltip("Duration in seconds to display title before fading out.")]
    [SerializeField, Min(0.5f)] private float holdDuration = 3.0f;

    [Tooltip("Fade out duration in seconds.")]
    [SerializeField, Min(0.1f)] private float fadeOutDuration = 1.0f;

    [Header("UI References")]
    [Tooltip("CanvasGroup controlling splash screen opacity.")]
    [SerializeField] private CanvasGroup splashCanvasGroup;

    private void Awake()
    {
        if (transform.parent != null)
        {
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        if (splashCanvasGroup != null)
        {
            splashCanvasGroup.alpha = 0f;
        }
    }

    private void Start()
    {
        StartCoroutine(SplashScreenSequence());
    }

    private IEnumerator SplashScreenSequence()
    {
        if (splashCanvasGroup == null) yield break;

        // Fade In
        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            splashCanvasGroup.alpha = Mathf.Clamp01(elapsed / fadeInDuration);
            yield return null;
        }
        splashCanvasGroup.alpha = 1f;

        yield return new WaitForSeconds(holdDuration);

        // Fade Out
        yield return StartCoroutine(QuickFadeOut());
    }

    private IEnumerator QuickFadeOut()
    {
        if (splashCanvasGroup != null)
        {
            float startAlpha = splashCanvasGroup.alpha;
            float elapsed = 0f;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                splashCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / (fadeOutDuration + 0.1f));
                yield return null;
            }
            splashCanvasGroup.alpha = 0f;
        }

        gameObject.SetActive(false);
    }
}
