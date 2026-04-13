using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using TitanAscent.Environment;

namespace TitanAscent.Systems
{
    // -----------------------------------------------------------------------
    // Enums & Data Structures
    // -----------------------------------------------------------------------

    public enum LogEventType
    {
        ClimbStart,
        FallSmall,
        FallMedium,
        FallLarge,
        FallCatastrophic,
        RecoverySuccess,
        RecoveryFailed,
        GrappleMiss,
        GrappleAttach,
        ThrusterDepleted,
        ZoneEntered,
        NewHeightRecord,
        Victory,
        PlayerStuck,
        SessionEnd
    }

    [Serializable]
    public class LogEntry
    {
        public string eventType;
        public float height;
        public float sessionTimeSeconds;
        public int zoneIndex;
        public string additionalData;
    }

    [Serializable]
    public class PlaytestSession
    {
        public string sessionId;
        public string startTime;
        public string buildVersion;
        public List<LogEntry> events = new List<LogEntry>();
    }

    // -----------------------------------------------------------------------
    // PlaytestLogger
    // -----------------------------------------------------------------------

    /// <summary>
    /// Records gameplay events for post-session analysis.
    /// Writes to a JSON file in Application.persistentDataPath at session end.
    /// Auto-hooks into FallTracker.OnFallCompleted and ZoneManager.OnZoneChanged.
    /// </summary>
    public class PlaytestLogger : MonoBehaviour
    {
        // Singleton
        public static PlaytestLogger Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private bool autoStartSession = true;
        [SerializeField] private float stuckHeightThreshold = 2f;    // metres of movement required in 60 s
        [SerializeField] private float stuckCheckInterval = 60f;      // seconds between stuck checks

        // -----------------------------------------------------------------------
        // Private State
        // -----------------------------------------------------------------------

        private PlaytestSession _session;
        private bool _sessionActive;
        private float _sessionStartTime;

        private float _lastHeight;
        private float _lastStuckCheckTime;
        private int _currentZoneIndex;

        private FallTracker _fallTracker;
        private ZoneManager _zoneManager;

        // -----------------------------------------------------------------------
        // Lifecycle
        // -----------------------------------------------------------------------

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            // Auto-hook into existing systems
            _fallTracker = FindFirstObjectByType<FallTracker>();
            if (_fallTracker != null)
                _fallTracker.OnFallCompleted.AddListener(OnFallCompleted);

            _zoneManager = FindFirstObjectByType<ZoneManager>();
            if (_zoneManager != null)
                _zoneManager.OnZoneChanged.AddListener(OnZoneChanged);

            if (autoStartSession)
                StartSession();

            StartCoroutine(StuckDetectionRoutine());
        }

        private void OnDestroy()
        {
            if (_fallTracker != null)
                _fallTracker.OnFallCompleted.RemoveListener(OnFallCompleted);

            if (_zoneManager != null)
                _zoneManager.OnZoneChanged.RemoveListener(OnZoneChanged);
        }

        // -----------------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------------

        /// <summary>Generates a new session and begins logging.</summary>
        public void StartSession()
        {
            _session = new PlaytestSession
            {
                sessionId    = Guid.NewGuid().ToString(),
                startTime    = DateTime.UtcNow.ToString("o"),
                buildVersion = Application.version,
                events       = new List<LogEntry>()
            };

            _sessionActive    = true;
            _sessionStartTime = Time.time;
            _lastHeight       = 0f;
            _lastStuckCheckTime = Time.time;
            _currentZoneIndex = 0;

            LogEvent(LogEventType.ClimbStart, 0f, 0);
            Debug.Log($"[PlaytestLogger] Session started: {_session.sessionId}");
        }

        /// <summary>Appends a log entry to the current session.</summary>
        public void LogEvent(LogEventType eventType, float height, int zone, string extra = "")
        {
            if (!_sessionActive || _session == null) return;

            var entry = new LogEntry
            {
                eventType          = eventType.ToString(),
                height             = height,
                sessionTimeSeconds = Time.time - _sessionStartTime,
                zoneIndex          = zone,
                additionalData     = extra
            };

            _session.events.Add(entry);
        }

        /// <summary>Finalises the session and writes the JSON log file.</summary>
        public void EndSession()
        {
            if (!_sessionActive || _session == null) return;

            LogEvent(LogEventType.SessionEnd, _lastHeight, _currentZoneIndex,
                     $"totalEvents:{_session.events.Count}");

            _sessionActive = false;

            string fileName = $"playtest_{_session.sessionId}.json";
            string filePath = Path.Combine(Application.persistentDataPath, fileName);

            try
            {
                string json = JsonUtility.ToJson(_session, true);
                File.WriteAllText(filePath, json, Encoding.UTF8);
                Debug.Log($"[PlaytestLogger] Session saved: {filePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[PlaytestLogger] Failed to write session file: {e.Message}");
            }
        }

        /// <summary>
        /// Returns a human-readable summary of the current session including
        /// total falls, average fall height, most-failed zone, time-per-zone, and total climb time.
        /// </summary>
        public string GetSessionSummary()
        {
            if (_session == null || _session.events.Count == 0)
                return "No session data available.";

            int totalFalls        = 0;
            float totalFallHeight = 0f;
            Dictionary<int, int>   fallsPerZone     = new Dictionary<int, int>();
            Dictionary<int, float> firstTimeInZone  = new Dictionary<int, float>();
            Dictionary<int, float> lastTimeInZone   = new Dictionary<int, float>();

            foreach (var entry in _session.events)
            {
                // Detect fall events
                bool isFall = entry.eventType == LogEventType.FallSmall.ToString()
                           || entry.eventType == LogEventType.FallMedium.ToString()
                           || entry.eventType == LogEventType.FallLarge.ToString()
                           || entry.eventType == LogEventType.FallCatastrophic.ToString();

                if (isFall)
                {
                    totalFalls++;
                    totalFallHeight += entry.height;

                    if (!fallsPerZone.ContainsKey(entry.zoneIndex))
                        fallsPerZone[entry.zoneIndex] = 0;
                    fallsPerZone[entry.zoneIndex]++;
                }

                // Track time per zone (first / last event seen in zone)
                if (!firstTimeInZone.ContainsKey(entry.zoneIndex))
                    firstTimeInZone[entry.zoneIndex] = entry.sessionTimeSeconds;
                lastTimeInZone[entry.zoneIndex] = entry.sessionTimeSeconds;
            }

            float avgFallHeight = totalFalls > 0 ? totalFallHeight / totalFalls : 0f;

            // Most-failed zone
            int mostFailedZone      = -1;
            int mostFailedZoneFalls = 0;
            foreach (var kvp in fallsPerZone)
            {
                if (kvp.Value > mostFailedZoneFalls)
                {
                    mostFailedZoneFalls = kvp.Value;
                    mostFailedZone      = kvp.Key;
                }
            }

            // Build time-per-zone string
            var sb = new StringBuilder();
            sb.AppendLine("=== Playtest Session Summary ===");
            sb.AppendLine($"Session ID    : {_session.sessionId}");
            sb.AppendLine($"Build Version : {_session.buildVersion}");
            sb.AppendLine($"Total Climb Time : {FormatTime(Time.time - _sessionStartTime)}");
            sb.AppendLine($"Total Falls   : {totalFalls}");
            sb.AppendLine($"Avg Fall Height: {avgFallHeight:F1} m");
            sb.AppendLine($"Most Falls In Zone: {(mostFailedZone >= 0 ? $"Zone {mostFailedZone} ({mostFailedZoneFalls} falls)" : "N/A")}");
            sb.AppendLine("Time Per Zone:");
            foreach (var kvp in firstTimeInZone)
            {
                float time = lastTimeInZone.ContainsKey(kvp.Key)
                    ? lastTimeInZone[kvp.Key] - kvp.Value
                    : 0f;
                sb.AppendLine($"  Zone {kvp.Key}: {FormatTime(time)}");
            }
            sb.AppendLine($"Total Events Logged: {_session.events.Count}");

            return sb.ToString();
        }

        // -----------------------------------------------------------------------
        // Event Listeners
        // -----------------------------------------------------------------------

        private void OnFallCompleted(FallData data)
        {
            LogEventType eventType;
            switch (data.severity)
            {
                case FallSeverity.Small:        eventType = LogEventType.FallSmall;        break;
                case FallSeverity.Medium:       eventType = LogEventType.FallMedium;       break;
                case FallSeverity.Large:        eventType = LogEventType.FallLarge;        break;
                case FallSeverity.Catastrophic: eventType = LogEventType.FallCatastrophic; break;
                case FallSeverity.RunEnding:    eventType = LogEventType.FallCatastrophic; break;
                default:                        return;
            }

            LogEvent(eventType, data.endHeight, _currentZoneIndex,
                     $"startH:{data.startHeight:F1},dist:{data.distance:F1},dur:{data.duration:F2}s");
        }

        private void OnZoneChanged(TitanZone previous, TitanZone newZone)
        {
            _currentZoneIndex = _zoneManager != null ? _zoneManager.CurrentZoneIndex : 0;
            LogEvent(LogEventType.ZoneEntered, _lastHeight, _currentZoneIndex,
                     $"zone:{newZone?.name ?? "unknown"}");
        }

        // -----------------------------------------------------------------------
        // Stuck Detection Coroutine
        // -----------------------------------------------------------------------

        private IEnumerator StuckDetectionRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(stuckCheckInterval);

                if (!_sessionActive) continue;

                float currentHeight = GetCurrentPlayerHeight();
                float heightDelta   = Mathf.Abs(currentHeight - _lastHeight);

                if (heightDelta < stuckHeightThreshold)
                {
                    LogEvent(LogEventType.PlayerStuck, currentHeight, _currentZoneIndex,
                             $"heightDelta:{heightDelta:F2}m in {stuckCheckInterval}s");
                    Debug.LogWarning($"[PlaytestLogger] Player stuck detected at height {currentHeight:F1}m.");
                }

                _lastHeight = currentHeight;
            }
        }

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        private float GetCurrentPlayerHeight()
        {
            if (_fallTracker != null) return _fallTracker.transform.position.y;
            var player = FindFirstObjectByType<Player.PlayerController>();
            return player != null ? player.transform.position.y : 0f;
        }

        private static string FormatTime(float seconds)
        {
            int m = Mathf.FloorToInt(seconds / 60f);
            int s = Mathf.FloorToInt(seconds % 60f);
            return $"{m:D2}:{s:D2}";
        }

        private void OnApplicationQuit()
        {
            if (_sessionActive) EndSession();
        }
    }
}
