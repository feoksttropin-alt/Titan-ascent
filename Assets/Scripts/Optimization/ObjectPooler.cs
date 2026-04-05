using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TitanAscent.Optimization
{
    /// <summary>
    /// Interface implemented by pooled GameObjects that want spawn/despawn callbacks.
    /// </summary>
    public interface IPoolable
    {
        void OnSpawn();
        void OnDespawn();
    }

    /// <summary>
    /// Generic singleton object pooler.  Keyed by prefab name.
    /// </summary>
    public class ObjectPooler : MonoBehaviour
    {
        // ── Singleton ─────────────────────────────────────────────────────────────
        public static ObjectPooler Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // ── Pool storage ──────────────────────────────────────────────────────────
        private readonly Dictionary<string, Queue<GameObject>> pools =
            new Dictionary<string, Queue<GameObject>>();

        // Maps instance → prefab name so Return() knows which pool to use
        private readonly Dictionary<int, string> instancePoolKey =
            new Dictionary<int, string>();

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Pre-instantiates <paramref name="count"/> copies of <paramref name="prefab"/>,
        /// deactivates them, and places them in the pool.
        /// </summary>
        public void Prewarm(GameObject prefab, int count)
        {
            if (prefab == null) return;
            string key = prefab.name;
            EnsurePool(key);

            for (int i = 0; i < count; i++)
            {
                GameObject obj = CreateInstance(prefab, key);
                obj.SetActive(false);
                pools[key].Enqueue(obj);
            }
        }

        /// <summary>
        /// Returns a pooled instance of <paramref name="prefab"/> (activates it)
        /// or instantiates a new one if the pool is empty.
        /// </summary>
        public GameObject Get(GameObject prefab)
        {
            if (prefab == null) return null;
            string key = prefab.name;
            EnsurePool(key);

            GameObject obj;
            if (pools[key].Count > 0)
            {
                obj = pools[key].Dequeue();
                if (obj == null)
                {
                    // Pooled object was destroyed externally — make a fresh one
                    obj = CreateInstance(prefab, key);
                }
            }
            else
            {
                obj = CreateInstance(prefab, key);
            }

            obj.SetActive(true);
            NotifySpawn(obj);
            return obj;
        }

        /// <summary>
        /// Returns a pooled instance positioned and rotated as specified.
        /// </summary>
        public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            GameObject obj = Get(prefab);
            if (obj != null)
            {
                obj.transform.position = position;
                obj.transform.rotation = rotation;
            }
            return obj;
        }

        /// <summary>
        /// Deactivates <paramref name="obj"/> and returns it to its pool.
        /// </summary>
        public void Return(GameObject obj)
        {
            if (obj == null) return;

            NotifyDespawn(obj);
            obj.SetActive(false);

            if (instancePoolKey.TryGetValue(obj.GetInstanceID(), out string key))
            {
                EnsurePool(key);
                pools[key].Enqueue(obj);
            }
            else
            {
                // Unknown origin — just deactivate and parent to pooler
                obj.transform.SetParent(transform);
            }
        }

        /// <summary>
        /// Returns <paramref name="obj"/> to the pool after <paramref name="delay"/> seconds.
        /// </summary>
        public void ReturnAfter(GameObject obj, float delay)
        {
            if (obj == null) return;
            StartCoroutine(ReturnAfterCoroutine(obj, delay));
        }

        // ── Private helpers ───────────────────────────────────────────────────────

        private void EnsurePool(string key)
        {
            if (!pools.ContainsKey(key))
                pools[key] = new Queue<GameObject>();
        }

        private GameObject CreateInstance(GameObject prefab, string key)
        {
            GameObject obj = Instantiate(prefab, transform);
            obj.name = prefab.name; // Strip "(Clone)" suffix so key lookup stays clean
            instancePoolKey[obj.GetInstanceID()] = key;
            return obj;
        }

        private static void NotifySpawn(GameObject obj)
        {
            IPoolable[] poolables = obj.GetComponentsInChildren<IPoolable>(includeInactive: true);
            foreach (IPoolable p in poolables)
                p.OnSpawn();
        }

        private static void NotifyDespawn(GameObject obj)
        {
            IPoolable[] poolables = obj.GetComponentsInChildren<IPoolable>(includeInactive: true);
            foreach (IPoolable p in poolables)
                p.OnDespawn();
        }

        private IEnumerator ReturnAfterCoroutine(GameObject obj, float delay)
        {
            yield return new WaitForSeconds(delay);
            Return(obj);
        }
    }
}
