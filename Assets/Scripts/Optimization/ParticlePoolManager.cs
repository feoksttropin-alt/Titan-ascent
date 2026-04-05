using System.Collections.Generic;
using UnityEngine;

namespace TitanAscent.Optimization
{
    /// <summary>
    /// Specialized pooler for particle effects.
    /// Pre-warms common particle prefabs on scene load.
    ///
    /// Usage
    /// ─────
    /// Assign prefabs in the Inspector for each pool slot.
    /// Call ParticlePoolManager.Play(prefabId, position, rotation) from anywhere.
    /// Each ParticleSystem's stopAction should be set to Callback, which calls
    /// ReturnToPool — this is configured automatically by the pool.
    /// </summary>
    public class ParticlePoolManager : MonoBehaviour
    {
        // ── Singleton ──────────────────────────────────────────────────────────

        public static ParticlePoolManager Instance { get; private set; }

        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("Prefabs")]
        [SerializeField] private ParticleSystem impactSparkPrefab;
        [SerializeField] private ParticleSystem dustPuffPrefab;
        [SerializeField] private ParticleSystem thrusterBurstPrefab;
        [SerializeField] private ParticleSystem landingDustPrefab;
        [SerializeField] private ParticleSystem crystalFlashPrefab;
        [SerializeField] private ParticleSystem windDebrisPrefab;
        [SerializeField] private ParticleSystem goldenMotesPrefab;

        [Header("Pool Sizes")]
        [SerializeField] private int impactSparkCount  = 20;
        [SerializeField] private int dustPuffCount     = 15;
        [SerializeField] private int thrusterBurstCount= 10;
        [SerializeField] private int landingDustCount  = 8;
        [SerializeField] private int crystalFlashCount = 8;
        [SerializeField] private int windDebrisCount   = 25;
        [SerializeField] private int goldenMotesCount  = 5;

        // ── Predefined IDs ─────────────────────────────────────────────────────

        public const string ID_ImpactSpark   = "ImpactSpark";
        public const string ID_DustPuff      = "DustPuff";
        public const string ID_ThrusterBurst = "ThrusterBurst";
        public const string ID_LandingDust   = "LandingDust";
        public const string ID_CrystalFlash  = "CrystalFlash";
        public const string ID_WindDebris    = "WindDebris";
        public const string ID_GoldenMotes   = "GoldenMotes";

        // ── Internal storage ───────────────────────────────────────────────────

        private readonly Dictionary<string, Queue<ParticleSystem>> _pools =
            new Dictionary<string, Queue<ParticleSystem>>();

        private readonly Dictionary<string, ParticleSystem> _prefabMap =
            new Dictionary<string, ParticleSystem>();

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            RegisterPrefabs();
            PrewarmAll();
        }

        // ── Registration & pre-warm ────────────────────────────────────────────

        private void RegisterPrefabs()
        {
            Register(ID_ImpactSpark,   impactSparkPrefab,  impactSparkCount);
            Register(ID_DustPuff,      dustPuffPrefab,     dustPuffCount);
            Register(ID_ThrusterBurst, thrusterBurstPrefab,thrusterBurstCount);
            Register(ID_LandingDust,   landingDustPrefab,  landingDustCount);
            Register(ID_CrystalFlash,  crystalFlashPrefab, crystalFlashCount);
            Register(ID_WindDebris,    windDebrisPrefab,   windDebrisCount);
            Register(ID_GoldenMotes,   goldenMotesPrefab,  goldenMotesCount);
        }

        private void Register(string id, ParticleSystem prefab, int prewarmCount)
        {
            if (prefab == null) return;
            _prefabMap[id] = prefab;
            _pools[id]     = new Queue<ParticleSystem>(prewarmCount);
        }

        private void PrewarmAll()
        {
            foreach (KeyValuePair<string, ParticleSystem> kv in _prefabMap)
            {
                int count = GetPrewarmCount(kv.Key);
                for (int i = 0; i < count; i++)
                {
                    ParticleSystem ps = CreateInstance(kv.Key, kv.Value);
                    ps.gameObject.SetActive(false);
                    _pools[kv.Key].Enqueue(ps);
                }
            }
        }

        private int GetPrewarmCount(string id)
        {
            switch (id)
            {
                case ID_ImpactSpark:   return impactSparkCount;
                case ID_DustPuff:      return dustPuffCount;
                case ID_ThrusterBurst: return thrusterBurstCount;
                case ID_LandingDust:   return landingDustCount;
                case ID_CrystalFlash:  return crystalFlashCount;
                case ID_WindDebris:    return windDebrisCount;
                case ID_GoldenMotes:   return goldenMotesCount;
                default:               return 4;
            }
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Static convenience method. Gets a pooled ParticleSystem, places it at
        /// <paramref name="position"/> with <paramref name="rotation"/>, then plays it.
        /// </summary>
        public static void Play(string prefabId, Vector3 position, Quaternion rotation)
        {
            if (Instance == null)
            {
                Debug.LogWarning("[ParticlePoolManager] No instance in scene.");
                return;
            }
            Instance.PlayInternal(prefabId, position, rotation);
        }

        /// <summary>
        /// Gets a pooled ParticleSystem for <paramref name="prefabId"/>.
        /// Caller is responsible for playing and eventually returning it.
        /// </summary>
        public ParticleSystem Get(string prefabId)
        {
            if (!_pools.TryGetValue(prefabId, out Queue<ParticleSystem> pool))
                return null;

            ParticleSystem ps = null;

            // Drain any nulls
            while (pool.Count > 0 && ps == null)
                ps = pool.Dequeue();

            if (ps == null)
            {
                if (!_prefabMap.TryGetValue(prefabId, out ParticleSystem prefab) || prefab == null)
                    return null;
                ps = CreateInstance(prefabId, prefab);
            }

            ps.gameObject.SetActive(true);
            return ps;
        }

        /// <summary>
        /// Returns a ParticleSystem to its pool. Called automatically via stopAction callback.
        /// </summary>
        public void Return(ParticleSystem ps)
        {
            if (ps == null) return;
            ps.gameObject.SetActive(false);

            string id = ps.gameObject.name;
            if (_pools.TryGetValue(id, out Queue<ParticleSystem> pool))
                pool.Enqueue(ps);
            else
            {
                // Unknown origin — just deactivate under the pooler
                ps.transform.SetParent(transform, false);
            }
        }

        // ── Internal helpers ───────────────────────────────────────────────────

        private void PlayInternal(string prefabId, Vector3 position, Quaternion rotation)
        {
            ParticleSystem ps = Get(prefabId);
            if (ps == null) return;

            ps.transform.position = position;
            ps.transform.rotation = rotation;
            ps.Play();
        }

        private ParticleSystem CreateInstance(string id, ParticleSystem prefab)
        {
            ParticleSystem ps = Instantiate(prefab, transform);
            ps.gameObject.name = id; // Ensures pool lookup works in Return()

            // Configure stopAction so the PS auto-returns when it finishes
            var main = ps.main;
            main.stopAction = ParticleSystemStopAction.Callback;

            // Add callback component
            PooledParticleCallback cb = ps.gameObject.GetComponent<PooledParticleCallback>();
            if (cb == null)
                cb = ps.gameObject.AddComponent<PooledParticleCallback>();
            cb.Initialize(this);

            ps.gameObject.SetActive(false);
            return ps;
        }
    }

    // ── Callback helper ───────────────────────────────────────────────────────

    /// <summary>
    /// Attached to each pooled ParticleSystem GO.
    /// OnParticleSystemStopped returns the PS to the pool.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class PooledParticleCallback : MonoBehaviour
    {
        private ParticlePoolManager _pool;
        private ParticleSystem      _ps;

        public void Initialize(ParticlePoolManager pool)
        {
            _pool = pool;
            _ps   = GetComponent<ParticleSystem>();
        }

        private void OnParticleSystemStopped()
        {
            _pool?.Return(_ps);
        }
    }
}
