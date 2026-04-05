using System;
using UnityEngine;

namespace TitanAscent.Systems
{
    // -----------------------------------------------------------------------
    // End reason enum
    // -----------------------------------------------------------------------

    public enum EndReason
    {
        Victory,
        LargeFall,
        Quit,
        Restart
    }

    // -----------------------------------------------------------------------
    // SessionEndedEvent (published on EventBus)
    // -----------------------------------------------------------------------

    public struct SessionEndedEvent
    {
        public EndReason reason;
        public float     duration;
        public int       falls;
        public float     maxHeight;
    }

    // -----------------------------------------------------------------------
    // SessionManager
    // -----------------------------------------------------------------------

    /// <summary>
    /// Manages the lifecycle of a single run (session). Replaces scattered
    /// StartClimb logic that was spread across GameManager and TitleScreen.
    /// Singleton — DontDestroyOnLoad.
    /// </summary>
    public class SessionManager : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        // Singleton
        // -----------------------------------------------------------------------

        public static SessionManager Instance { get; private set; }

        // -----------------------------------------------------------------------
        // Inspector
        // -----------------------------------------------------------------------

        [Header("System References")]
        [SerializeField] private SessionStatsTracker sessionStats;
        [SerializeField] private PlaytestLogger       playtestLogger;
        [SerializeField] private GhostSystem          ghostSystem;
        [SerializeField] private SpeedrunManager      speedrunManager;
        [SerializeField] private AchievementValidator achievementValidator;
        [SerializeField] private SaveManager          saveManager;
        [SerializeField] private FallTracker          fallTracker;

        [Header("Speedrun")]
        [SerializeField] private bool autoStartSpeedrun = false;

        // -----------------------------------------------------------------------
        // Private state
        // -----------------------------------------------------------------------

        private bool  _sessionActive;
        private float _sessionStartTime;

        public bool  IsSessionActive => _sessionActive;
        public float SessionDuration => _sessionActive ? Time.time - _sessionStartTime : 0f;

        // -----------------------------------------------------------------------
        // Lifecycle
        // -----------------------------------------------------------------------

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

        private void Start()
        {
            ResolveReferences();

            if (fallTracker != null)
            {
                fallTracker.OnFallCompleted.AddListener(OnFallCompleted);
            }
        }

        private void OnDestroy()
        {
            if (fallTracker != null)
                fallTracker.OnFallCompleted.RemoveListener(OnFallCompleted);
        }

        // -----------------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------------

        /// <summary>Starts a new run session.</summary>
        public void StartSession()
        {
            ResolveReferences();

            _sessionActive   = true;
            _sessionStartTime = Time.time;

            // Reset per-session trackers
            sessionStats?.StartSession();
            achievementValidator?.OnSessionStarted();

            // Start logging
            playtestLogger?.StartSession();

            // Start ghost recording
            ghostSystem?.StartRecording();

            // Start speedrun timer if appropriate
            if (autoStartSpeedrun && speedrunManager != null)
                speedrunManager.StartSpeedrun();

            // Publish event
            EventBus.Publish(new ClimbStartedEvent { startTime = _sessionStartTime });

            Debug.Log("[SessionManager] Session started.");
        }

        /// <summary>Ends the current session with the supplied reason.</summary>
        public void EndSession(EndReason reason)
        {
            if (!_sessionActive) return;
            _sessionActive = false;

            float duration  = Time.time - _sessionStartTime;
            float maxHeight = fallTracker != null ? fallTracker.BestHeightEver : 0f;
            int   falls     = fallTracker != null ? fallTracker.TotalFalls : 0;

            // Notify achievement validator of special conditions
            if (reason == EndReason.Victory)
                achievementValidator?.NotifyVictory();

            // Stop ghost recording
            ghostSystem?.StopRecording();

            // End speedrun
            speedrunManager?.EndSpeedrun(reason == EndReason.Victory);

            // Run achievement checks
            achievementValidator?.ValidateAll();

            // Persist run record
            SaveRunRecord(reason, duration, maxHeight, falls);

            // End playtest session (writes JSON log)
            playtestLogger?.EndSession();

            // Publish event
            EventBus.Publish(new SessionEndedEvent
            {
                reason    = reason,
                duration  = duration,
                falls     = falls,
                maxHeight = maxHeight
            });

            Debug.Log($"[SessionManager] Session ended. Reason={reason}, Duration={duration:F1}s, MaxHeight={maxHeight:F1}m, Falls={falls}");
        }

        // -----------------------------------------------------------------------
        // Private helpers
        // -----------------------------------------------------------------------

        private void ResolveReferences()
        {
            if (sessionStats         == null) sessionStats         = FindFirstObjectByType<SessionStatsTracker>();
            if (playtestLogger       == null) playtestLogger       = PlaytestLogger.Instance ?? FindFirstObjectByType<PlaytestLogger>();
            if (ghostSystem          == null) ghostSystem          = FindFirstObjectByType<GhostSystem>();
            if (speedrunManager      == null) speedrunManager      = SpeedrunManager.Instance ?? FindFirstObjectByType<SpeedrunManager>();
            if (achievementValidator == null) achievementValidator = FindFirstObjectByType<AchievementValidator>();
            if (saveManager          == null) saveManager          = FindFirstObjectByType<SaveManager>();
            if (fallTracker          == null) fallTracker          = FindFirstObjectByType<FallTracker>();
        }

        private void SaveRunRecord(EndReason reason, float duration, float maxHeight, int falls)
        {
            if (saveManager == null) return;

            UI.RunRecord record = new UI.RunRecord
            {
                runDate         = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                maxHeight       = maxHeight,
                totalFalls      = falls,
                longestFall     = saveManager.CurrentData.longestFall,
                durationSeconds = duration,
                modeType        = autoStartSpeedrun ? "Speedrun" : "Normal",
                reached         = reason == EndReason.Victory
            };

            if (saveManager.CurrentData.runHistory == null)
                saveManager.CurrentData.runHistory = new System.Collections.Generic.List<UI.RunRecord>();

            saveManager.CurrentData.runHistory.Insert(0, record);

            // Keep the last 20 records
            if (saveManager.CurrentData.runHistory.Count > 20)
                saveManager.CurrentData.runHistory.RemoveRange(20, saveManager.CurrentData.runHistory.Count - 20);

            saveManager.Save();
        }

        private void OnFallCompleted(FallData data)
        {
            achievementValidator?.NotifyFall(data);
        }
    }
}
