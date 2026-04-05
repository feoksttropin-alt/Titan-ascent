using System.Collections;
using UnityEngine;
using TitanAscent.Environment;

namespace TitanAscent.VFX
{
    /// <summary>
    /// Visual effects driven by the WindSystem: upward column particles, ambient
    /// horizontal streaks, Zone-7 storm debris with lightning, and high-altitude
    /// dust above 7000 m. All particle rates scale with wind strength.
    /// </summary>
    public class WindVFX : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("Particle Systems")]
        [SerializeField] private ParticleSystem windColumnParticles;
        [SerializeField] private ParticleSystem ambientStreakParticles;
        [SerializeField] private ParticleSystem stormDebrisParticles;
        [SerializeField] private ParticleSystem highAltitudeDustParticles;

        [Header("Lightning")]
        [SerializeField] private Light          lightningLight;
        [SerializeField] private LineRenderer   lightningLine;
        [SerializeField] private CanvasGroup    lightningScreenFlash;

        [Header("Zone Thresholds")]
        [SerializeField] private float ambientStreakZoneMinHeight  = 5500f;  // Zone 5 minimum
        [SerializeField] private float stormZoneMinHeight          = 6500f;  // Zone 7 upper-back storm
        [SerializeField] private float highAltitudeDustMinHeight   = 7000f;

        [Header("Lightning Timing")]
        [SerializeField] private float lightningMinInterval  = 8f;
        [SerializeField] private float lightningMaxInterval  = 15f;
        [SerializeField] private float lightningFlashDuration = 0.04f;
        [SerializeField] private float lightningPeakIntensity = 8f;

        [Header("Lightning Sky Spawn Range")]
        [SerializeField] private float lightningHorizontalRange = 40f;
        [SerializeField] private float lightningHeightAbovePlayer = 120f;

        [Header("Particle Emission Scaling")]
        [SerializeField] private float columnMaxEmissionRate   = 60f;
        [SerializeField] private float streakMaxEmissionRate   = 40f;
        [SerializeField] private float debrisMaxEmissionRate   = 50f;
        [SerializeField] private float dustMaxEmissionRate     = 20f;

        // ── Private state ─────────────────────────────────────────────────────

        private WindSystem   windSystem;
        private ZoneManager  zoneManager;
        private Player.PlayerController player;

        private bool isInWindColumn;
        private bool lightningCoroutineRunning;
        private Coroutine lightningCoroutine;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            windSystem  = FindFirstObjectByType<WindSystem>();
            zoneManager = FindFirstObjectByType<ZoneManager>();
            player      = FindFirstObjectByType<Player.PlayerController>();

            // Start all particle systems stopped
            StopAll();
        }

        private void OnEnable()
        {
            // Wind VFX driven by Update polling — no discrete events to subscribe to
        }

        private void OnDisable()
        {
            StopAll();
            if (lightningCoroutine != null)
            {
                StopCoroutine(lightningCoroutine);
                lightningCoroutineRunning = false;
            }
        }

        private void Update()
        {
            float altitude    = player != null ? player.CurrentHeight : transform.position.y;
            float windStrength = GetCurrentWindStrength(altitude);
            float windNorm    = Mathf.Clamp01(windStrength / 30f); // normalise to 0-1

            UpdateWindColumn(windNorm);
            UpdateAmbientStreaks(altitude, windNorm);
            UpdateStormDebris(altitude, windNorm);
            UpdateHighAltitudeDust(altitude, windNorm);
            ManageLightning(altitude);
        }

        // ── Wind Column ───────────────────────────────────────────────────────

        private void UpdateWindColumn(float windNorm)
        {
            if (windColumnParticles == null) return;

            if (isInWindColumn && windNorm > 0.05f)
            {
                EnsurePlaying(windColumnParticles);
                SetEmissionRate(windColumnParticles, windNorm * columnMaxEmissionRate);
            }
            else
            {
                if (windColumnParticles.isPlaying)
                    windColumnParticles.Stop();
            }
        }

        /// <summary>
        /// Called externally (e.g., by a wind-column trigger volume) to signal the player
        /// has entered or exited an upward wind column.
        /// </summary>
        public void SetInWindColumn(bool inColumn)
        {
            isInWindColumn = inColumn;
        }

        // ── Ambient Horizontal Streaks ─────────────────────────────────────────

        private void UpdateAmbientStreaks(float altitude, float windNorm)
        {
            if (ambientStreakParticles == null) return;

            if (altitude >= ambientStreakZoneMinHeight && windNorm > 0.1f)
            {
                EnsurePlaying(ambientStreakParticles);
                SetEmissionRate(ambientStreakParticles, windNorm * streakMaxEmissionRate);

                // Align streak direction with approximate wind
                AlignParticleSystemToWindDirection(ambientStreakParticles);
            }
            else
            {
                if (ambientStreakParticles.isPlaying)
                    ambientStreakParticles.Stop();
            }
        }

        // ── Storm Debris ──────────────────────────────────────────────────────

        private void UpdateStormDebris(float altitude, float windNorm)
        {
            if (stormDebrisParticles == null) return;

            if (altitude >= stormZoneMinHeight)
            {
                EnsurePlaying(stormDebrisParticles);
                float stormNorm = Mathf.Clamp01(
                    (altitude - stormZoneMinHeight) / 1300f) * windNorm;
                SetEmissionRate(stormDebrisParticles, stormNorm * debrisMaxEmissionRate);
                AlignParticleSystemToWindDirection(stormDebrisParticles);
            }
            else
            {
                if (stormDebrisParticles.isPlaying)
                    stormDebrisParticles.Stop();
            }
        }

        // ── High-Altitude Dust ────────────────────────────────────────────────

        private void UpdateHighAltitudeDust(float altitude, float windNorm)
        {
            if (highAltitudeDustParticles == null) return;

            if (altitude >= highAltitudeDustMinHeight)
            {
                EnsurePlaying(highAltitudeDustParticles);
                float dustNorm = Mathf.Clamp01(
                    (altitude - highAltitudeDustMinHeight) / 3000f) * windNorm;
                SetEmissionRate(highAltitudeDustParticles, dustNorm * dustMaxEmissionRate);
            }
            else
            {
                if (highAltitudeDustParticles.isPlaying)
                    highAltitudeDustParticles.Stop();
            }
        }

        // ── Lightning Management ──────────────────────────────────────────────

        private void ManageLightning(float altitude)
        {
            bool inStorm = altitude >= stormZoneMinHeight;

            if (inStorm && !lightningCoroutineRunning)
            {
                lightningCoroutineRunning = true;
                lightningCoroutine = StartCoroutine(LightningCoroutine());
            }
            else if (!inStorm && lightningCoroutineRunning)
            {
                if (lightningCoroutine != null) StopCoroutine(lightningCoroutine);
                lightningCoroutineRunning = false;
                ClearLightning();
            }
        }

        private IEnumerator LightningCoroutine()
        {
            while (true)
            {
                float wait = Random.Range(lightningMinInterval, lightningMaxInterval);
                yield return new WaitForSeconds(wait);

                yield return StartCoroutine(StrikeLightning());
            }
        }

        private IEnumerator StrikeLightning()
        {
            // Determine bolt endpoints
            Vector3 playerPos = player != null ? player.transform.position : transform.position;
            Vector3 skyPos    = playerPos + new Vector3(
                Random.Range(-lightningHorizontalRange, lightningHorizontalRange),
                lightningHeightAbovePlayer,
                Random.Range(-lightningHorizontalRange, lightningHorizontalRange));

            // Place bolt using LineRenderer
            if (lightningLine != null)
            {
                lightningLine.enabled = true;
                int points = 8;
                lightningLine.positionCount = points;
                for (int i = 0; i < points; i++)
                {
                    float t = (float)i / (points - 1);
                    Vector3 basePos = Vector3.Lerp(skyPos, playerPos, t);
                    // Jitter mid-points
                    if (i > 0 && i < points - 1)
                    {
                        basePos += new Vector3(
                            Random.Range(-3f, 3f),
                            Random.Range(-2f, 2f),
                            Random.Range(-3f, 3f));
                    }
                    lightningLine.SetPosition(i, basePos);
                }
            }

            // Flash directional light
            if (lightningLight != null)
            {
                lightningLight.enabled = true;
                lightningLight.intensity = lightningPeakIntensity;
            }

            // Flash screen
            if (lightningScreenFlash != null)
                lightningScreenFlash.alpha = 0.35f;

            yield return new WaitForSeconds(lightningFlashDuration);

            ClearLightning();
        }

        private void ClearLightning()
        {
            if (lightningLine  != null) lightningLine.enabled  = false;
            if (lightningLight != null)
            {
                lightningLight.intensity = 0f;
                lightningLight.enabled   = false;
            }
            if (lightningScreenFlash != null)
                lightningScreenFlash.alpha = 0f;
        }

        // ── Utilities ─────────────────────────────────────────────────────────

        private float GetCurrentWindStrength(float altitude)
        {
            if (windSystem != null)
                return windSystem.GetWindStrengthAtAltitude(altitude);
            return 0f;
        }

        private void AlignParticleSystemToWindDirection(ParticleSystem ps)
        {
            if (ps == null || windSystem == null) return;

            float altitude = player != null ? player.CurrentHeight : transform.position.y;
            // WindSystem exposes direction indirectly via global direction field (45 deg default).
            // We read the approximate global wind direction from the field exposed on the object.
            // Horizontal wind: use global angle stored in WindSystem (not publicly exposed —
            // use the approximate horizontal normalised vector from the calculated force).
            float angle   = 45f * Mathf.Deg2Rad; // fallback
            Vector3 windDir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

            if (windDir != Vector3.zero)
                ps.transform.rotation = Quaternion.LookRotation(windDir);
        }

        private static void SetEmissionRate(ParticleSystem ps, float rate)
        {
            if (ps == null) return;
            var emission        = ps.emission;
            emission.rateOverTime = rate;
        }

        private static void EnsurePlaying(ParticleSystem ps)
        {
            if (ps == null) return;
            if (!ps.isPlaying)
                ps.Play();
        }

        private void StopAll()
        {
            StopPs(windColumnParticles);
            StopPs(ambientStreakParticles);
            StopPs(stormDebrisParticles);
            StopPs(highAltitudeDustParticles);
            ClearLightning();
        }

        private static void StopPs(ParticleSystem ps)
        {
            if (ps != null && ps.isPlaying)
                ps.Stop();
        }
    }
}
