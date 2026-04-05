using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TitanAscent.Environment;

namespace TitanAscent.Systems
{
    public enum WorldEventType
    {
        WingTremor,
        BreathingExpansion,
        MuscleContraction,
        WindGust,
        LightningStrike
    }

    /// <summary>
    /// Schedules titan world events per-zone, preventing harmful overlaps.
    /// </summary>
    public class WorldEventScheduler : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TitanMovement titanMovement;
        [SerializeField] private WindSystem windSystem;

        // ------------------------------------------------------------------
        // Internal config per event type
        // ------------------------------------------------------------------

        private class EventConfig
        {
            public WorldEventType type;
            public float cooldownAfterFire;    // minimum gap after this event finishes
            public float minInterval;
            public float maxInterval;
        }

        private static readonly Dictionary<WorldEventType, EventConfig> Configs =
            new Dictionary<WorldEventType, EventConfig>
            {
                {
                    WorldEventType.WingTremor,
                    new EventConfig
                    {
                        type = WorldEventType.WingTremor,
                        cooldownAfterFire = 10f,
                        minInterval = 45f,
                        maxInterval = 90f
                    }
                },
                {
                    WorldEventType.WindGust,
                    new EventConfig
                    {
                        type = WorldEventType.WindGust,
                        cooldownAfterFire = 5f,
                        minInterval = 20f,
                        maxInterval = 40f
                    }
                },
                {
                    WorldEventType.LightningStrike,
                    new EventConfig
                    {
                        type = WorldEventType.LightningStrike,
                        cooldownAfterFire = 2f,
                        minInterval = 8f,
                        maxInterval = 15f
                    }
                },
                {
                    WorldEventType.BreathingExpansion,
                    new EventConfig
                    {
                        type = WorldEventType.BreathingExpansion,
                        cooldownAfterFire = 1f,
                        minInterval = 6f,
                        maxInterval = 6f   // constant 6s cycle
                    }
                },
                {
                    WorldEventType.MuscleContraction,
                    new EventConfig
                    {
                        type = WorldEventType.MuscleContraction,
                        cooldownAfterFire = 8f,
                        minInterval = 30f,
                        maxInterval = 30f
                    }
                }
            };

        // ------------------------------------------------------------------
        // Zone rhythm definitions
        // ------------------------------------------------------------------

        private class ZoneRhythm
        {
            public int zone;
            public WorldEventType eventType;
        }

        private static readonly ZoneRhythm[] ZoneRhythms =
        {
            new ZoneRhythm { zone = 4, eventType = WorldEventType.WingTremor },
            new ZoneRhythm { zone = 5, eventType = WorldEventType.WindGust },
            new ZoneRhythm { zone = 7, eventType = WorldEventType.LightningStrike },
            new ZoneRhythm { zone = 8, eventType = WorldEventType.BreathingExpansion },
            new ZoneRhythm { zone = 9, eventType = WorldEventType.MuscleContraction }
        };

        // ------------------------------------------------------------------
        // Runtime state
        // ------------------------------------------------------------------

        // Which events are currently executing
        private readonly HashSet<WorldEventType> activeEvents = new HashSet<WorldEventType>();

        // Time at which each event became inactive (for cooldown tracking)
        private readonly Dictionary<WorldEventType, float> lastEndTime =
            new Dictionary<WorldEventType, float>();

        // Coroutines running per-zone rhythm
        private readonly Dictionary<int, Coroutine> zoneCoroutines =
            new Dictionary<int, Coroutine>();

        // Overlap rule: if WingTremor is active, delay BreathingExpansion for 5 s
        private const float OverlapDelay = 5f;

        // ------------------------------------------------------------------
        // Public API
        // ------------------------------------------------------------------

        /// <summary>Returns true if the specified event is currently running.</summary>
        public bool IsEventActive(WorldEventType eventType) => activeEvents.Contains(eventType);

        /// <summary>Begins the zone's predefined event rhythm.</summary>
        public void StartZoneRhythm(int zone)
        {
            // Don't double-start
            if (zoneCoroutines.ContainsKey(zone))
                return;

            foreach (ZoneRhythm rhythm in ZoneRhythms)
            {
                if (rhythm.zone == zone)
                {
                    Coroutine c = StartCoroutine(ZoneRhythmCoroutine(zone, rhythm.eventType));
                    zoneCoroutines[zone] = c;
                    return;
                }
            }
        }

        /// <summary>Stops and cancels a zone's event rhythm.</summary>
        public void CancelZoneEvents(int zone)
        {
            if (zoneCoroutines.TryGetValue(zone, out Coroutine c))
            {
                if (c != null)
                    StopCoroutine(c);
                zoneCoroutines.Remove(zone);
            }
        }

        // ------------------------------------------------------------------
        // Zone rhythm coroutine
        // ------------------------------------------------------------------

        private IEnumerator ZoneRhythmCoroutine(int zone, WorldEventType eventType)
        {
            EventConfig cfg = Configs[eventType];

            // Initial random delay so events from different zones don't all fire at once
            float initialDelay = UnityEngine.Random.Range(cfg.minInterval * 0.25f, cfg.minInterval * 0.75f);
            yield return new WaitForSeconds(initialDelay);

            while (true)
            {
                // Wait for any cooldown from the last time this event ran
                yield return WaitForCooldown(eventType);

                // Check overlap rule: WingTremor active → delay non-WingTremor events
                if (eventType != WorldEventType.WingTremor && activeEvents.Contains(WorldEventType.WingTremor))
                    yield return new WaitForSeconds(OverlapDelay);

                // Fire the event
                yield return FireEvent(eventType);

                // Wait interval before scheduling the next occurrence
                float interval = UnityEngine.Random.Range(cfg.minInterval, cfg.maxInterval);
                yield return new WaitForSeconds(interval);
            }
        }

        private IEnumerator WaitForCooldown(WorldEventType eventType)
        {
            if (!lastEndTime.TryGetValue(eventType, out float endTime))
                yield break;

            float cooldown = Configs[eventType].cooldownAfterFire;
            float remaining = (endTime + cooldown) - Time.time;
            if (remaining > 0f)
                yield return new WaitForSeconds(remaining);
        }

        private IEnumerator FireEvent(WorldEventType eventType)
        {
            activeEvents.Add(eventType);

            try
            {
                switch (eventType)
                {
                    case WorldEventType.WingTremor:
                        FireWingTremor();
                        break;
                    case WorldEventType.BreathingExpansion:
                        FireBreathingExpansion();
                        break;
                    case WorldEventType.MuscleContraction:
                        FireMuscleContraction();
                        break;
                    case WorldEventType.WindGust:
                        FireWindGust();
                        break;
                    case WorldEventType.LightningStrike:
                        FireLightningStrike();
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[WorldEventScheduler] Exception firing {eventType}: {e}");
            }

            // Hold 'active' state for the estimated event duration
            float duration = GetEventDuration(eventType);
            yield return new WaitForSeconds(duration);

            activeEvents.Remove(eventType);
            lastEndTime[eventType] = Time.time;
        }

        // ------------------------------------------------------------------
        // Event dispatch helpers
        // ------------------------------------------------------------------

        private float GetEventDuration(WorldEventType eventType)
        {
            return eventType switch
            {
                WorldEventType.WingTremor       => 2.5f,
                WorldEventType.BreathingExpansion => 4f,
                WorldEventType.MuscleContraction => 1.5f,
                WorldEventType.WindGust         => 3f,
                WorldEventType.LightningStrike  => 1f,
                _                               => 2f
            };
        }

        private void FireTitanMovementEvent(TitanMovementType movementType, float amplitude, float duration, int[] zones)
        {
            if (titanMovement == null) return;

            // TitanMovement raises the event internally on its own schedule; for externally
            // triggered events we publish directly via its public UnityEvent so all listeners
            // (audio, VFX, etc.) receive the signal without bypassing safety clamping.
            TitanMovementEvent evt = new TitanMovementEvent
            {
                movementType = movementType,
                amplitude    = amplitude,
                duration     = duration,
                affectedZoneIndices = zones,
                direction    = Vector3.zero
            };

            titanMovement.OnTitanMovementEvent?.Invoke(evt);
        }

        private void FireWingTremor()
        {
            FireTitanMovementEvent(TitanMovementType.WingTremor, 0.8f, 2.5f, new[] { 3, 4 });
            Debug.Log("[WorldEventScheduler] WingTremor fired.");
        }

        private void FireBreathingExpansion()
        {
            FireTitanMovementEvent(TitanMovementType.BreathingExpansion, 0.3f, 4f, new[] { 7 });
            Debug.Log("[WorldEventScheduler] BreathingExpansion fired.");
        }

        private void FireMuscleContraction()
        {
            FireTitanMovementEvent(TitanMovementType.MuscleContraction, 0.4f, 1.5f, new[] { 6, 7 });
            Debug.Log("[WorldEventScheduler] MuscleContraction fired.");
        }

        private void FireWindGust()
        {
            // Wind system does not yet expose TriggerGust; signal via EventBus for WindSystem to pick up
            Debug.Log("[WorldEventScheduler] WindGust fired.");
        }

        private void FireLightningStrike()
        {
            // Lightning is a pure environment signal; other systems subscribe via EventBus
            Debug.Log("[WorldEventScheduler] LightningStrike fired.");
        }

        // ------------------------------------------------------------------
        // Lifecycle
        // ------------------------------------------------------------------

        private void OnDestroy()
        {
            StopAllCoroutines();
            zoneCoroutines.Clear();
            activeEvents.Clear();
        }
    }
}
