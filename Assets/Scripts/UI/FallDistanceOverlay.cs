using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TitanAscent.UI
{
    public class FallDistanceOverlay : MonoBehaviour
    {
        [Header("UI Components")]
        [SerializeField] private TextMeshProUGUI fallDistanceText;
        [SerializeField] private TextMeshProUGUI landingFlashText;
        [SerializeField] private CanvasGroup overlayGroup;

        [Header("Thresholds")]
        [SerializeField] private float showThreshold = 20f;
        [SerializeField] private float catastrophicThreshold = 500f;

        [Header("Colors")]
        [SerializeField] private Color normalFallColor = new Color(1f, 0.3f, 0.2f);
        [SerializeField] private Color catastrophicColor = new Color(0.8f, 0f, 0f);

        [Header("References")]
        [SerializeField] private Systems.FallTracker fallTracker;

        private bool isShowing = false;
        private Coroutine fadeRoutine;
        private Coroutine landingFlashRoutine;
        private float baseTextSize = 48f;

        private void Awake()
        {
            if (overlayGroup != null) overlayGroup.alpha = 0f;
            if (landingFlashText != null) landingFlashText.alpha = 0f;
        }

        private void Start()
        {
            if (fallTracker != null)
                fallTracker.OnFallCompleted.AddListener(OnFallLanded);
        }

        private void OnDisable()
        {
            if (fadeRoutine        != null) { StopCoroutine(fadeRoutine);        fadeRoutine        = null; }
            if (landingFlashRoutine != null) { StopCoroutine(landingFlashRoutine); landingFlashRoutine = null; }
        }

        private void OnDestroy()
        {
            if (fallTracker != null)
                fallTracker.OnFallCompleted.RemoveListener(OnFallLanded);
        }

        private void Update()
        {
            if (fallTracker == null) return;

            bool shouldShow = fallTracker.IsFalling && fallTracker.CurrentFallDistance >= showThreshold;

            if (shouldShow)
            {
                if (!isShowing) ShowOverlay();
                UpdateCounter(fallTracker.CurrentFallDistance);
            }
            else if (isShowing && !fallTracker.IsFalling)
            {
                HideOverlay();
            }
        }

        private void ShowOverlay()
        {
            isShowing = true;
            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            fadeRoutine = StartCoroutine(FadeOverlay(0f, 1f, 0.3f));
        }

        private void HideOverlay()
        {
            isShowing = false;
            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            fadeRoutine = StartCoroutine(FadeOverlay(1f, 0f, 0.5f));
        }

        private void UpdateCounter(float distance)
        {
            if (fallDistanceText == null) return;

            bool isCatastrophic = distance >= catastrophicThreshold;
            fallDistanceText.color = isCatastrophic ? catastrophicColor : normalFallColor;

            float sizeBoost = Mathf.Clamp(distance * 0.02f, 0f, 24f);
            fallDistanceText.fontSize = baseTextSize + sizeBoost;

            fallDistanceText.text = $"▼ {Mathf.RoundToInt(distance)}m";

            if (isCatastrophic)
            {
                float pulse = 1f + Mathf.Sin(Time.time * 6f) * 0.08f;
                fallDistanceText.transform.localScale = Vector3.one * pulse;
            }
            else
            {
                fallDistanceText.transform.localScale = Vector3.one;
            }
        }

        private void OnFallLanded(Systems.FallData data)
        {
            if (data.distance < showThreshold) return;
            if (landingFlashRoutine != null) StopCoroutine(landingFlashRoutine);
            landingFlashRoutine = StartCoroutine(LandingFlash(data.distance));
        }

        private IEnumerator LandingFlash(float distance)
        {
            yield return new WaitForSeconds(0.2f);

            if (fallDistanceText != null) fallDistanceText.text = "";
            if (overlayGroup != null) overlayGroup.alpha = 0f;
            isShowing = false;

            if (landingFlashText == null) yield break;

            bool isCatastrophic = distance >= catastrophicThreshold;
            landingFlashText.text = $"FELL  {Mathf.RoundToInt(distance)}m";
            landingFlashText.color = isCatastrophic ? catastrophicColor : normalFallColor;
            landingFlashText.fontSize = isCatastrophic ? 64f : 52f;
            landingFlashText.transform.localScale = Vector3.one * 0.7f;

            // Scale up
            float t = 0f;
            while (t < 0.25f)
            {
                t += Time.deltaTime;
                float scale = Mathf.Lerp(0.7f, 1.2f, t / 0.25f);
                landingFlashText.transform.localScale = Vector3.one * scale;
                landingFlashText.alpha = Mathf.Lerp(0f, 1f, t / 0.25f);
                yield return null;
            }

            yield return new WaitForSeconds(0.5f);

            // Fade out
            t = 0f;
            while (t < 0.8f)
            {
                t += Time.deltaTime;
                landingFlashText.alpha = Mathf.Lerp(1f, 0f, t / 0.8f);
                yield return null;
            }
            landingFlashText.alpha = 0f;
        }

        private IEnumerator FadeOverlay(float from, float to, float duration)
        {
            if (overlayGroup == null) yield break;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                overlayGroup.alpha = Mathf.Lerp(from, to, t / duration);
                yield return null;
            }
            overlayGroup.alpha = to;
        }
    }
}
