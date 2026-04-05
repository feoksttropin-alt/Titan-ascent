using System.Collections;
using UnityEngine;

namespace TitanAscent.Environment
{
    /// <summary>
    /// Distant visual indicator of the summit (titan's crystal crown) visible
    /// from far below. Always rendered without fog occlusion. Intensity scales
    /// with player altitude; brightens and adds a vertical beam in Zone 9.
    /// On victory fires a particle burst at the summit.
    /// </summary>
    public class SummitIndicator : MonoBehaviour
    {
        // ------------------------------------------------------------------
        // Inspector
        // ------------------------------------------------------------------

        [Header("Summit Transform")]
        [SerializeField] private Vector3 summitWorldPosition = new Vector3(0f, 10000f, 0f);

        [Header("Billboard Glow")]
        [SerializeField] private SpriteRenderer glowSprite;
        [SerializeField] private Light          glowLight;

        [Header("Vertical Beam")]
        [SerializeField] private GameObject    beamObject;   // stretched quad or LineRenderer parent
        [SerializeField] private LineRenderer  beamLine;

        [Header("Victory Burst")]
        [SerializeField] private ParticleSystem victoryParticles;

        [Header("Intensity Curve")]
        [SerializeField] private float minIntensity     = 0.05f;
        [SerializeField] private float maxIntensity     = 3.0f;

        [Header("Pulse")]
        [SerializeField] private float pulsePeriod      = 4f;   // seconds per full cycle
        [SerializeField] private float pulseVariation   = 0.20f; // ±20% intensity variation

        [Header("Zone Thresholds")]
        [SerializeField] private float beamAltitude    = 9000f;  // Zone 9 threshold
        [SerializeField] private float maxAltitude     = 10000f;

        // ------------------------------------------------------------------
        // Private state
        // ------------------------------------------------------------------

        private Player.PlayerController _player;
        private float                   _baseIntensity;
        private bool                    _beamActive;
        private bool                    _victoryFired;

        // Cache sprite default color
        private Color _spriteBaseColor;

        // ------------------------------------------------------------------
        // Lifecycle
        // ------------------------------------------------------------------

        private void Awake()
        {
            _player = FindFirstObjectByType<Player.PlayerController>();

            // Position self at summit
            transform.position = summitWorldPosition;

            // Disable beam until player reaches Zone 9
            if (beamObject != null) beamObject.SetActive(false);
            if (beamLine   != null) beamLine.enabled = false;

            // Cache sprite colour
            _spriteBaseColor = glowSprite != null ? glowSprite.color : Color.white;

            // Wire victory event if GameManager is available
            var gm = FindFirstObjectByType<Systems.GameManager>();
            if (gm != null)
                gm.OnVictory.AddListener(OnVictory);
        }

        private void OnDestroy()
        {
            var gm = FindFirstObjectByType<Systems.GameManager>();
            if (gm != null)
                gm.OnVictory.RemoveListener(OnVictory);
        }

        private void Update()
        {
            if (_player == null)
            {
                _player = FindFirstObjectByType<Player.PlayerController>();
                return;
            }

            float altitude = _player.CurrentHeight;

            // Always face camera (billboard behaviour)
            FaceCamera();

            // Base intensity: lerp from min to max across full altitude range
            float altFraction = Mathf.Clamp01(altitude / maxAltitude);
            _baseIntensity = Mathf.Lerp(minIntensity, maxIntensity, altFraction);

            // Apply pulse
            float pulse       = 1f + Mathf.Sin(Time.time * Mathf.PI * 2f / pulsePeriod) * pulseVariation;
            float intensity   = _baseIntensity * pulse;

            ApplyIntensity(intensity);

            // Beam management
            if (altitude >= beamAltitude && !_beamActive)
            {
                _beamActive = true;
                ActivateBeam();
            }
            else if (altitude < beamAltitude && _beamActive)
            {
                _beamActive = false;
                DeactivateBeam();
            }

            if (_beamActive)
                UpdateBeam(intensity);
        }

        // ------------------------------------------------------------------
        // Visual helpers
        // ------------------------------------------------------------------

        private void FaceCamera()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            transform.LookAt(cam.transform);
            transform.Rotate(0f, 180f, 0f); // correct for billboard facing
        }

        private void ApplyIntensity(float intensity)
        {
            // Glow sprite
            if (glowSprite != null)
            {
                Color c = _spriteBaseColor;
                c.a = Mathf.Clamp01(intensity * 0.5f);
                glowSprite.color = c;
            }

            // Glow light (no fog — set renderMode to ForceNotImportant and rely on layer)
            if (glowLight != null)
            {
                glowLight.intensity = intensity;
            }
        }

        private void ActivateBeam()
        {
            if (beamObject != null) beamObject.SetActive(true);
            if (beamLine   != null)
            {
                beamLine.enabled = true;
                beamLine.SetPosition(0, summitWorldPosition);
                beamLine.SetPosition(1, summitWorldPosition + Vector3.up * 2000f);
            }
        }

        private void DeactivateBeam()
        {
            if (beamObject != null) beamObject.SetActive(false);
            if (beamLine   != null) beamLine.enabled = false;
        }

        private void UpdateBeam(float intensity)
        {
            if (beamLine == null) return;

            float beamAlpha = Mathf.Clamp01(intensity * 0.35f);
            Color beamColor = new Color(0.85f, 0.95f, 1.0f, beamAlpha);
            beamLine.startColor = beamColor;
            beamLine.endColor   = new Color(beamColor.r, beamColor.g, beamColor.b, 0f);

            float beamWidth     = Mathf.Lerp(0.5f, 2.5f, Mathf.InverseLerp(beamAltitude, maxAltitude,
                _player != null ? _player.CurrentHeight : beamAltitude));
            beamLine.startWidth = beamWidth;
            beamLine.endWidth   = beamWidth * 0.2f;
        }

        // ------------------------------------------------------------------
        // Victory
        // ------------------------------------------------------------------

        private void OnVictory()
        {
            if (_victoryFired) return;
            _victoryFired = true;
            StartCoroutine(VictoryBurst());
        }

        private IEnumerator VictoryBurst()
        {
            // Dramatic brightness flash
            float elapsed = 0f;
            float peakIntensity = maxIntensity * 5f;

            while (elapsed < 0.4f)
            {
                elapsed += Time.deltaTime;
                ApplyIntensity(Mathf.Lerp(_baseIntensity, peakIntensity, elapsed / 0.4f));
                yield return null;
            }

            // Fire particles
            if (victoryParticles != null)
            {
                victoryParticles.transform.position = summitWorldPosition;
                victoryParticles.Play();
            }

            // Settle to sustained bright glow
            yield return new WaitForSeconds(0.2f);

            elapsed = 0f;
            float sustainIntensity = maxIntensity * 2f;
            while (elapsed < 1.0f)
            {
                elapsed += Time.deltaTime;
                ApplyIntensity(Mathf.Lerp(peakIntensity, sustainIntensity, elapsed / 1.0f));
                yield return null;
            }

            _baseIntensity = sustainIntensity;
        }
    }
}
