using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;

namespace TitanAscent.Environment
{
    public enum TitanMovementType
    {
        WingTremor,
        BreathingExpansion,
        MuscleContraction
    }

    [System.Serializable]
    public class TitanMovementEvent
    {
        public TitanMovementType movementType;
        public float amplitude;
        public float duration;
        public int[] affectedZoneIndices;
        public Vector3 direction;
    }

    [System.Serializable]
    public class TitanMovementDefinition
    {
        public TitanMovementType type;
        [Header("Parameters")]
        public float amplitude = 0.5f;
        public float frequency = 0.1f;
        public float duration = 2f;
        public int[] affectedZoneIndices = { 3 };
        public float minTimeBetween = 15f;
        public float maxTimeBetween = 45f;
        [HideInInspector] public float nextTriggerTime;
    }

    public class TitanMovement : MonoBehaviour
    {
        [Header("Movement Definitions")]
        [SerializeField] private TitanMovementDefinition wingTremorDef = new TitanMovementDefinition
        {
            type = TitanMovementType.WingTremor,
            amplitude = 0.8f,
            frequency = 3f,
            duration = 1.5f,
            affectedZoneIndices = new[] { 3, 4 },
            minTimeBetween = 20f,
            maxTimeBetween = 60f
        };

        [SerializeField] private TitanMovementDefinition breathingDef = new TitanMovementDefinition
        {
            type = TitanMovementType.BreathingExpansion,
            amplitude = 0.3f,
            frequency = 0.25f,
            duration = 4f,
            affectedZoneIndices = new[] { 7 },
            minTimeBetween = 8f,
            maxTimeBetween = 12f
        };

        [SerializeField] private TitanMovementDefinition muscleContractionDef = new TitanMovementDefinition
        {
            type = TitanMovementType.MuscleContraction,
            amplitude = 0.4f,
            frequency = 1f,
            duration = 0.8f,
            affectedZoneIndices = new[] { 6, 7 },
            minTimeBetween = 30f,
            maxTimeBetween = 90f
        };

        [Header("Safety")]
        [SerializeField] private float maxSurfaceDisplacement = 2f; // Hard limit on any surface movement

        [Header("Affected Surfaces")]
        [SerializeField] private List<Transform> movableSurfaces = new List<Transform>();

        [Header("Events")]
        public UnityEvent<TitanMovementEvent> OnTitanMovementEvent;

        private ZoneManager zoneManager;
        private Player.PlayerController player;
        private Dictionary<TitanMovementType, TitanMovementDefinition> definitions;
        private List<Coroutine> activeMovements = new List<Coroutine>();
        private Dictionary<Transform, Vector3> originalPositions = new Dictionary<Transform, Vector3>();

        private void Awake()
        {
            zoneManager = FindFirstObjectByType<ZoneManager>();
            player = FindFirstObjectByType<Player.PlayerController>();

            definitions = new Dictionary<TitanMovementType, TitanMovementDefinition>
            {
                { TitanMovementType.WingTremor, wingTremorDef },
                { TitanMovementType.BreathingExpansion, breathingDef },
                { TitanMovementType.MuscleContraction, muscleContractionDef }
            };

            // Record original positions of all movable surfaces
            foreach (Transform surface in movableSurfaces)
            {
                if (surface != null)
                    originalPositions[surface] = surface.position;
            }

            // Stagger initial trigger times
            float now = Time.time;
            wingTremorDef.nextTriggerTime = now + Random.Range(wingTremorDef.minTimeBetween, wingTremorDef.maxTimeBetween);
            breathingDef.nextTriggerTime = now + Random.Range(5f, breathingDef.maxTimeBetween);
            muscleContractionDef.nextTriggerTime = now + Random.Range(muscleContractionDef.minTimeBetween, muscleContractionDef.maxTimeBetween);
        }

        private void Update()
        {
            CheckAndTriggerMovements();
        }

        private void CheckAndTriggerMovements()
        {
            float now = Time.time;

            foreach (var kvp in definitions)
            {
                TitanMovementDefinition def = kvp.Value;
                if (now >= def.nextTriggerTime)
                {
                    TriggerMovement(def);
                    def.nextTriggerTime = now + Random.Range(def.minTimeBetween, def.maxTimeBetween);
                }
            }
        }

        private void TriggerMovement(TitanMovementDefinition def)
        {
            TitanMovementEvent evt = new TitanMovementEvent
            {
                movementType = def.type,
                amplitude = def.amplitude,
                duration = def.duration,
                affectedZoneIndices = def.affectedZoneIndices,
                direction = GetMovementDirection(def.type)
            };

            OnTitanMovementEvent?.Invoke(evt);

            Coroutine co = StartCoroutine(ExecuteMovement(def, evt));
            activeMovements.Add(co);
        }

        private IEnumerator ExecuteMovement(TitanMovementDefinition def, TitanMovementEvent evt)
        {
            List<Transform> affectedSurfaces = GetSurfacesInZones(def.affectedZoneIndices);
            Dictionary<Transform, Vector3> originPositions = new Dictionary<Transform, Vector3>();

            foreach (Transform surface in affectedSurfaces)
            {
                originPositions[surface] = originalPositions.ContainsKey(surface)
                    ? originalPositions[surface]
                    : surface.position;
            }

            float elapsed = 0f;
            float halfDuration = def.duration * 0.5f;

            while (elapsed < def.duration)
            {
                float t = elapsed / def.duration;
                float envelope = Mathf.Sin(t * Mathf.PI); // Ramps up then down

                float displacement = Mathf.Sin(elapsed * def.frequency * Mathf.PI * 2f)
                    * def.amplitude * envelope;

                // Clamp displacement to safety limit
                displacement = Mathf.Clamp(displacement, -maxSurfaceDisplacement, maxSurfaceDisplacement);

                foreach (Transform surface in affectedSurfaces)
                {
                    if (surface == null) continue;
                    Vector3 origin = originPositions[surface];
                    surface.position = origin + evt.direction * displacement;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            // Restore surfaces to original positions
            foreach (Transform surface in affectedSurfaces)
            {
                if (surface == null) continue;
                if (originPositions.ContainsKey(surface))
                    surface.position = originPositions[surface];
            }
        }

        private List<Transform> GetSurfacesInZones(int[] zoneIndices)
        {
            // In a real implementation this would query zone-tagged surfaces.
            // For now, return all tagged movable surfaces (filtered by zone in a full build).
            return movableSurfaces.FindAll(s => s != null);
        }

        private Vector3 GetMovementDirection(TitanMovementType type)
        {
            switch (type)
            {
                case TitanMovementType.WingTremor:
                    return new Vector3(Random.Range(-1f, 1f), Random.Range(-0.2f, 0.2f), 0f).normalized;
                case TitanMovementType.BreathingExpansion:
                    return Vector3.up;
                case TitanMovementType.MuscleContraction:
                    return new Vector3(0f, 0f, Random.Range(-1f, 1f)).normalized;
                default:
                    return Vector3.zero;
            }
        }

        private void OnDisable()
        {
            foreach (Coroutine co in activeMovements)
            {
                if (co != null) StopCoroutine(co);
            }
            activeMovements.Clear();

            // Restore all surfaces
            foreach (var kvp in originalPositions)
            {
                if (kvp.Key != null)
                    kvp.Key.position = kvp.Value;
            }
        }
    }
}
