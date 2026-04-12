using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TitanAscent.Systems;

namespace TitanAscent.UI
{
    public class ChallengeModeHUD : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────────

        [Header("Root Panel")]
        [SerializeField] private CanvasGroup rootGroup;

        [Header("Daily Badge")]
        [SerializeField] private GameObject dailyBadge;
        [SerializeField] private TextMeshProUGUI dailyDateText;

        [Header("Low Fuel Modifier")]
        [SerializeField] private GameObject lowFuelRow;
        [SerializeField] private Image       lowFuelIcon;
        [SerializeField] private TextMeshProUGUI lowFuelLabel;

        [Header("Extreme Wind Modifier")]
        [SerializeField] private GameObject extremeWindRow;
        [SerializeField] private Image       extremeWindIcon;
        [SerializeField] private TextMeshProUGUI extremeWindLabel;

        [Header("Ultra Slippery Modifier")]
        [SerializeField] private GameObject ultraSlipperyRow;
        [SerializeField] private Image       ultraSlipperyIcon;
        [SerializeField] private TextMeshProUGUI ultraSlipperyLabel;

        [Header("Pulse Settings")]
        [SerializeField] private float pulseDuration = 0.4f;
        [SerializeField] private Color pulseColor     = new Color(1f, 0.6f, 0.1f, 1f);
        [SerializeField] private float pulseScale     = 1.25f;

        [Header("Fade Duration")]
        [SerializeField] private float fadeInDuration = 0.5f;

        // ── Internal ─────────────────────────────────────────────────────────

        private ChallengeManager challengeManager;

        private ChallengeModifier lastModifiers = ChallengeModifier.None;
        private bool wasVisible;

        private Coroutine fadeinCoroutine;

        // ── Unity Lifecycle ──────────────────────────────────────────────────

        private void Awake()
        {
            if (rootGroup == null)
                rootGroup = GetComponent<CanvasGroup>();

            SetGroupAlpha(0f);
        }

        private void Start()
        {
            challengeManager = FindFirstObjectByType<ChallengeManager>();
            Refresh();
        }

        private void Update()
        {
            if (challengeManager == null) return;

            ChallengeModifier mods = challengeManager.ActiveModifiers;
            bool shouldBeVisible   = mods != ChallengeModifier.None;

            // Fade in when challenge activates
            if (shouldBeVisible && !wasVisible)
            {
                if (fadeinCoroutine != null) StopCoroutine(fadeinCoroutine);
                fadeinCoroutine = StartCoroutine(FadeIn());
            }
            else if (!shouldBeVisible && wasVisible)
            {
                SetGroupAlpha(0f);
            }

            wasVisible = shouldBeVisible;

            // Refresh rows if modifiers changed
            if (mods != lastModifiers)
            {
                lastModifiers = mods;
                Refresh();
            }

            // Pulse Low Fuel icon while active (example: energy depletes faster)
            if (mods.HasFlag(ChallengeModifier.LowFuel) && lowFuelIcon != null)
                PulseLowFuelIcon();
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>Trigger a pulse on the Low Fuel icon to indicate active depletion.</summary>
        public void PulseLowFuelModifier()
        {
            if (lowFuelRow != null && lowFuelRow.activeSelf)
                StartCoroutine(PulseElement(lowFuelIcon != null ? lowFuelIcon.transform : lowFuelRow.transform,
                                            lowFuelIcon));
        }

        // ── Private Helpers ──────────────────────────────────────────────────

        private void Refresh()
        {
            if (challengeManager == null) return;

            ChallengeModifier mods = challengeManager.ActiveModifiers;

            bool hasLowFuel      = mods.HasFlag(ChallengeModifier.LowFuel);
            bool hasExtremeWind  = mods.HasFlag(ChallengeModifier.ExtremeWind);
            bool hasUltraSlippery = mods.HasFlag(ChallengeModifier.UltraSlippery);

            SetRow(lowFuelRow,       hasLowFuel);
            SetRow(extremeWindRow,   hasExtremeWind);
            SetRow(ultraSlipperyRow, hasUltraSlippery);

            // Populate modifier labels with name and description
            if (hasLowFuel && lowFuelLabel != null)
                lowFuelLabel.text = "Low Fuel — Thruster energy reduced to 35%";

            if (hasExtremeWind && extremeWindLabel != null)
                extremeWindLabel.text = "Extreme Wind — Wind force at 2.5×";

            if (hasUltraSlippery && ultraSlipperyLabel != null)
                ultraSlipperyLabel.text = "Ultra Slippery — Surface friction at 20%";

            // Daily badge
            bool isDaily = challengeManager.IsDaily;
            if (dailyBadge != null) dailyBadge.SetActive(isDaily);
            if (isDaily && dailyDateText != null)
                dailyDateText.text = DateTime.UtcNow.ToString("MMM dd");
        }

        private void SetRow(GameObject row, bool active)
        {
            if (row != null) row.SetActive(active);
        }

        private void SetGroupAlpha(float alpha)
        {
            if (rootGroup != null) rootGroup.alpha = alpha;
        }

        private IEnumerator FadeIn()
        {
            float elapsed = 0f;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.deltaTime;
                SetGroupAlpha(Mathf.Clamp01(elapsed / fadeInDuration));
                yield return null;
            }
            SetGroupAlpha(1f);
            fadeinCoroutine = null;
        }

        // Simple per-frame pulse — runs continuously while LowFuel is active
        private float lowFuelPulseTimer;
        private void PulseLowFuelIcon()
        {
            lowFuelPulseTimer += Time.deltaTime;
            float t = Mathf.Abs(Mathf.Sin(lowFuelPulseTimer * 2.5f));
            if (lowFuelIcon != null)
                lowFuelIcon.color = Color.Lerp(Color.white, new Color(1f, 0.5f, 0f), t);
        }

        private IEnumerator PulseElement(Transform target, Image colorTarget)
        {
            if (target == null) yield break;

            Vector3 originalScale   = target.localScale;
            Color   originalColor   = colorTarget != null ? colorTarget.color : Color.white;

            float elapsed = 0f;
            while (elapsed < pulseDuration)
            {
                elapsed += Time.deltaTime;
                float t     = elapsed / pulseDuration;
                float pulse = Mathf.Sin(t * Mathf.PI);

                target.localScale = Vector3.Lerp(originalScale, originalScale * pulseScale, pulse);
                if (colorTarget != null)
                    colorTarget.color = Color.Lerp(originalColor, pulseColor, pulse);

                yield return null;
            }

            target.localScale = originalScale;
            if (colorTarget != null) colorTarget.color = originalColor;
        }
    }
}
