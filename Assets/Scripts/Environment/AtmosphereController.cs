using System.Collections;
using UnityEngine;

namespace TitanAscent.Environment
{
    /// <summary>
    /// Manages sky, fog, and directional lighting as the player climbs.
    /// Driven by player altitude (0–10 000 m). Call UpdateForAltitude(float)
    /// from ZoneTransitionManager or directly each frame.
    /// </summary>
    public class AtmosphereController : MonoBehaviour
    {
        // ------------------------------------------------------------------
        // Inspector
        // ------------------------------------------------------------------

        [Header("Scene References")]
        [SerializeField] private Light sunLight;

        [Header("Transition")]
        [SerializeField] private float transitionSpeed = 2f;

        // ------------------------------------------------------------------
        // Altitude band definitions
        // ------------------------------------------------------------------

        private struct AtmosphereBand
        {
            public float minAlt;
            public float maxAlt;
            public Color lightColor;
            public float lightIntensity;
            public Color fogColor;
            public float fogDensity;
            public Color ambientColor;
        }

        private static readonly AtmosphereBand[] Bands = new AtmosphereBand[]
        {
            // 0 – dawn warm
            new AtmosphereBand
            {
                minAlt = 0f, maxAlt = 500f,
                lightColor    = new Color(1.0f, 0.85f, 0.70f),
                lightIntensity = 0.8f,
                fogColor      = new Color(0.95f, 0.75f, 0.65f),
                fogDensity    = 0.008f,
                ambientColor  = new Color(0.90f, 0.70f, 0.60f)
            },
            // 1 – clear morning
            new AtmosphereBand
            {
                minAlt = 500f, maxAlt = 2000f,
                lightColor    = new Color(1.0f, 0.95f, 0.90f),
                lightIntensity = 1.0f,
                fogColor      = new Color(0.85f, 0.90f, 1.0f),
                fogDensity    = 0.003f,
                ambientColor  = new Color(0.70f, 0.75f, 0.85f)
            },
            // 2 – bright blue sky
            new AtmosphereBand
            {
                minAlt = 2000f, maxAlt = 4500f,
                lightColor    = new Color(0.90f, 0.95f, 1.0f),
                lightIntensity = 1.2f,
                fogColor      = new Color(0.60f, 0.75f, 1.0f),
                fogDensity    = 0.001f,
                ambientColor  = new Color(0.55f, 0.68f, 0.90f)
            },
            // 3 – high altitude blue
            new AtmosphereBand
            {
                minAlt = 4500f, maxAlt = 6500f,
                lightColor    = new Color(0.70f, 0.80f, 1.0f),
                lightIntensity = 1.1f,
                fogColor      = new Color(0.50f, 0.60f, 0.85f),
                fogDensity    = 0.002f,
                ambientColor  = new Color(0.45f, 0.55f, 0.80f)
            },
            // 4 – stormy grey-blue
            new AtmosphereBand
            {
                minAlt = 6500f, maxAlt = 8000f,
                lightColor    = new Color(0.50f, 0.55f, 0.65f),
                lightIntensity = 0.7f,
                fogColor      = new Color(0.40f, 0.43f, 0.52f),
                fogDensity    = 0.012f,
                ambientColor  = new Color(0.35f, 0.38f, 0.48f)
            },
            // 5 – dark storm
            new AtmosphereBand
            {
                minAlt = 8000f, maxAlt = 9500f,
                lightColor    = new Color(0.30f, 0.35f, 0.40f),
                lightIntensity = 0.4f,
                fogColor      = new Color(0.22f, 0.25f, 0.32f),
                fogDensity    = 0.025f,
                ambientColor  = new Color(0.20f, 0.23f, 0.30f)
            },
            // 6 – above the storm, crystal light
            new AtmosphereBand
            {
                minAlt = 9500f, maxAlt = 10000f,
                lightColor    = new Color(0.80f, 0.90f, 1.0f),
                lightIntensity = 0.9f,
                fogColor      = new Color(0.70f, 0.80f, 0.95f),
                fogDensity    = 0.002f,
                ambientColor  = new Color(0.65f, 0.78f, 0.95f)
            }
        };

        // Lightning is active in bands 4 (stormy) and 5 (dark storm)
        private const int LightningBandStart = 4;
        private const int LightningBandEnd   = 5;

        // ------------------------------------------------------------------
        // Runtime state
        // ------------------------------------------------------------------

        private Color   _targetLightColor;
        private float   _targetLightIntensity;
        private Color   _targetFogColor;
        private float   _targetFogDensity;
        private Color   _targetAmbientColor;

        private float   _currentAltitude;
        private bool    _lightningRoutineRunning;
        private Coroutine _lightningCoroutine;
        private float   _baseLightIntensity;

        // ------------------------------------------------------------------
        // Lifecycle
        // ------------------------------------------------------------------

        private void Awake()
        {
            if (sunLight == null)
                sunLight = FindFirstObjectByType<Light>();

            _baseLightIntensity = sunLight != null ? sunLight.intensity : 1f;

            // Initialise render settings to band 0
            ApplyBandImmediate(Bands[0]);
        }

        private void Update()
        {
            // Smooth lerp toward targets every frame
            float t = Time.deltaTime * transitionSpeed;

            if (sunLight != null)
            {
                sunLight.color     = Color.Lerp(sunLight.color, _targetLightColor, t);
                sunLight.intensity = Mathf.Lerp(sunLight.intensity, _targetLightIntensity, t);
            }

            RenderSettings.fogDensity  = Mathf.Lerp(RenderSettings.fogDensity,  _targetFogDensity,   t);
            RenderSettings.fogColor    = Color.Lerp(RenderSettings.fogColor,    _targetFogColor,     t);
            RenderSettings.ambientLight = Color.Lerp(RenderSettings.ambientLight, _targetAmbientColor, t);
        }

        // ------------------------------------------------------------------
        // Public API
        // ------------------------------------------------------------------

        /// <summary>
        /// Primary entry point. Called by ZoneTransitionManager or directly each frame.
        /// </summary>
        public void UpdateForAltitude(float altitude)
        {
            _currentAltitude = altitude;

            // Find the two surrounding bands and blend between them
            int bandIndex = GetBandIndex(altitude);
            AtmosphereBand band = Bands[bandIndex];

            // Interpolate between current band and next (if there is one) for smoothness
            if (bandIndex < Bands.Length - 1)
            {
                AtmosphereBand next  = Bands[bandIndex + 1];
                float          inner = Mathf.InverseLerp(band.minAlt, band.maxAlt, altitude);

                _targetLightColor     = Color.Lerp(band.lightColor,    next.lightColor,    inner);
                _targetLightIntensity = Mathf.Lerp(band.lightIntensity, next.lightIntensity, inner);
                _targetFogColor       = Color.Lerp(band.fogColor,       next.fogColor,       inner);
                _targetFogDensity     = Mathf.Lerp(band.fogDensity,     next.fogDensity,     inner);
                _targetAmbientColor   = Color.Lerp(band.ambientColor,   next.ambientColor,   inner);
            }
            else
            {
                _targetLightColor     = band.lightColor;
                _targetLightIntensity = band.lightIntensity;
                _targetFogColor       = band.fogColor;
                _targetFogDensity     = band.fogDensity;
                _targetAmbientColor   = band.ambientColor;
            }

            // Cache base intensity for lightning (use target so it tracks the lerped value)
            _baseLightIntensity = _targetLightIntensity;

            // Lightning management
            bool inLightningZone = bandIndex >= LightningBandStart && bandIndex <= LightningBandEnd;
            if (inLightningZone && !_lightningRoutineRunning)
            {
                _lightningCoroutine    = StartCoroutine(LightningRoutine());
                _lightningRoutineRunning = true;
            }
            else if (!inLightningZone && _lightningRoutineRunning)
            {
                if (_lightningCoroutine != null)
                    StopCoroutine(_lightningCoroutine);
                _lightningRoutineRunning = false;
            }
        }

        // ------------------------------------------------------------------
        // Lightning
        // ------------------------------------------------------------------

        private IEnumerator LightningRoutine()
        {
            while (true)
            {
                // Frequency increases with altitude in the storm bands
                float altFraction = Mathf.InverseLerp(
                    Bands[LightningBandStart].minAlt,
                    Bands[LightningBandEnd].maxAlt,
                    _currentAltitude);

                float waitMin = Mathf.Lerp(6f, 1.5f, altFraction);
                float waitMax = Mathf.Lerp(14f, 4f,  altFraction);

                yield return new WaitForSeconds(Random.Range(waitMin, waitMax));

                // Flash
                yield return StartCoroutine(LightningFlash());
            }
        }

        private IEnumerator LightningFlash()
        {
            if (sunLight == null) yield break;

            float originalIntensity = sunLight.intensity;
            float peakIntensity     = 3.5f;

            // Snap to peak
            sunLight.intensity = peakIntensity;
            yield return new WaitForSeconds(0.05f);

            // Quick falloff
            float elapsed  = 0f;
            float fallTime = 0.12f;
            while (elapsed < fallTime)
            {
                elapsed           += Time.deltaTime;
                sunLight.intensity = Mathf.Lerp(peakIntensity, originalIntensity, elapsed / fallTime);
                yield return null;
            }

            sunLight.intensity = originalIntensity;

            // Occasional double-flash
            if (Random.value < 0.3f)
            {
                yield return new WaitForSeconds(Random.Range(0.08f, 0.18f));
                yield return StartCoroutine(LightningFlash());
            }
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private static int GetBandIndex(float altitude)
        {
            for (int i = Bands.Length - 1; i >= 0; i--)
            {
                if (altitude >= Bands[i].minAlt)
                    return i;
            }
            return 0;
        }

        private void ApplyBandImmediate(AtmosphereBand band)
        {
            _targetLightColor     = band.lightColor;
            _targetLightIntensity = band.lightIntensity;
            _targetFogColor       = band.fogColor;
            _targetFogDensity     = band.fogDensity;
            _targetAmbientColor   = band.ambientColor;

            if (sunLight != null)
            {
                sunLight.color     = band.lightColor;
                sunLight.intensity = band.lightIntensity;
            }

            RenderSettings.fog         = true;
            RenderSettings.fogMode     = FogMode.Exponential;
            RenderSettings.fogDensity  = band.fogDensity;
            RenderSettings.fogColor    = band.fogColor;
            RenderSettings.ambientLight = band.ambientColor;
        }
    }
}
