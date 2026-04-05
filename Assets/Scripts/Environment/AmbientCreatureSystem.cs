using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TitanAscent.Environment
{
    // -----------------------------------------------------------------------
    // Creature type enum
    // -----------------------------------------------------------------------

    public enum AmbientCreatureType
    {
        DistantBird,
        LargeFlyer,
        StormCreature
    }

    // -----------------------------------------------------------------------
    // Data for a spawnable creature definition
    // -----------------------------------------------------------------------

    [System.Serializable]
    public class CreatureDefinition
    {
        public AmbientCreatureType type;
        public GameObject          prefab;

        [Header("Altitude Band")]
        public float minAltitude = 0f;
        public float maxAltitude = 5000f;

        [Header("Pool")]
        [Range(1, 20)] public int poolSize = 8;

        [Header("Flock / Spawn")]
        [Range(1, 20)] public int  minGroupSize = 5;
        [Range(1, 20)] public int  maxGroupSize = 12;
        public float flightSpeed  = 6f;
        public float pathRadius   = 80f;           // radius from player for creature paths

        [HideInInspector] public List<GameObject> pool = new List<GameObject>();
    }

    // -----------------------------------------------------------------------
    // Runtime creature instance data
    // -----------------------------------------------------------------------

    internal class CreatureInstance
    {
        public GameObject       go;
        public AmbientCreatureType type;
        public Vector3[]        splinePath;         // world-space waypoints
        public int              pathIndex;
        public float            speed;
        public bool             active;
        public float            altitude;

        // For flocks
        public int flockId = -1;
        public Vector3 flockOffset;
    }

    // -----------------------------------------------------------------------
    // AmbientCreatureSystem
    // -----------------------------------------------------------------------

    public class AmbientCreatureSystem : MonoBehaviour
    {
        // ------------------------------------------------------------------
        // Inspector
        // ------------------------------------------------------------------

        [Header("Creature Definitions")]
        [SerializeField] private CreatureDefinition[] creatureDefinitions = new CreatureDefinition[]
        {
            new CreatureDefinition
            {
                type        = AmbientCreatureType.DistantBird,
                minAltitude = 0f,
                maxAltitude = 6000f,
                poolSize    = 16,
                minGroupSize = 5,
                maxGroupSize = 15,
                flightSpeed = 7f,
                pathRadius  = 60f
            },
            new CreatureDefinition
            {
                type        = AmbientCreatureType.LargeFlyer,
                minAltitude = 1000f,
                maxAltitude = 8000f,
                poolSize    = 3,
                minGroupSize = 1,
                maxGroupSize = 1,
                flightSpeed = 4f,
                pathRadius  = 150f
            },
            new CreatureDefinition
            {
                type        = AmbientCreatureType.StormCreature,
                minAltitude = 6500f,
                maxAltitude = 10000f,
                poolSize    = 4,
                minGroupSize = 1,
                maxGroupSize = 1,
                flightSpeed = 5f,
                pathRadius  = 100f
            }
        };

        [Header("Culling")]
        [SerializeField] private float altitudeBandBuffer   = 500f;
        [SerializeField] private float scatterRadius        = 50f;    // scatter distance when player approaches flock

        [Header("Timing")]
        [SerializeField] private float spawnCheckInterval   = 3f;
        [SerializeField] private float stormCircleInterval  = 6f;     // StormCreature dive animation period

        // ------------------------------------------------------------------
        // Private state
        // ------------------------------------------------------------------

        private Player.PlayerController _player;
        private List<CreatureInstance>  _instances = new List<CreatureInstance>();
        private float                   _spawnTimer;
        private int                     _nextFlockId;

        // ------------------------------------------------------------------
        // Lifecycle
        // ------------------------------------------------------------------

        private void Awake()
        {
            _player = FindFirstObjectByType<Player.PlayerController>();
            BuildPools();
        }

        private void OnDestroy()
        {
            // Pools are parented to this GO so they'll be destroyed automatically
        }

        private void Update()
        {
            if (_player == null)
            {
                _player = FindFirstObjectByType<Player.PlayerController>();
                return;
            }

            float altitude = _player.CurrentHeight;

            _spawnTimer += Time.deltaTime;
            if (_spawnTimer >= spawnCheckInterval)
            {
                _spawnTimer = 0f;
                ManageCreaturePopulation(altitude);
            }

            MoveCreatures(altitude);
        }

        // ------------------------------------------------------------------
        // Pool construction
        // ------------------------------------------------------------------

        private void BuildPools()
        {
            foreach (CreatureDefinition def in creatureDefinitions)
            {
                def.pool.Clear();
                if (def.prefab == null) continue;

                for (int i = 0; i < def.poolSize; i++)
                {
                    GameObject go = Instantiate(def.prefab, transform);
                    go.SetActive(false);
                    go.name = $"{def.type}_Pool_{i}";
                    def.pool.Add(go);
                }
            }
        }

        // ------------------------------------------------------------------
        // Population management
        // ------------------------------------------------------------------

        private void ManageCreaturePopulation(float altitude)
        {
            // Deactivate out-of-band instances
            for (int i = _instances.Count - 1; i >= 0; i--)
            {
                CreatureInstance inst = _instances[i];
                if (!inst.active) continue;

                CreatureDefinition def = FindDef(inst.type);
                if (def == null) continue;

                float bandMin = def.minAltitude - altitudeBandBuffer;
                float bandMax = def.maxAltitude + altitudeBandBuffer;

                if (altitude < bandMin || altitude > bandMax)
                    ReturnToPool(inst, def);
            }

            // Spawn new creatures for in-band definitions
            foreach (CreatureDefinition def in creatureDefinitions)
            {
                if (def.prefab == null) continue;

                float bandMin = def.minAltitude - altitudeBandBuffer;
                float bandMax = def.maxAltitude + altitudeBandBuffer;

                if (altitude < bandMin || altitude > bandMax) continue;

                // Count active instances of this type
                int active = CountActiveOfType(def.type);
                if (active >= def.poolSize) continue;

                // Try to spawn a group
                int groupSize = def.type == AmbientCreatureType.DistantBird
                    ? Random.Range(def.minGroupSize, def.maxGroupSize + 1)
                    : 1;

                SpawnGroup(def, groupSize, altitude);
            }
        }

        private void SpawnGroup(CreatureDefinition def, int count, float playerAltitude)
        {
            int flockId    = def.type == AmbientCreatureType.DistantBird ? _nextFlockId++ : -1;
            Vector3[] path = GenerateSplinePath(def, playerAltitude);

            for (int i = 0; i < count; i++)
            {
                GameObject go = GetFromPool(def);
                if (go == null) break;

                go.SetActive(true);
                go.transform.position = path[0] + (def.type == AmbientCreatureType.DistantBird
                    ? new Vector3(Random.Range(-5f, 5f), Random.Range(-2f, 2f), Random.Range(-5f, 5f))
                    : Vector3.zero);

                CreatureInstance inst = new CreatureInstance
                {
                    go          = go,
                    type        = def.type,
                    splinePath  = path,
                    pathIndex   = 0,
                    speed       = def.flightSpeed * Random.Range(0.9f, 1.1f),
                    active      = true,
                    altitude    = playerAltitude,
                    flockId     = flockId,
                    flockOffset = def.type == AmbientCreatureType.DistantBird
                        ? new Vector3(Random.Range(-4f, 4f), Random.Range(-1f, 1f), Random.Range(-4f, 4f))
                        : Vector3.zero
                };
                _instances.Add(inst);
            }
        }

        // ------------------------------------------------------------------
        // Creature movement
        // ------------------------------------------------------------------

        private void MoveCreatures(float playerAltitude)
        {
            Vector3 playerPos = _player.transform.position;

            foreach (CreatureInstance inst in _instances)
            {
                if (!inst.active || inst.go == null) continue;

                // Flock scatter when player is close
                if (inst.type == AmbientCreatureType.DistantBird)
                {
                    float distToPlayer = Vector3.Distance(inst.go.transform.position, playerPos);
                    if (distToPlayer < scatterRadius)
                    {
                        Vector3 scatterDir = (inst.go.transform.position - playerPos).normalized;
                        inst.go.transform.position += scatterDir * inst.speed * 2f * Time.deltaTime;
                        continue;
                    }
                }

                // Follow spline path
                if (inst.splinePath == null || inst.splinePath.Length == 0) continue;

                Vector3 target = inst.splinePath[inst.pathIndex] + inst.flockOffset;

                // For storm creatures, add a circling drift offset
                if (inst.type == AmbientCreatureType.StormCreature)
                {
                    float angle   = Time.time * 0.4f + inst.flockId * 1.2f;
                    target += new Vector3(
                        Mathf.Cos(angle) * 15f,
                        Mathf.Sin(Time.time * 0.3f) * 8f,
                        Mathf.Sin(angle) * 15f);
                }

                float step       = inst.speed * Time.deltaTime;
                inst.go.transform.position = Vector3.MoveTowards(
                    inst.go.transform.position, target, step);

                // Face direction of travel
                Vector3 moveDir = (target - inst.go.transform.position);
                if (moveDir.sqrMagnitude > 0.001f)
                    inst.go.transform.forward = Vector3.Lerp(
                        inst.go.transform.forward, moveDir.normalized, Time.deltaTime * 3f);

                // Advance waypoint
                if (Vector3.Distance(inst.go.transform.position, target) < 2f)
                {
                    inst.pathIndex = (inst.pathIndex + 1) % inst.splinePath.Length;
                    if (inst.pathIndex == 0)
                    {
                        // Finished path — regenerate
                        inst.splinePath = GenerateSplinePath(FindDef(inst.type), playerAltitude);
                    }
                }
            }
        }

        // ------------------------------------------------------------------
        // Path generation
        // ------------------------------------------------------------------

        private Vector3[] GenerateSplinePath(CreatureDefinition def, float playerAltitude)
        {
            if (_player == null) return new Vector3[] { Vector3.zero };

            Vector3 center = _player.transform.position;
            center.y = Mathf.Clamp(playerAltitude + Random.Range(-50f, 50f),
                def.minAltitude, def.maxAltitude);

            int   pointCount = 6;
            var   path       = new Vector3[pointCount];
            float angleStep  = 360f / pointCount;

            for (int i = 0; i < pointCount; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad + Random.Range(-0.3f, 0.3f);
                float r     = def.pathRadius * Random.Range(0.8f, 1.2f);
                path[i] = center + new Vector3(
                    Mathf.Cos(angle) * r,
                    Random.Range(-10f, 10f),
                    Mathf.Sin(angle) * r);
            }

            return path;
        }

        // ------------------------------------------------------------------
        // Pool helpers
        // ------------------------------------------------------------------

        private GameObject GetFromPool(CreatureDefinition def)
        {
            foreach (GameObject go in def.pool)
            {
                if (go != null && !go.activeInHierarchy)
                    return go;
            }
            return null;
        }

        private void ReturnToPool(CreatureInstance inst, CreatureDefinition def)
        {
            inst.active = false;
            if (inst.go != null)
                inst.go.SetActive(false);
            _instances.Remove(inst);
        }

        private int CountActiveOfType(AmbientCreatureType type)
        {
            int count = 0;
            foreach (CreatureInstance inst in _instances)
            {
                if (inst.active && inst.type == type) count++;
            }
            return count;
        }

        private CreatureDefinition FindDef(AmbientCreatureType type)
        {
            foreach (CreatureDefinition def in creatureDefinitions)
            {
                if (def.type == type) return def;
            }
            return null;
        }
    }
}
