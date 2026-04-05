using System.Collections;
using UnityEngine;

namespace TitanAscent.Environment
{
    /// <summary>
    /// Crystal surface geometry component. Manages emission color, pulsing, and
    /// a bright flash when the player grapples onto this crystal.
    /// Near-summit crystals (y > 8000m) pulse faster and brighter.
    /// Uses MaterialPropertyBlock to avoid shared material modification.
    /// </summary>
    public class CrystalFormation : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        // Inspector
        // -----------------------------------------------------------------------

        [Header("Crystal Properties")]
        [Range(0f, 1f)]
        [SerializeField] private float crystalAge          = 0.5f;  // 0=young/bright, 1=old/dim
        [SerializeField] private float resonanceFrequency  = 1.2f;  // Hz for sine pulse

        [Header("Emission")]
        [SerializeField] private Color  baseEmissionColor  = new Color(0.5f, 1f, 1f); // cyan-white
        [SerializeField] private float  minEmission        = 0.3f;
        [SerializeField] private float  maxEmission        = 1.0f;
        [SerializeField] private float  pulseAmplitude     = 0.15f;

        [Header("Grapple Flash")]
        [SerializeField] private float  flashMultiplier    = 3f;
        [SerializeField] private float  flashHoldDuration  = 0.08f;
        [SerializeField] private float  flashFadeOutTime   = 0.3f;

        [Header("Summit Boost (y > 8000)")]
        [SerializeField] private float  summitFreqMultiplier    = 2.5f;
        [SerializeField] private float  summitBrightnessBoost   = 0.4f;

        // -----------------------------------------------------------------------
        // Private state
        // -----------------------------------------------------------------------

        private Renderer              _renderer;
        private MaterialPropertyBlock _propBlock;
        private float                 _baseEmission;     // derived from crystalAge
        private float                 _currentFrequency;
        private float                 _brightnessBoost;
        private bool                  _isFlashing = false;

        // -----------------------------------------------------------------------
        // Lifecycle
        // -----------------------------------------------------------------------

        private void Awake()
        {
            _renderer  = GetComponent<Renderer>();
            _propBlock = new MaterialPropertyBlock();

            // crystalAge 0 = young (bright), 1 = old (dim)
            _baseEmission = Mathf.Lerp(maxEmission, minEmission, crystalAge);

            // Summit boost
            bool isSummit = transform.position.y > 8000f;
            _currentFrequency = isSummit
                ? resonanceFrequency * summitFreqMultiplier
                : resonanceFrequency;
            _brightnessBoost  = isSummit ? summitBrightnessBoost : 0f;

            // Subscribe to grapple attachment events via SurfaceAnchorPoint
            SurfaceAnchorPoint anchor = GetComponentInChildren<SurfaceAnchorPoint>()
                ?? GetComponent<SurfaceAnchorPoint>();
            // We use the anchor's state directly or listen via event bus
            // For decoupled flash: expose a public method and call from GrappleController
        }

        private void Start()
        {
            // Apply initial emission
            ApplyEmission(_baseEmission + _brightnessBoost);
        }

        // -----------------------------------------------------------------------
        // Update — pulse emission
        // -----------------------------------------------------------------------

        private void Update()
        {
            if (_isFlashing) return;

            float sinVal   = Mathf.Sin(Time.time * _currentFrequency * Mathf.PI * 2f);
            float emission = _baseEmission + sinVal * pulseAmplitude + _brightnessBoost;
            emission       = Mathf.Max(0f, emission);
            ApplyEmission(emission);
        }

        // -----------------------------------------------------------------------
        // Grapple flash — call this from GrappleController / SurfaceAnchorPoint
        // -----------------------------------------------------------------------

        /// <summary>
        /// Triggers the brief bright flash when a player grapples onto this crystal.
        /// Can be called from GrappleController when the attached anchor is a CrystalFormation.
        /// </summary>
        public void TriggerGrappleFlash()
        {
            if (!_isFlashing)
                StartCoroutine(GrappleFlashRoutine());
        }

        private IEnumerator GrappleFlashRoutine()
        {
            _isFlashing = true;

            // Peak flash
            float peakEmission = (_baseEmission + _brightnessBoost) * flashMultiplier;
            ApplyEmission(peakEmission);

            yield return new WaitForSeconds(flashHoldDuration);

            // Fade back to normal
            float elapsed    = 0f;
            float startEmit  = peakEmission;
            float targetEmit = _baseEmission + _brightnessBoost;

            while (elapsed < flashFadeOutTime)
            {
                elapsed += Time.deltaTime;
                float t       = Mathf.Clamp01(elapsed / flashFadeOutTime);
                float current = Mathf.Lerp(startEmit, targetEmit, t);
                ApplyEmission(current);
                yield return null;
            }

            ApplyEmission(targetEmit);
            _isFlashing = false;
        }

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        private void ApplyEmission(float intensity)
        {
            if (_renderer == null) return;

            _renderer.GetPropertyBlock(_propBlock);

            Color emission = baseEmissionColor * intensity;
            _propBlock.SetColor("_EmissionColor", emission);

            // Also tint base color slightly with the emission hue
            Color base_ = Color.Lerp(Color.white, baseEmissionColor, 0.15f);
            _propBlock.SetColor("_BaseColor", base_);
            _propBlock.SetColor("_Color",     base_);

            _renderer.SetPropertyBlock(_propBlock);
        }

        // -----------------------------------------------------------------------
        // Gizmos
        // -----------------------------------------------------------------------

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
        }
    }
}
