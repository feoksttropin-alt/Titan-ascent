using UnityEngine;
using System.Collections.Generic;

namespace TitanAscent.Environment
{
    [System.Serializable]
    public class WindZoneData
    {
        public string zoneName;
        public Vector3 direction = Vector3.right;
        [Range(0f, 50f)] public float strength = 5f;
        [Range(0f, 1f)] public float turbulence = 0.2f;
        public float width = 10f;
        public bool isActive = true;
    }

    public class WindSystem : MonoBehaviour
    {
        [Header("Global Wind")]
        [SerializeField] private float baseWindStrength = 2f;
        [SerializeField] private float maxAltitudeWindStrength = 30f;
        [SerializeField] private float maxAltitude = 10000f;
        [SerializeField] private float strongWindStartAltitude = 6500f; // Zone 7
        [SerializeField] private float globalWindDirection = 45f; // degrees, horizontal

        [Header("Breathing Wind (Zone 8 - The Neck)")]
        [SerializeField] private float breathingPulseFrequency = 0.25f;
        [SerializeField] private float breathingAmplitude = 15f;
        [SerializeField] private float breathingZoneMinHeight = 7800f;
        [SerializeField] private float breathingZoneMaxHeight = 9000f;

        [Header("Turbulence")]
        [SerializeField] private float turbulenceScale = 0.5f;
        [SerializeField] private float turbulenceSpeed = 1.2f;

        [Header("Local Wind Zones")]
        [SerializeField] private List<WindZoneData> windZones = new List<WindZoneData>();

        private Rigidbody playerRigidbody;
        private Player.PlayerController playerController;
        private ZoneManager zoneManager;

        private float turbulenceOffset;
        private float breathingPhase;

        private void Awake()
        {
            playerController = FindFirstObjectByType<Player.PlayerController>();
            if (playerController != null)
                playerRigidbody = playerController.GetComponent<Rigidbody>();

            zoneManager = FindFirstObjectByType<ZoneManager>();
            turbulenceOffset = Random.Range(0f, 100f);
        }

        private void FixedUpdate()
        {
            if (playerRigidbody == null) return;

            float altitude = playerController != null ? playerController.CurrentHeight : playerRigidbody.position.y;
            Vector3 totalWind = CalculateGlobalWind(altitude) + CalculateBreathingWind(altitude);

            // Apply local wind zones
            totalWind += GetLocalWindAtPosition(playerRigidbody.position);

            // Apply wind force to player
            playerRigidbody.AddForce(totalWind, ForceMode.Force);

            breathingPhase += Time.fixedDeltaTime * breathingPulseFrequency * Mathf.PI * 2f;
        }

        private Vector3 CalculateGlobalWind(float altitude)
        {
            float altitudeFactor;

            if (altitude < strongWindStartAltitude)
            {
                altitudeFactor = altitude / maxAltitude;
            }
            else
            {
                // Wind ramps up faster above Zone 7
                float baseContribution = strongWindStartAltitude / maxAltitude;
                float excess = (altitude - strongWindStartAltitude) / (maxAltitude - strongWindStartAltitude);
                altitudeFactor = baseContribution + excess * (1f - baseContribution);
            }

            float windStrength = Mathf.Lerp(baseWindStrength, maxAltitudeWindStrength, altitudeFactor);

            // Turbulence using Perlin noise
            float noiseX = Mathf.PerlinNoise(Time.time * turbulenceSpeed + turbulenceOffset, 0f) * 2f - 1f;
            float noiseZ = Mathf.PerlinNoise(0f, Time.time * turbulenceSpeed + turbulenceOffset) * 2f - 1f;

            float angle = globalWindDirection * Mathf.Deg2Rad;
            Vector3 baseWind = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * windStrength;

            // Add turbulence perpendicular to main wind direction
            Vector3 turbulenceForce = new Vector3(noiseX, noiseX * 0.3f, noiseZ) * windStrength * turbulenceScale;

            return baseWind + turbulenceForce;
        }

        private Vector3 CalculateBreathingWind(float altitude)
        {
            if (altitude < breathingZoneMinHeight || altitude > breathingZoneMaxHeight)
                return Vector3.zero;

            // Pulsing outward wind simulating titan breathing
            float pulse = Mathf.Sin(breathingPhase);
            float intensityFactor = 1f - Mathf.Abs(altitude - (breathingZoneMinHeight + breathingZoneMaxHeight) * 0.5f)
                / ((breathingZoneMaxHeight - breathingZoneMinHeight) * 0.5f);

            // Breathing pushes slightly away from the neck center
            Vector3 breathDirection = Vector3.forward; // Simplified; world-space facing away from titan
            return breathDirection * pulse * breathingAmplitude * intensityFactor;
        }

        private Vector3 GetLocalWindAtPosition(Vector3 position)
        {
            Vector3 total = Vector3.zero;

            foreach (WindZoneData zone in windZones)
            {
                if (!zone.isActive) continue;

                // Check if within zone width
                Vector3 toPlayer = position - transform.position;
                float lateralDist = Vector3.Cross(zone.direction.normalized, toPlayer).magnitude;

                if (lateralDist <= zone.width)
                {
                    float falloff = 1f - (lateralDist / zone.width);
                    float noiseVal = Mathf.PerlinNoise(Time.time * 0.5f, zone.zoneName.GetHashCode() * 0.01f) * 2f - 1f;
                    float turbStrength = zone.strength * zone.turbulence * noiseVal;
                    total += zone.direction.normalized * (zone.strength + turbStrength) * falloff;
                }
            }

            return total;
        }

        public void SetGlobalWindStrength(float multiplier)
        {
            maxAltitudeWindStrength = 30f * multiplier;
        }

        public float GetWindStrengthAtAltitude(float altitude)
        {
            return CalculateGlobalWind(altitude).magnitude;
        }

        private void OnDrawGizmosSelected()
        {
            foreach (WindZoneData zone in windZones)
            {
                if (!zone.isActive) continue;
                Gizmos.color = new Color(0.5f, 0.8f, 1f, 0.4f);
                Gizmos.DrawRay(transform.position, zone.direction.normalized * zone.strength);
                Gizmos.DrawWireSphere(transform.position, zone.width);
            }
        }
    }
}
