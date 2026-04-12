using System.Collections.Generic;
using UnityEngine;

namespace TitanAscent.Systems
{
    public enum TimeScalePriority
    {
        Normal          = 0,
        GrappleImpact   = 1,
        CatastrophicFall= 2,
        Victory         = 3,
        PostRun         = 5,
        PauseMenu       = 10
    }

    /// <summary>
    /// Priority-stack time scale controller. All scripts should call this
    /// instead of assigning Time.timeScale directly. Singleton MonoBehaviour.
    /// </summary>
    public class TimeScaleManager : MonoBehaviour
    {
        public static TimeScaleManager Instance { get; private set; }

        // (scale, priority, expiryTime)  — expiryTime < 0 means no expiry
        private struct Entry
        {
            public float scale;
            public TimeScalePriority priority;
            public float expiryTime;   // unscaled time
        }

        private readonly List<Entry> stack = new List<Entry>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Push a permanent Normal baseline so stack is never empty
            stack.Add(new Entry
            {
                scale = 1f,
                priority = TimeScalePriority.Normal,
                expiryTime = -1f
            });

            ApplyTopEntry();
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            bool removed = false;

            for (int i = stack.Count - 1; i >= 0; i--)
            {
                if (stack[i].expiryTime >= 0f && now >= stack[i].expiryTime)
                {
                    stack.RemoveAt(i);
                    removed = true;
                }
            }

            if (removed)
                ApplyTopEntry();
        }

        /// <summary>
        /// Add or replace an entry for the given priority.
        /// Pass duration &lt;= 0 for permanent (until RemoveScale is called).
        /// </summary>
        public void SetTimeScale(float scale, TimeScalePriority priority, float duration = -1f)
        {
            // Remove any existing entry with the same priority
            RemoveEntryByPriority(priority);

            float expiry = (duration > 0f) ? Time.unscaledTime + duration : -1f;

            stack.Add(new Entry
            {
                scale = scale,
                priority = priority,
                expiryTime = expiry
            });

            ApplyTopEntry();
        }

        /// <summary>
        /// Remove the entry for the given priority. The previous highest
        /// remaining priority will become active.
        /// </summary>
        public void RemoveScale(TimeScalePriority priority)
        {
            RemoveEntryByPriority(priority);
            ApplyTopEntry();
        }

        /// <summary>
        /// Returns the scale value currently being applied.
        /// </summary>
        public float CurrentScale => Time.timeScale;

        // ------------------------------------------------------------------
        // Private helpers
        // ------------------------------------------------------------------

        private void RemoveEntryByPriority(TimeScalePriority priority)
        {
            for (int i = stack.Count - 1; i >= 0; i--)
            {
                if (stack[i].priority == priority)
                {
                    stack.RemoveAt(i);
                    // Only remove the first match (most recently added)
                    break;
                }
            }
        }

        private void ApplyTopEntry()
        {
            if (stack.Count == 0)
            {
                // Safety: restore normal time if stack is somehow drained
                Time.timeScale = 1f;
                return;
            }

            // Find the highest-priority entry (highest enum value wins)
            Entry best = stack[0];
            foreach (Entry e in stack)
            {
                if ((int)e.priority > (int)best.priority)
                    best = e;
            }

            Time.timeScale = best.scale;
            Time.fixedDeltaTime = 0.02f * best.scale;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Time.timeScale = 1f;
                Time.fixedDeltaTime = 0.02f;
            }
        }
    }
}
