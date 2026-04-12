using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TitanAscent.Systems;
using TitanAscent.Optimization;

namespace TitanAscent.VFX
{
    /// <summary>
    /// Visual effects triggered during falls: speed-line overlay, FOV pulse,
    /// catastrophic-fall vignette, landing dust explosion, and emergency-window
    /// gold-edge glow. Subscribes to FallTracker events.
    /// </summary>
    public class FallVFX : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("Speed Lines (Canvas overlay)")]
        [SerializeField] private RectTransform speedLinesContainer;   // 16 radial Image children

        [Header("Red Screen Vignette")]
        [SerializeField] private CanvasGroup screenVignetteRed;

        [Header("Gold Edge Glow")]
        [SerializeField] private CanvasGroup screenGoldEdge;

        [Header("Landing Dust")]
        [SerializeField] private ParticleSystem landingDustPrefab;

        [Header("Thresholds")]
        [SerializeField] private float speedLinesStartFall   = 20f;   // metres fallen before lines appear
        [SerializeField] private float fovPulseStartFall     = 100f;  // metres
        [SerializeField] private float catastrophicFall      = 500f;  // metres
        [SerializeField] private float maxSpeedForLines      = 50f;   // m/s at which alpha is 0.6
        [SerializeField] private float fovBoost              = 5f;    // degrees added during FOV pulse
        [SerializeField] private float fovPulseDuration      = 0.2f;

        [Header("Vignette")]
        [SerializeField] private float vignetteMaxAlpha      = 0.5f;
        [SerializeField] private float goldEdgeFadeDuration  = 0.5f;

        [Header("Landing Dust Scaling")]
        [SerializeField] private float maxFallDistanceForDust = 500f;  // maps to max particle count

        // ── Private state ─────────────────────────────────────────────────────

        private FallTracker fallTracker;
        private Camera      mainCamera;

        private Image[] speedLineImages;         // cached children of speedLinesContainer
        private float   defaultFov = 60f;
        private float   currentFallDistance;
        private float   currentFallSpeed;

        private Coroutine fovCoroutine;
        private Coroutine goldEdgeCoroutine;

        private bool isFalling;
        private bool fovPulseFired;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            fallTracker = GetComponent<FallTracker>();
            if (fallTracker == null)
                fallTracker = GetComponentInParent<FallTracker>();

            mainCamera = Camera.main;
            if (mainCamera != null)
                defaultFov = mainCamera.fieldOfView;

            CacheSpeedLineImages();
            ResetUI();
        }

        private void OnEnable()
        {
            if (fallTracker != null)
            {
                fallTracker.OnFallDistanceUpdate.AddListener(HandleFallDistanceUpdate);
                fallTracker.OnFallCompleted.AddListener(HandleFallCompleted);
                fallTracker.OnEmergencyWindowOpen.AddListener(HandleEmergencyWindowOpen);
            }
        }

        private void OnDisable()
        {
            if (fallTracker != null)
            {
                fallTracker.OnFallDistanceUpdate.RemoveListener(HandleFallDistanceUpdate);
                fallTracker.OnFallCompleted.RemoveListener(HandleFallCompleted);
                fallTracker.OnEmergencyWindowOpen.RemoveListener(HandleEmergencyWindowOpen);
            }

            ResetUI();
        }

        private void Update()
        {
            if (!isFalling) return;

            // Sample current vertical speed from Rigidbody if available
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
                currentFallSpeed = Mathf.Abs(Mathf.Min(0f, rb.velocity.y));

            UpdateSpeedLines();
            UpdateCatastrophicVignette();
            MaybeTriggerFovPulse();
        }

        // ── Event Handlers ────────────────────────────────────────────────────

        private void HandleFallDistanceUpdate(float distance)
        {
            isFalling       = true;
            currentFallDistance = distance;
            fovPulseFired   = fovPulseFired && (distance >= fovPulseStartFall);
        }

        private void HandleFallCompleted(FallData data)
        {
            isFalling           = false;
            currentFallDistance = 0f;
            currentFallSpeed    = 0f;

            // Hide speed lines
            SetSpeedLinesAlpha(0f);

            // Hide catastrophic vignette
            if (screenVignetteRed != null)
                screenVignetteRed.alpha = 0f;

            fovPulseFired = false;

            // Restore FOV immediately
            if (fovCoroutine != null) StopCoroutine(fovCoroutine);
            if (mainCamera != null)
                mainCamera.fieldOfView = defaultFov;

            // Landing dust scaled by fall distance
            if (data.distance >= speedLinesStartFall)
                SpawnLandingDust(data.distance);
        }

        private void HandleEmergencyWindowOpen()
        {
            // Gold screen-edge glow
            if (goldEdgeCoroutine != null) StopCoroutine(goldEdgeCoroutine);
            goldEdgeCoroutine = StartCoroutine(GoldEdgeCoroutine());
        }

        // ── Speed Lines ───────────────────────────────────────────────────────

        private void CacheSpeedLineImages()
        {
            if (speedLinesContainer == null) return;

            speedLineImages = speedLinesContainer.GetComponentsInChildren<Image>(includeInactive: true);
        }

        private void UpdateSpeedLines()
        {
            if (currentFallDistance < speedLinesStartFall)
            {
                SetSpeedLinesAlpha(0f);
                return;
            }

            float alpha = Mathf.Lerp(0f, 0.6f,
                Mathf.InverseLerp(0f, maxSpeedForLines, currentFallSpeed));
            SetSpeedLinesAlpha(alpha);
        }

        private void SetSpeedLinesAlpha(float alpha)
        {
            if (speedLineImages == null) return;
            foreach (Image img in speedLineImages)
            {
                if (img == null) continue;
                Color c = img.color;
                c.a = alpha;
                img.color = c;
            }
        }

        // ── FOV Pulse ─────────────────────────────────────────────────────────

        private void MaybeTriggerFovPulse()
        {
            if (fovPulseFired) return;
            if (currentFallDistance < fovPulseStartFall) return;

            fovPulseFired = true;
            if (fovCoroutine != null) StopCoroutine(fovCoroutine);
            fovCoroutine = StartCoroutine(FovPulseCoroutine());
        }

        private IEnumerator FovPulseCoroutine()
        {
            if (mainCamera == null) yield break;

            float target   = defaultFov + fovBoost;
            float half     = fovPulseDuration * 0.5f;
            float elapsed  = 0f;

            // Expand
            while (elapsed < half)
            {
                elapsed += Time.deltaTime;
                mainCamera.fieldOfView = Mathf.Lerp(defaultFov, target, elapsed / half);
                yield return null;
            }

            // Contract
            elapsed = 0f;
            while (elapsed < half)
            {
                elapsed += Time.deltaTime;
                mainCamera.fieldOfView = Mathf.Lerp(target, defaultFov, elapsed / half);
                yield return null;
            }

            mainCamera.fieldOfView = defaultFov;
            fovCoroutine = null;
        }

        // ── Catastrophic Vignette ─────────────────────────────────────────────

        private void UpdateCatastrophicVignette()
        {
            if (screenVignetteRed == null) return;
            if (currentFallDistance < catastrophicFall)
            {
                screenVignetteRed.alpha = 0f;
                return;
            }

            // Pulse alpha with fall speed
            float baseAlpha = Mathf.InverseLerp(catastrophicFall, catastrophicFall + 300f, currentFallDistance)
                              * vignetteMaxAlpha;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.PI * (1f + currentFallSpeed * 0.04f));
            screenVignetteRed.alpha = baseAlpha * pulse;
        }

        // ── Gold Edge (Emergency Window) ──────────────────────────────────────

        private IEnumerator GoldEdgeCoroutine()
        {
            if (screenGoldEdge == null) yield break;

            // Fade in
            float elapsed = 0f;
            while (elapsed < goldEdgeFadeDuration)
            {
                elapsed += Time.deltaTime;
                screenGoldEdge.alpha = Mathf.Lerp(0f, 1f, elapsed / goldEdgeFadeDuration);
                yield return null;
            }
            screenGoldEdge.alpha = 1f;

            // Fade out
            elapsed = 0f;
            while (elapsed < goldEdgeFadeDuration)
            {
                elapsed += Time.deltaTime;
                screenGoldEdge.alpha = Mathf.Lerp(1f, 0f, elapsed / goldEdgeFadeDuration);
                yield return null;
            }
            screenGoldEdge.alpha = 0f;
            goldEdgeCoroutine = null;
        }

        // ── Landing Dust ──────────────────────────────────────────────────────

        private void SpawnLandingDust(float fallDistance)
        {
            if (landingDustPrefab == null) return;
            if (ObjectPooler.Instance == null) return;

            GameObject dustGO = ObjectPooler.Instance.Get(
                landingDustPrefab.gameObject, transform.position, Quaternion.identity);

            if (dustGO == null) return;

            ParticleSystem ps = dustGO.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                int count = Mathf.RoundToInt(
                    Mathf.Lerp(10f, 80f,
                        Mathf.InverseLerp(speedLinesStartFall, maxFallDistanceForDust, fallDistance)));

                var main = ps.main;
                main.startSpeedMultiplier = Mathf.Lerp(1f, 5f,
                    Mathf.InverseLerp(speedLinesStartFall, maxFallDistanceForDust, fallDistance));

                ps.Emit(count);
            }

            ObjectPooler.Instance.ReturnAfter(dustGO, 2.5f);
        }

        // ── Utilities ─────────────────────────────────────────────────────────

        private void ResetUI()
        {
            SetSpeedLinesAlpha(0f);

            if (screenVignetteRed != null) screenVignetteRed.alpha = 0f;
            if (screenGoldEdge    != null) screenGoldEdge.alpha    = 0f;

            if (mainCamera != null)
                mainCamera.fieldOfView = defaultFov;
        }
    }
}
