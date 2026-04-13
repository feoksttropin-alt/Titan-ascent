using System;
using UnityEngine;

namespace TitanAscent.Environment
{
    // -----------------------------------------------------------------------
    // Data
    // -----------------------------------------------------------------------

    [Serializable]
    public class WindParticleSet
    {
        [Tooltip("The particle system to control.")]
        public ParticleSystem particleSystem;

        [Tooltip("Minimum altitude (m) at which this set is active.")]
        public float activationMinHeight;

        [Tooltip("Maximum altitude (m) at which this set is active.")]
        public float activationMaxHeight;

        [Tooltip("Emission rate multiplier at peak intensity for this altitude band.")]
        public float intensityAtPeak = 30f;
    }

    // -----------------------------------------------------------------------
    // WindParticleSystem
    // -----------------------------------------------------------------------

    /// <summary>
    /// Manages wind-driven particle effects (dust, debris, ice crystals) that
    /// reinforce altitude and zone feel. Emission rate and velocity track
    /// <see cref="WindSystem.GetWindStrengthAtAltitude"/> in real time.
    ///
    /// Particle sets are configured per altitude band:
    ///   0–2 000 m  : dust motes, small debris
    ///   2–5 000 m  : larger debris, feather fragments
    ///   5–7 800 m  : ash particles, distant dark specks
    ///   7 800–10 000 m : ice crystals, storm particles, lightning flashes
    ///
    /// During strong wind gusts (WindSystem strength &gt; 2.0) the emission
    /// rate is briefly tripled.
    /// </summary>
    public class WindParticleSystem : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        // Inspector
        // -----------------------------------------------------------------------

        [Header("Altitude Particle Sets")]
        [SerializeField] private WindParticleSet[] particleSets = new WindParticleSet[0];

        [Header("Gust Settings")]
        [SerializeField] private float gustThreshold     = 2f;
        [SerializeField] private float gustEmissionMultiplier = 3f;
        [SerializeField] private float gustFadeSpeed     = 2f;   // rate at which multiplier returns to 1

        [Header("Velocity Influence")]
        [SerializeField] private float baseWindVelocityScale = 0.5f; // m/s of particle velocity per unit wind strength

        // -----------------------------------------------------------------------
        // Private State
        // -----------------------------------------------------------------------

        private WindSystem _windSystem;
        private float      _currentAltitude;
        private float      _currentGustMultiplier = 1f;

        // -----------------------------------------------------------------------
        // Default set definitions — used when particleSets is empty (designer fallback)
        // -----------------------------------------------------------------------

        private static readonly (float min, float max, float peak)[] DefaultBands =
        {
            (0f,    2000f,  25f),  // dust / small debris
            (2000f, 5000f,  35f),  // larger debris / feathers
            (5000f, 7800f,  45f),  // ash / distant specks
            (7800f, 10000f, 60f),  // ice crystals / storm / lightning
        };

        // -----------------------------------------------------------------------
        // Lifecycle
        // -----------------------------------------------------------------------

        private void Awake()
        {
            _windSystem = FindFirstObjectByType<WindSystem>();
        }

        private void Update()
        {
            _currentAltitude = GetPlayerAltitude();
            float windStrength = _windSystem != null
                ? _windSystem.GetWindStrengthAtAltitude(_currentAltitude)
                : 0f;

            UpdateGustMultiplier(windStrength);
            UpdateParticleSets(windStrength);
        }

        // -----------------------------------------------------------------------
        // Core Update Logic
        // -----------------------------------------------------------------------

        private void UpdateGustMultiplier(float windStrength)
        {
            if (windStrength > gustThreshold)
            {
                // Snap to gust multiplier immediately on a strong gust
                _currentGustMultiplier = gustEmissionMultiplier;
            }
            else
            {
                // Decay back toward 1
                _currentGustMultiplier = Mathf.MoveTowards(
                    _currentGustMultiplier,
                    1f,
                    gustFadeSpeed * Time.deltaTime);
            }
        }

        private void UpdateParticleSets(float windStrength)
        {
            if (particleSets == null || particleSets.Length == 0) return;

            foreach (var set in particleSets)
            {
                if (set == null || set.particleSystem == null) continue;

                bool inRange = _currentAltitude >= set.activationMinHeight
                            && _currentAltitude <  set.activationMaxHeight;

                var emission = set.particleSystem.emission;
                var velocity = set.particleSystem.velocityOverLifetime;

                if (!inRange)
                {
                    emission.enabled = false;
                    if (set.particleSystem.isPlaying)
                        set.particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    continue;
                }

                // Ensure the system is playing
                if (!set.particleSystem.isPlaying)
                    set.particleSystem.Play();

                emission.enabled = true;

                // Intensity = how centred we are within the altitude band
                float bandRange   = set.activationMaxHeight - set.activationMinHeight;
                float bandMid     = set.activationMinHeight + bandRange * 0.5f;
                float distFromMid = Mathf.Abs(_currentAltitude - bandMid);
                float falloff     = 1f - Mathf.Clamp01(distFromMid / (bandRange * 0.5f));

                float baseEmission = set.intensityAtPeak * falloff;
                float finalRate    = baseEmission * _currentGustMultiplier;

                // Scale emission by normalised wind strength (0–30 m/s range)
                float windFactor = Mathf.Clamp01(windStrength / 30f);
                finalRate       *= Mathf.Lerp(0.1f, 1f, windFactor);

                emission.rateOverTime = finalRate;

                // Adjust particle velocity to match wind direction
                if (velocity.enabled)
                {
                    float velMag = windStrength * baseWindVelocityScale;
                    velocity.x   = new ParticleSystem.MinMaxCurve(velMag);
                }
            }
        }

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        private float GetPlayerAltitude()
        {
            var player = FindFirstObjectByType<Player.PlayerController>();
            return player != null ? player.CurrentHeight : 0f;
        }

        // -----------------------------------------------------------------------
        // Editor Gizmos
        // -----------------------------------------------------------------------

        private void OnDrawGizmosSelected()
        {
            if (particleSets == null) return;

            foreach (var set in particleSets)
            {
                if (set == null || set.particleSystem == null) continue;

                // Draw a horizontal band indicator at the midpoint altitude
                float mid = (set.activationMinHeight + set.activationMaxHeight) * 0.5f;
                Vector3 center = new Vector3(transform.position.x, mid, transform.position.z);

                Gizmos.color = new Color(0.5f, 0.9f, 1f, 0.3f);
                Gizmos.DrawWireSphere(center, 5f);
                Gizmos.color = new Color(0.5f, 0.9f, 1f, 0.15f);
                Gizmos.DrawWireCube(
                    center,
                    new Vector3(10f, set.activationMaxHeight - set.activationMinHeight, 10f));
            }
        }
    }
}
