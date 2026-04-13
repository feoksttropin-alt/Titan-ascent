using System.Collections;
using UnityEngine;
using TMPro;
using TitanAscent.Environment;

namespace TitanAscent.UI
{
    /// <summary>
    /// Manages the zone name popup that appears when entering a new zone.
    ///
    /// Layout:
    ///   - Zone number text (small, e.g. "ZONE 4") above
    ///   - Zone name text (large, e.g. "WING ROOT") below
    ///   - Description text (small, 1 s delay after name appears)
    ///
    /// Animation: slides in from the left over 0.4 s, holds 2.5 s,
    ///            slides out to the right over 0.5 s.
    ///
    /// Queue-safe: a new zone transition cancels and replaces any in-progress one.
    ///
    /// Tint colour matches zone ambient colour.
    /// </summary>
    public class ZoneNameDisplay : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        // Inspector
        // -----------------------------------------------------------------------

        [Header("Text Elements")]
        [SerializeField] private TextMeshProUGUI zoneNumberText;
        [SerializeField] private TextMeshProUGUI zoneNameText;
        [SerializeField] private TextMeshProUGUI descriptionText;

        [Header("Layout")]
        [SerializeField] private RectTransform containerRect;

        [Header("Animation Timings")]
        [SerializeField] private float slideInDuration  = 0.4f;
        [SerializeField] private float holdDuration     = 2.5f;
        [SerializeField] private float slideOutDuration = 0.5f;
        [SerializeField] private float descriptionDelay = 1f;

        [Header("Slide Offsets")]
        [Tooltip("How far off-screen to the left the panel starts (pixels, negative).")]
        [SerializeField] private float slideInStartX  = -800f;
        [Tooltip("How far off-screen to the right the panel ends (pixels, positive).")]
        [SerializeField] private float slideOutEndX   =  800f;

        // -----------------------------------------------------------------------
        // Private State
        // -----------------------------------------------------------------------

        private Coroutine _displayCoroutine;
        private Vector2   _anchoredOrigin;  // resting position of the container

        private ZoneManager _zoneManager;

        // -----------------------------------------------------------------------
        // Lifecycle
        // -----------------------------------------------------------------------

        private void Awake()
        {
            if (containerRect == null && transform is RectTransform rt)
                containerRect = rt;

            if (containerRect != null)
                _anchoredOrigin = containerRect.anchoredPosition;

            // Start hidden
            SetAlpha(0f);
            if (containerRect != null)
                containerRect.anchoredPosition = new Vector2(slideInStartX, _anchoredOrigin.y);
        }

        private void Start()
        {
            _zoneManager = FindFirstObjectByType<ZoneManager>();
            if (_zoneManager != null)
                _zoneManager.OnZoneChanged.AddListener(OnZoneChanged);
        }

        private void OnDestroy()
        {
            if (_zoneManager != null)
                _zoneManager.OnZoneChanged.RemoveListener(OnZoneChanged);
        }

        // -----------------------------------------------------------------------
        // Zone Change Handler
        // -----------------------------------------------------------------------

        private void OnZoneChanged(TitanZone previous, TitanZone newZone)
        {
            if (newZone == null) return;

            int zoneIndex = _zoneManager != null ? _zoneManager.CurrentZoneIndex + 1 : 0;
            ShowZone(zoneIndex, newZone.name, newZone.description, newZone.ambientColor);
        }

        // -----------------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------------

        /// <summary>
        /// Displays zone entry UI. Cancels any in-progress display immediately.
        /// </summary>
        public void ShowZone(int zoneNumber, string zoneName, string description, Color tintColor)
        {
            // Cancel previous display instantly
            if (_displayCoroutine != null)
            {
                StopCoroutine(_displayCoroutine);
                _displayCoroutine = null;
            }

            // Apply content
            if (zoneNumberText  != null) zoneNumberText.text  = $"ZONE {zoneNumber}";
            if (zoneNameText    != null) zoneNameText.text    = zoneName.ToUpper();
            if (descriptionText != null)
            {
                descriptionText.text  = description;
                descriptionText.alpha = 0f;
            }

            // Apply tint colour to all text elements
            ApplyTint(tintColor);

            _displayCoroutine = StartCoroutine(AnimateDisplay());
        }

        // -----------------------------------------------------------------------
        // Animation Coroutine
        // -----------------------------------------------------------------------

        private IEnumerator AnimateDisplay()
        {
            if (containerRect == null) yield break;

            // --- Slide In ---
            Vector2 startPos = new Vector2(slideInStartX, _anchoredOrigin.y);
            Vector2 endPos   = _anchoredOrigin;

            float elapsed = 0f;
            while (elapsed < slideInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t  = Mathf.SmoothStep(0f, 1f, elapsed / slideInDuration);

                containerRect.anchoredPosition = Vector2.LerpUnclamped(startPos, endPos, t);
                SetAlpha(t);
                yield return null;
            }

            containerRect.anchoredPosition = endPos;
            SetAlpha(1f);

            // --- Description fade in after delay ---
            yield return new WaitForSecondsRealtime(descriptionDelay);

            if (descriptionText != null)
            {
                float fadeDur = 0.4f;
                elapsed = 0f;
                while (elapsed < fadeDur)
                {
                    elapsed           += Time.unscaledDeltaTime;
                    descriptionText.alpha = Mathf.Clamp01(elapsed / fadeDur);
                    yield return null;
                }
                descriptionText.alpha = 1f;
            }

            // --- Hold ---
            float holdRemaining = holdDuration - descriptionDelay;
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, holdRemaining));

            // --- Slide Out ---
            Vector2 slideOutEnd = new Vector2(slideOutEndX, _anchoredOrigin.y);
            Vector2 slideOutStart = containerRect.anchoredPosition;

            elapsed = 0f;
            while (elapsed < slideOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t  = Mathf.SmoothStep(0f, 1f, elapsed / slideOutDuration);

                containerRect.anchoredPosition = Vector2.LerpUnclamped(slideOutStart, slideOutEnd, t);
                SetAlpha(1f - t);
                yield return null;
            }

            SetAlpha(0f);
            containerRect.anchoredPosition = new Vector2(slideInStartX, _anchoredOrigin.y);

            if (descriptionText != null) descriptionText.alpha = 0f;

            _displayCoroutine = null;
        }

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        private void SetAlpha(float a)
        {
            if (zoneNumberText  != null) zoneNumberText.alpha  = a;
            if (zoneNameText    != null) zoneNameText.alpha    = a;
            // Description alpha is managed separately
        }

        private void ApplyTint(Color color)
        {
            // Use a brightened version so text stays legible against dark backgrounds
            Color brightened = Color.Lerp(color, Color.white, 0.6f);

            if (zoneNumberText  != null) zoneNumberText.color  = brightened;
            if (zoneNameText    != null) zoneNameText.color    = Color.white;
            if (descriptionText != null) descriptionText.color = brightened;
        }
    }
}
