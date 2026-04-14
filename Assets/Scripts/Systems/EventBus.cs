using System;
using System.Collections.Generic;
using UnityEngine;
using TitanAscent.Environment;

namespace TitanAscent.Systems
{
    // ---------------------------------------------------------------------------
    // Pre-defined event structs
    // ---------------------------------------------------------------------------

    public struct ClimbStartedEvent
    {
        public float startTime;
    }

    public struct FallStartedEvent
    {
        public float height;
    }

    public struct FallEndedEvent
    {
        public FallData data;
    }

    public struct RecoveryEvent
    {
        public float fromHeight;
        public float savedHeight;
    }

    public struct ZoneChangedEvent
    {
        public int fromZone;
        public int toZone;
        public float height;
    }

    public struct GrappleAttachedEvent
    {
        public Vector3 anchor;
        public SurfaceType surface;
    }

    public struct GrappleReleasedEvent
    {
        public Vector3 velocity;
        public float ropeLength;
    }

    public struct NewHeightEvent
    {
        public float height;
        public bool isRecord;
    }

    public struct VictoryEvent
    {
        public float time;
        public int falls;
        public float longestFall;
    }

    public struct TitanShudderEvent
    {
        public float amplitude;
        public float duration;
        public int[] affectedZoneIndices;
    }

    public struct WindGustEvent
    {
        public float intensity;
        public float duration;
    }

    public struct BreathingPulseEvent
    {
        public float amplitude;
        public float duration;
    }

    // ---------------------------------------------------------------------------
    // Event Bus
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Lightweight type-safe event bus. Static, no MonoBehaviour.
    /// Thread-safe: copies handler list before invocation.
    /// Duplicate subscriptions are rejected in O(1) via a parallel HashSet.
    /// </summary>
    public static class EventBus
    {
        // Ordered list for deterministic invocation; HashSet for O(1) duplicate guard.
        private static readonly Dictionary<Type, List<Delegate>>    handlers =
            new Dictionary<Type, List<Delegate>>();
        private static readonly Dictionary<Type, HashSet<Delegate>> handlerSets =
            new Dictionary<Type, HashSet<Delegate>>();

        private static readonly object lockObject = new object();

        /// <summary>
        /// Subscribe a handler for event type T.
        /// Duplicate subscriptions are silently ignored in O(1).
        /// </summary>
        public static void Subscribe<T>(Action<T> handler)
        {
            if (handler == null) return;

            Type key = typeof(T);
            lock (lockObject)
            {
                if (!handlers.TryGetValue(key, out List<Delegate> list))
                {
                    list = new List<Delegate>();
                    handlers[key]    = list;
                    handlerSets[key] = new HashSet<Delegate>();
                }

                // O(1) duplicate check via HashSet
                if (handlerSets[key].Add(handler))
                    list.Add(handler);
            }
        }

        /// <summary>
        /// Unsubscribe a handler for event type T.
        /// </summary>
        public static void Unsubscribe<T>(Action<T> handler)
        {
            if (handler == null) return;

            Type key = typeof(T);
            lock (lockObject)
            {
                if (handlers.TryGetValue(key, out List<Delegate> list)
                    && handlerSets.TryGetValue(key, out HashSet<Delegate> set)
                    && set.Remove(handler))
                {
                    list.Remove(handler);
                }
            }
        }

        /// <summary>
        /// Publish an event to all subscribers of type T.
        /// Handler list is snapshot-copied before invocation for thread safety.
        /// </summary>
        public static void Publish<T>(T evt)
        {
            Type key = typeof(T);
            List<Delegate> snapshot;

            lock (lockObject)
            {
                if (!handlers.TryGetValue(key, out List<Delegate> list) || list.Count == 0)
                    return;

                snapshot = new List<Delegate>(list);
            }

            foreach (Delegate d in snapshot)
            {
                try
                {
                    ((Action<T>)d)(evt);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[EventBus] Exception in handler for {key.Name}: {e}");
                }
            }
        }

        /// <summary>Remove all subscribers for all event types. Call on scene reload.</summary>
        public static void Clear()
        {
            lock (lockObject)
            {
                handlers.Clear();
                handlerSets.Clear();
            }
        }

        /// <summary>Remove all subscribers for a specific event type.</summary>
        public static void Clear<T>()
        {
            Type key = typeof(T);
            lock (lockObject)
            {
                handlers.Remove(key);
                handlerSets.Remove(key);
            }
        }
    }
}
