using UnityEngine;

namespace TitanAscent.Optimization
{
    /// <summary>
    /// Specialised pool for anchor-point highlight and attach visual effects.
    /// Pre-warms 20 highlight instances and 10 attach-burst instances on Start.
    /// </summary>
    public class AnchorEffectPool : MonoBehaviour
    {
        [Header("Effect Prefabs")]
        [SerializeField] private GameObject highlightEffectPrefab;
        [SerializeField] private GameObject attachEffectPrefab;

        [Header("Prewarm Counts")]
        [SerializeField] private int highlightPrewarmCount = 20;
        [SerializeField] private int attachPrewarmCount    = 10;

        [Header("Auto-return Delay")]
        [SerializeField] private float attachEffectLifetime = 1f;

        private void Start()
        {
            ObjectPooler pooler = ObjectPooler.Instance;
            if (pooler == null)
            {
                Debug.LogWarning("[AnchorEffectPool] ObjectPooler singleton not found. " +
                                 "Ensure an ObjectPooler exists in the scene before AnchorEffectPool.");
                return;
            }

            if (highlightEffectPrefab != null)
                pooler.Prewarm(highlightEffectPrefab, highlightPrewarmCount);

            if (attachEffectPrefab != null)
                pooler.Prewarm(attachEffectPrefab, attachPrewarmCount);
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Retrieves a highlight effect from the pool, places it at
        /// <paramref name="position"/>, and returns it as a handle.
        /// Pass the handle to <see cref="HideHighlight"/> when done.
        /// Returns null if the prefab is not assigned or the pooler is missing.
        /// </summary>
        public GameObject ShowHighlight(Vector3 position)
        {
            if (highlightEffectPrefab == null) return null;

            ObjectPooler pooler = ObjectPooler.Instance;
            if (pooler == null) return null;

            GameObject handle = pooler.Get(
                highlightEffectPrefab,
                position,
                Quaternion.identity);

            return handle;
        }

        /// <summary>
        /// Retrieves an attach-burst effect from the pool, places it at
        /// <paramref name="position"/>, and auto-returns it after
        /// <see cref="attachEffectLifetime"/> seconds.
        /// </summary>
        public void ShowAttach(Vector3 position)
        {
            if (attachEffectPrefab == null) return;

            ObjectPooler pooler = ObjectPooler.Instance;
            if (pooler == null) return;

            GameObject fx = pooler.Get(
                attachEffectPrefab,
                position,
                Quaternion.identity);

            if (fx != null)
                pooler.ReturnAfter(fx, attachEffectLifetime);
        }

        /// <summary>
        /// Returns a highlight effect obtained via <see cref="ShowHighlight"/> back
        /// to the pool.
        /// </summary>
        public void HideHighlight(GameObject handle)
        {
            if (handle == null) return;

            ObjectPooler pooler = ObjectPooler.Instance;
            if (pooler != null)
                pooler.Return(handle);
            else
                handle.SetActive(false);
        }
    }
}
