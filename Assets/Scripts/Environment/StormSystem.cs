using System.Collections;
using UnityEngine;
using TitanAscent.Systems;

namespace TitanAscent.Environment
{
    /// <summary>
    /// Manages Zone 7 storm conditions (Upper Back Storm, 6500–7800 m).
    /// Activates when the player enters the Zone 7 trigger volume (or when
    /// enabled manually via ActivateStorm / DeactivateStorm).
    ///
    /// Features:
    ///   - Continuous base wind force via WindSystem
    ///   - Random gusts at variable intervals in a random horizontal direction ±30 deg from main wind
    ///   - Lightning warning (light flicker) followed by atmosphere lightning strike
    ///   - Screen shake on each lightning strike via JuiceController
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class StormSystem : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("References")]
        [SerializeField] private Rigidbody playerRb;
        [SerializeField] private JuiceController juiceController;
        [SerializeField] private AtmosphereController atmosphereController;

        [Header("Wind Settings")]
        [Tooltip("Continuous base horizontal wind force in Newtons.")]
        [SerializeField] private float baseWindForce = 40f;

        [Tooltip("Peak force applied during a gust.")]
        [SerializeField] private float gustForce = 70f;

        [Tooltip("Random interval range between gusts (seconds: min, max).")]
        [SerializeField] private Vector2 gustInterval = new Vector2(8f, 20f);

        [Tooltip("Random duration range of each gust (seconds: min, max).")]
        [SerializeField] private Vector2 gustDuration = new Vector2(1.5f, 3f);

        [Header("Lightning Settings")]
        [Tooltip("Random interval range between lightning strikes (seconds: min, max).")]
        [SerializeField] private Vector2 lightningInterval = new Vector2(5f, 15f);

        [Tooltip("Seconds of warning flash before full strike.")]
        [SerializeField] private float lightningWarningTime = 0.5f;

        [Header("Zone Wind Direction")]
        [Tooltip("Base wind direction angle in degrees (horizontal, around Y axis).")]
        [SerializeField] private float windDirectionDeg = 45f;

        [Tooltip("Zone 7 multiplier applied to WindSystem on storm activation.")]
        [SerializeField] private float zone7WindMultiplier = 2.5f;

        // ── Runtime state ─────────────────────────────────────────────────────

        private bool _stormActive;
        private bool _playerInside;
        private Vector3 _mainWindDir;

        private Coroutine _gustCoroutine;
        private Coroutine _lightningCoroutine;

        private WindSystem _windSystem;
        private Light _sunLight;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            Collider col = GetComponent<Collider>();
            col.isTrigger = true;

            _windSystem = FindFirstObjectByType<WindSystem>();
            _sunLight   = FindFirstObjectByType<Light>();

            if (playerRb == null)
            {
                Player.PlayerController pc = FindFirstObjectByType<Player.PlayerController>();
                if (pc != null)
                    playerRb = pc.GetComponent<Rigidbody>();
            }

            if (juiceController == null)
                juiceController = FindFirstObjectByType<JuiceController>();

            if (atmosphereController == null)
                atmosphereController = FindFirstObjectByType<AtmosphereController>();

            // Cache main wind direction from inspector angle
            float rad = windDirectionDeg * Mathf.Deg2Rad;
            _mainWindDir = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)).normalized;
        }

        // ── Trigger callbacks ─────────────────────────────────────────────────

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            _playerInside = true;
            ActivateStorm();
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            _playerInside = false;
            DeactivateStorm();
        }

        // ── Physics ───────────────────────────────────────────────────────────

        private void FixedUpdate()
        {
            if (!_stormActive || !_playerInside || playerRb == null) return;

            // Apply continuous base wind force with subtle turbulence
            float turbulence = Mathf.PerlinNoise(Time.time * 0.7f, 13.37f) * 2f - 1f;
            Vector3 windForce = _mainWindDir * (baseWindForce + turbulence * baseWindForce * 0.15f);
            playerRb.AddForce(windForce, ForceMode.Force);
        }

        // ── Storm Control ─────────────────────────────────────────────────────

        /// <summary>Activates storm effects. Safe to call multiple times.</summary>
        public void ActivateStorm()
        {
            if (_stormActive) return;
            _stormActive = true;

            // Ramp up WindSystem for zone 7
            _windSystem?.SetGlobalWindMultiplier(zone7WindMultiplier);

            _gustCoroutine      = StartCoroutine(GustRoutine());
            _lightningCoroutine = StartCoroutine(LightningRoutine());
        }

        /// <summary>Deactivates storm effects and stops coroutines.</summary>
        public void DeactivateStorm()
        {
            if (!_stormActive) return;
            _stormActive = false;

            if (_gustCoroutine      != null) { StopCoroutine(_gustCoroutine);      _gustCoroutine = null; }
            if (_lightningCoroutine != null) { StopCoroutine(_lightningCoroutine); _lightningCoroutine = null; }

            // Restore wind to a neutral multiplier
            _windSystem?.SetGlobalWindMultiplier(1f);
        }

        // ── Gust Coroutine ────────────────────────────────────────────────────

        private IEnumerator GustRoutine()
        {
            while (_stormActive)
            {
                float waitTime = Random.Range(gustInterval.x, gustInterval.y);
                yield return new WaitForSeconds(waitTime);

                if (!_stormActive || !_playerInside || playerRb == null) continue;

                float duration     = Random.Range(gustDuration.x, gustDuration.y);
                Vector3 gustDir    = PickGustDirection();
                yield return StartCoroutine(ApplyGust(gustDir, duration));
            }
        }

        private IEnumerator ApplyGust(Vector3 direction, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration && _stormActive && _playerInside && playerRb != null)
            {
                // Bell-curve envelope: ramp up then down
                float t        = elapsed / duration;
                float envelope = Mathf.Sin(t * Mathf.PI); // 0→1→0 over duration
                playerRb.AddForce(direction * gustForce * envelope, ForceMode.Force);

                elapsed += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }
        }

        /// <summary>Returns a gust direction ±30 degrees from the main wind direction.</summary>
        private Vector3 PickGustDirection()
        {
            float angleOffset = Random.Range(-30f, 30f);
            Quaternion rotation = Quaternion.AngleAxis(angleOffset, Vector3.up);
            return rotation * _mainWindDir;
        }

        // ── Lightning Coroutine ───────────────────────────────────────────────

        private IEnumerator LightningRoutine()
        {
            while (_stormActive)
            {
                float waitTime = Random.Range(lightningInterval.x, lightningInterval.y);
                yield return new WaitForSeconds(waitTime);

                if (!_stormActive) yield break;

                // Warning flash: dim-bright-dim over warningTime
                yield return StartCoroutine(LightningWarning());

                // Full strike
                TriggerLightningStrike();
            }
        }

        private IEnumerator LightningWarning()
        {
            if (_sunLight == null) yield break;

            float originalIntensity = _sunLight.intensity;
            float warningHalf       = lightningWarningTime * 0.5f;

            // Dim
            float elapsed = 0f;
            while (elapsed < warningHalf)
            {
                elapsed += Time.deltaTime;
                _sunLight.intensity = Mathf.Lerp(originalIntensity, originalIntensity * 0.3f, elapsed / warningHalf);
                yield return null;
            }

            // Bright flash
            elapsed = 0f;
            while (elapsed < warningHalf)
            {
                elapsed += Time.deltaTime;
                _sunLight.intensity = Mathf.Lerp(originalIntensity * 0.3f, originalIntensity * 1.5f, elapsed / warningHalf);
                yield return null;
            }

            _sunLight.intensity = originalIntensity;
        }

        private void TriggerLightningStrike()
        {
            // AtmosphereController manages lightning flash logic internally;
            // call UpdateForAltitude with storm-range altitude to trigger its routine.
            // As a direct trigger we can force a brief high-intensity spike via the sun light.
            if (_sunLight != null)
                StartCoroutine(LightningFlash());

            // Screen shake via JuiceController
            float shakeDistance = 200f; // treat as a heavy impact
            juiceController?.TriggerHardLanding(shakeDistance);
        }

        private IEnumerator LightningFlash()
        {
            if (_sunLight == null) yield break;

            float originalIntensity = _sunLight.intensity;

            _sunLight.intensity = 4f;
            yield return new WaitForSeconds(0.05f);

            float elapsed  = 0f;
            float fallTime = 0.15f;
            while (elapsed < fallTime)
            {
                elapsed += Time.deltaTime;
                _sunLight.intensity = Mathf.Lerp(4f, originalIntensity, elapsed / fallTime);
                yield return null;
            }

            _sunLight.intensity = originalIntensity;

            // Occasional secondary flash
            if (Random.value < 0.35f)
            {
                yield return new WaitForSeconds(Random.Range(0.05f, 0.12f));
                yield return StartCoroutine(LightningFlash());
            }
        }

        // ── Gizmos ────────────────────────────────────────────────────────────

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.5f, 0.4f, 0.8f, 0.4f);
            Collider col = GetComponent<Collider>();
            if (col is BoxCollider box)
            {
                Gizmos.matrix = Matrix4x4.TRS(transform.position + box.center, transform.rotation, Vector3.one);
                Gizmos.DrawWireCube(Vector3.zero, box.size);
                Gizmos.matrix = Matrix4x4.identity;
            }
            else
            {
                Gizmos.DrawWireSphere(transform.position, 20f);
            }

            // Main wind direction arrow
            float rad = windDirectionDeg * Mathf.Deg2Rad;
            Vector3 windDir = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * 8f;
            Gizmos.color = new Color(0.8f, 0.8f, 1f, 0.9f);
            Gizmos.DrawRay(transform.position, windDir);

            // ±30 degree spread lines
            Gizmos.color = new Color(0.8f, 0.8f, 1f, 0.4f);
            for (int side = -1; side <= 1; side += 2)
            {
                float angle = (windDirectionDeg + side * 30f) * Mathf.Deg2Rad;
                Vector3 spreadDir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 6f;
                Gizmos.DrawRay(transform.position, spreadDir);
            }
        }
    }
}
