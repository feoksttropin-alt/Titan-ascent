using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TitanAscent.Environment
{
    /// <summary>
    /// Component for individual titan body parts. Tracks zone membership,
    /// surface type, and drives subtle living movement/ambient effects.
    /// </summary>
    public class TitanBodySegment : MonoBehaviour
    {
        // ------------------------------------------------------------------
        // Inspector fields
        // ------------------------------------------------------------------

        [Header("Identity")]
        public string segmentName = "Segment";
        public int    zoneIndex   = 0;
        public SurfaceType surfaceType = SurfaceType.ScaleArmor;

        [Header("Movement")]
        public bool  isMovingElement    = false;
        public float movementAmplitude  = 0f;

        [Header("Ambient")]
        [SerializeField] private float ambientPulseSpeed = 0.25f; // breathing frequency (Hz)

        // ------------------------------------------------------------------
        // Internal state
        // ------------------------------------------------------------------

        [HideInInspector] public Vector3 basePosition;
        [HideInInspector] public Vector3 currentDisplacement;

        private TitanMovement _titanMovement;
        private Coroutine     _movementCoroutine;
        private Light         _crystalLight;
        private float         _crystalBaseLightIntensity;
        private float         _ambientPhase;

        // ------------------------------------------------------------------
        // Static registry
        // ------------------------------------------------------------------

        public static readonly List<TitanBodySegment> AllSegments = new List<TitanBodySegment>();

        // ------------------------------------------------------------------
        // Lifecycle
        // ------------------------------------------------------------------

        private void Awake()
        {
            basePosition = transform.position;

            // Cache a child Light for Crystal segments
            _crystalLight = GetComponentInChildren<Light>();
            if (_crystalLight != null)
                _crystalBaseLightIntensity = _crystalLight.intensity;

            AllSegments.Add(this);
        }

        private void OnEnable()
        {
            _titanMovement = FindFirstObjectByType<TitanMovement>();
            if (_titanMovement != null)
                _titanMovement.OnTitanMovementEvent.AddListener(OnMovementEvent);
        }

        private void OnDisable()
        {
            if (_movementCoroutine != null)
            {
                StopCoroutine(_movementCoroutine);
                _movementCoroutine = null;
            }
            if (_titanMovement != null)
                _titanMovement.OnTitanMovementEvent.RemoveListener(OnMovementEvent);
        }

        private void OnDestroy()
        {
            AllSegments.Remove(this);
        }

        private void Update()
        {
            _ambientPhase += Time.deltaTime;

            switch (surfaceType)
            {
                case SurfaceType.MuscleSkin:
                    ApplyMusclePulse();
                    break;
                case SurfaceType.WingMembrane:
                    ApplyWingFlutter();
                    break;
                case SurfaceType.CrystalSurface:
                    ApplyCrystalPulse();
                    break;
            }
        }

        // ------------------------------------------------------------------
        // TitanMovement event handler
        // ------------------------------------------------------------------

        private void OnMovementEvent(TitanMovementEvent evt)
        {
            if (!isMovingElement) return;

            // Check whether this segment's zone is affected
            bool affected = false;
            if (evt.affectedZoneIndices != null)
            {
                foreach (int idx in evt.affectedZoneIndices)
                {
                    if (idx == zoneIndex) { affected = true; break; }
                }
            }

            if (!affected) return;

            if (_movementCoroutine != null)
                StopCoroutine(_movementCoroutine);

            _movementCoroutine = StartCoroutine(
                AnimateToDisplacement(evt.direction * movementAmplitude, evt.duration));
        }

        private IEnumerator AnimateToDisplacement(Vector3 targetDisplacement, float duration)
        {
            duration = Mathf.Clamp(duration, 0.5f, 2f);
            float halfDuration = duration * 0.5f;

            // --- Move out ---
            Vector3 startDisp = currentDisplacement;
            float   elapsed   = 0f;

            while (elapsed < halfDuration)
            {
                elapsed          += Time.deltaTime;
                currentDisplacement = Vector3.Lerp(startDisp, targetDisplacement, elapsed / halfDuration);
                transform.position  = basePosition + currentDisplacement;
                yield return null;
            }

            currentDisplacement = targetDisplacement;
            transform.position  = basePosition + currentDisplacement;

            // --- Return ---
            elapsed   = 0f;
            startDisp = currentDisplacement;

            while (elapsed < halfDuration)
            {
                elapsed          += Time.deltaTime;
                currentDisplacement = Vector3.Lerp(startDisp, Vector3.zero, elapsed / halfDuration);
                transform.position  = basePosition + currentDisplacement;
                yield return null;
            }

            currentDisplacement    = Vector3.zero;
            transform.position     = basePosition;
            _movementCoroutine     = null;
        }

        // ------------------------------------------------------------------
        // Ambient surface effects
        // ------------------------------------------------------------------

        /// <summary>Subtle up-down breathing pulse for MuscleSkin.</summary>
        private void ApplyMusclePulse()
        {
            float pulse     = Mathf.Sin(_ambientPhase * ambientPulseSpeed * Mathf.PI * 2f);
            float amplitude = 0.02f;

            // Only offset if not mid-movement-event
            if (_movementCoroutine == null)
            {
                Vector3 offset     = Vector3.up * pulse * amplitude;
                transform.position = basePosition + offset;
                currentDisplacement = offset;
            }
        }

        /// <summary>Tiny random rotation flutter for WingMembrane.</summary>
        private void ApplyWingFlutter()
        {
            if (_movementCoroutine != null) return;

            float noiseX = (Mathf.PerlinNoise(_ambientPhase * 0.7f,  0f) * 2f - 1f) * 0.4f;
            float noiseZ = (Mathf.PerlinNoise(0f, _ambientPhase * 0.5f) * 2f - 1f) * 0.4f;

            transform.localRotation = Quaternion.Euler(noiseX, 0f, noiseZ);
        }

        /// <summary>Light intensity pulse for Crystal segments that have a child Light.</summary>
        private void ApplyCrystalPulse()
        {
            if (_crystalLight == null) return;

            float pulse = (Mathf.Sin(_ambientPhase * 0.4f * Mathf.PI * 2f) + 1f) * 0.5f; // 0–1
            _crystalLight.intensity = Mathf.Lerp(
                _crystalBaseLightIntensity * 0.6f,
                _crystalBaseLightIntensity * 1.4f,
                pulse);
        }

        // ------------------------------------------------------------------
        // Static query helpers
        // ------------------------------------------------------------------

        /// <summary>Returns all segments belonging to the given zone index.</summary>
        public static List<TitanBodySegment> GetSegmentsInZone(int zoneIndex)
        {
            var result = new List<TitanBodySegment>();
            foreach (TitanBodySegment seg in AllSegments)
            {
                if (seg != null && seg.zoneIndex == zoneIndex)
                    result.Add(seg);
            }
            return result;
        }
    }
}
