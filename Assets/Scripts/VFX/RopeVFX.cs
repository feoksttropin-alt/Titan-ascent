using System.Collections;
using UnityEngine;
using TitanAscent.Grapple;

namespace TitanAscent.VFX
{
    /// <summary>
    /// Visual polish for the rope rendered by RopeSimulator. Handles sway on slack,
    /// tension-based color shifts, max-tension pulse, release scatter, and a coil
    /// animation when the grapple is not attached. Updates the RopeSimulator's
    /// LineRenderer per-frame.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class RopeVFX : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("References")]
        [SerializeField] private RopeSimulator ropeSimulator;

        [Header("Sway")]
        [SerializeField] private float swayTensionThreshold  = 0.2f;   // below this tension: apply sway
        [SerializeField] private float swayAmplitudeMin      = 0.05f;
        [SerializeField] private float swayAmplitudeMax      = 0.15f;
        [SerializeField] private float swayBaseFrequency     = 1.4f;   // per-segment freq multiplied by segment index variation

        [Header("Tension Color")]
        [SerializeField] private Color ropeColorNeutral      = Color.white;
        [SerializeField] private Color ropeColorTense        = new Color(1f, 0.45f, 0.1f, 1f); // orange-red
        [SerializeField] private float tensionColorLowBound  = 0.7f;
        [SerializeField] private float tensionColorHighBound = 1.0f;

        [Header("Max Tension Pulse")]
        [SerializeField] private float maxTensionThreshold   = 0.95f;
        [SerializeField] private float pulsePeakBrightness   = 2.5f;   // HDR multiplier
        [SerializeField] private float pulseDuration         = 0.12f;

        [Header("Release Scatter")]
        [SerializeField] private float scatterOutwardSpeed   = 0.6f;   // units per second outward
        [SerializeField] private float scatterDuration       = 0.15f;

        [Header("Coil (when detached)")]
        [SerializeField] private float coilRadius            = 0.18f;
        [SerializeField] private float coilTurns             = 2.5f;
        [SerializeField] private float coilHeight            = 0.3f;
        [SerializeField] private float coilSpinSpeed         = 120f;   // degrees per second

        // ── Private state ─────────────────────────────────────────────────────

        private LineRenderer lineRenderer;

        private bool    wasTenseLastFrame;
        private float   pulseTimer;
        private bool    pulsing;
        private Coroutine pulseCoroutine;

        private bool    wasAttachedLastFrame;
        private bool    scatterActive;
        private Coroutine scatterCoroutine;

        // Coil
        private float   coilAngle;

        // Per-segment sway phase offsets (randomised at start so segments sway independently)
        private float[] swayPhaseOffsets;
        private float[] swayFrequencyMultipliers;

        // Cached segment positions used for scatter starting points and sway reuse
        private Vector3[] cachedPositions;
        private Vector3[] _swayPositionsBuffer;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            lineRenderer = GetComponent<LineRenderer>();

            if (ropeSimulator == null)
                ropeSimulator = GetComponentInParent<RopeSimulator>();
            if (ropeSimulator == null)
                ropeSimulator = FindFirstObjectByType<RopeSimulator>();

            InitSwayData();
        }

        private void InitSwayData()
        {
            int count = lineRenderer.positionCount > 0 ? lineRenderer.positionCount : 20;
            swayPhaseOffsets          = new float[count];
            swayFrequencyMultipliers  = new float[count];
            cachedPositions           = new Vector3[count];

            for (int i = 0; i < count; i++)
            {
                swayPhaseOffsets[i]         = Random.Range(0f, Mathf.PI * 2f);
                swayFrequencyMultipliers[i] = Random.Range(0.8f, 1.6f);
            }
        }

        private void LateUpdate()
        {
            if (ropeSimulator == null) return;

            bool isAttached = ropeSimulator.IsAttached;

            if (isAttached)
            {
                // Rope is live — apply visual polish
                float tension = ropeSimulator.GetTension();

                UpdateColors(tension);
                ApplySway(tension);
                HandleMaxTensionPulse(tension);

                wasAttachedLastFrame = true;
                scatterActive        = false;
            }
            else
            {
                if (wasAttachedLastFrame && !scatterActive)
                {
                    // Just released — kick scatter
                    CacheCurrentPositions();
                    if (scatterCoroutine != null) StopCoroutine(scatterCoroutine);
                    scatterCoroutine = StartCoroutine(ScatterRelease());
                }

                if (!scatterActive)
                    DrawCoil();

                wasAttachedLastFrame = false;
            }
        }

        // ── Color / Tension ───────────────────────────────────────────────────

        private void UpdateColors(float tension)
        {
            float t = Mathf.InverseLerp(tensionColorLowBound, tensionColorHighBound, tension);
            Color ropeColor = Color.Lerp(ropeColorNeutral, ropeColorTense, t);

            lineRenderer.startColor = ropeColor;
            lineRenderer.endColor   = ropeColor;

            // Per-segment color via gradient (approximate via start/end since Unity LR
            // uses gradient; for richer per-vertex color we would use SetColors with a
            // Gradient, but direct segment APIs aren't exposed — use start/end as fallback)
        }

        // ── Sway ──────────────────────────────────────────────────────────────

        private void ApplySway(float tension)
        {
            if (tension >= swayTensionThreshold) return;
            if (lineRenderer.positionCount < 3) return;

            // Slack factor: closer to 0 tension = more sway
            float slackFactor = 1f - Mathf.InverseLerp(0f, swayTensionThreshold, tension);
            float amplitude   = Mathf.Lerp(swayAmplitudeMin, swayAmplitudeMax, slackFactor);

            int count = lineRenderer.positionCount;
            EnsureSwayArrays(count);

            // Reuse cached buffer to avoid per-frame heap allocation
            if (_swayPositionsBuffer == null || _swayPositionsBuffer.Length != count)
                _swayPositionsBuffer = new Vector3[count];
            lineRenderer.GetPositions(_swayPositionsBuffer);
            Vector3[] positions = _swayPositionsBuffer;

            for (int i = 1; i < count - 1; i++) // don't touch anchor or player end
            {
                float freq  = swayBaseFrequency * swayFrequencyMultipliers[i];
                float sway  = Mathf.Sin(Time.time * freq + swayPhaseOffsets[i]) * amplitude;

                // Displace perpendicular to the rope axis (approximate with world right)
                Vector3 segDir = Vector3.zero;
                if (i + 1 < count)
                    segDir = (positions[i + 1] - positions[i - 1]).normalized;
                Vector3 perpendicular = segDir == Vector3.zero
                    ? Vector3.right
                    : Vector3.Cross(segDir, Vector3.up).normalized;

                positions[i] += perpendicular * sway;
            }

            lineRenderer.SetPositions(positions);
        }

        private void EnsureSwayArrays(int count)
        {
            if (swayPhaseOffsets == null || swayPhaseOffsets.Length != count)
            {
                swayPhaseOffsets         = new float[count];
                swayFrequencyMultipliers = new float[count];
                for (int i = 0; i < count; i++)
                {
                    swayPhaseOffsets[i]        = Random.Range(0f, Mathf.PI * 2f);
                    swayFrequencyMultipliers[i] = Random.Range(0.8f, 1.6f);
                }
            }
        }

        // ── Max Tension Pulse ─────────────────────────────────────────────────

        private void HandleMaxTensionPulse(float tension)
        {
            bool tenseNow = tension >= maxTensionThreshold;

            if (tenseNow && !wasTenseLastFrame)
            {
                // Rising edge: fire pulse
                if (pulseCoroutine != null) StopCoroutine(pulseCoroutine);
                pulseCoroutine = StartCoroutine(TensionPulseCoroutine());
            }

            wasTenseLastFrame = tenseNow;
        }

        private IEnumerator TensionPulseCoroutine()
        {
            pulsing = true;

            Color peak = ropeColorTense * pulsePeakBrightness;
            peak.a = 1f;

            float elapsed = 0f;
            while (elapsed < pulseDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / pulseDuration;
                Color c = Color.Lerp(peak, ropeColorTense, t);
                lineRenderer.startColor = c;
                lineRenderer.endColor   = c;
                yield return null;
            }

            pulsing        = false;
            pulseCoroutine = null;
        }

        // ── Release Scatter ───────────────────────────────────────────────────

        private void CacheCurrentPositions()
        {
            int count = lineRenderer.positionCount;
            if (cachedPositions == null || cachedPositions.Length != count)
                cachedPositions = new Vector3[count];
            lineRenderer.GetPositions(cachedPositions);
        }

        private IEnumerator ScatterRelease()
        {
            scatterActive = true;
            lineRenderer.enabled = true;

            int count = lineRenderer.positionCount;
            Vector3[] start = new Vector3[count];
            Vector3[] offsets = new Vector3[count];

            // Copy cached start positions
            for (int i = 0; i < count; i++)
                start[i] = cachedPositions != null && i < cachedPositions.Length
                    ? cachedPositions[i]
                    : transform.position;

            // Random outward offsets for middle segments
            for (int i = 1; i < count - 1; i++)
            {
                offsets[i] = new Vector3(
                    Random.Range(-1f, 1f),
                    Random.Range(-0.5f, 0.5f),
                    Random.Range(-1f, 1f)).normalized * scatterOutwardSpeed;
            }

            float elapsed = 0f;
            while (elapsed < scatterDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / scatterDuration;

                // Fade out width to 0
                float widthFactor = 1f - t;
                lineRenderer.startWidth = widthFactor * 0.04f;
                lineRenderer.endWidth   = widthFactor * 0.02f;

                Vector3[] positions = new Vector3[count];
                for (int i = 0; i < count; i++)
                    positions[i] = start[i] + offsets[i] * elapsed;

                lineRenderer.SetPositions(positions);
                yield return null;
            }

            lineRenderer.enabled  = false;
            lineRenderer.startWidth = 0.04f;
            lineRenderer.endWidth   = 0.02f;

            scatterActive    = false;
            scatterCoroutine = null;
        }

        // ── Idle Coil ─────────────────────────────────────────────────────────

        private void DrawCoil()
        {
            coilAngle += coilSpinSpeed * Time.deltaTime;

            lineRenderer.enabled = true;

            int    count      = Mathf.Max(lineRenderer.positionCount, 12);
            float  angleStep  = (coilTurns * 360f) / count;
            Vector3 origin    = transform.position;

            Vector3[] positions = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                float t     = (float)i / (count - 1);
                float angle = (coilAngle + i * angleStep) * Mathf.Deg2Rad;
                float r     = coilRadius * Mathf.Sin(t * Mathf.PI); // taper at both ends
                positions[i] = origin
                    + new Vector3(Mathf.Cos(angle) * r, t * coilHeight - coilHeight * 0.5f, Mathf.Sin(angle) * r);
            }

            lineRenderer.positionCount = count;
            lineRenderer.SetPositions(positions);

            lineRenderer.startColor = ropeColorNeutral;
            lineRenderer.endColor   = ropeColorNeutral;
        }
    }
}
