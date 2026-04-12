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
        private int   _fallCount;
        private float _bestHeightThisSession;

        // -----------------------------------------------------------------------
        // Properties
        // -----------------------------------------------------------------------

        public bool  IsSessionActive         => _sessionActive;
        public float SessionDuration         => _sessionActive ? Time.time - _sessionStartTime : 0f;

        /// <summary>Elapsed time since the session started (alias for SessionDuration).</summary>
        public float SessionTime             => SessionDuration;

        /// <summary>Number of falls recorded in the current session.</summary>
        public int   FallCount               => _fallCount;

        /// <summary>Best height reached in the current session.</summary>
        public float BestHeightThisSession   => _bestHeightThisSession;

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
            BindFallTrackerEvents();
        }

        private void OnDestroy()
        {
            UnbindFallTrackerEvents();
        }

        // -----------------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------------

        /// <summary>Starts a new run session.</summary>
        public void StartSession()
        {
            ResolveReferences();

            _sessionActive          = true;
            _sessionStartTime       = Time.time;
            _fallCount              = 0;
            _bestHeightThisSession  = 0f;

            // Re-bind in case FallTracker reference was resolved after Start()
            BindFallTrackerEvents();

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
            float maxHeight = _bestHeightThisSession > 0f
                ? _bestHeightThisSession
                : (fallTracker != null ? fallTracker.BestHeightEver : 0f);
            int   falls     = _fallCount;

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

        /// <summary>Ends the current session. Pass true for a victorious completion.</summary>
        public void EndSession(bool completed)
        {
            EndSession(completed ? EndReason.Victory : EndReason.Quit);
        }

        // -----------------------------------------------------------------------
        // Private helpers
        // -----------------------------------------------------------------------

        private void BindFallTrackerEvents()
        {
            if (fallTracker == null) return;
            // Guard against double-subscription by removing before adding
            fallTracker.OnFallCompleted.RemoveListener(OnFallCompleted);
            fallTracker.OnFallCompleted.AddListener(OnFallCompleted);

            fallTracker.OnNewHeightRecord.RemoveListener(OnNewHeightRecord);
            fallTracker.OnNewHeightRecord.AddListener(OnNewHeightRecord);
        }

        private void UnbindFallTrackerEvents()
        {
            if (fallTracker == null) return;
            fallTracker.OnFallCompleted.RemoveListener(OnFallCompleted);
            fallTracker.OnNewHeightRecord.RemoveListener(OnNewHeightRecord);
        }

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
            _fallCount++;
            achievementValidator?.NotifyFall(data);
        }

        private void OnNewHeightRecord(float height)
        {
            if (height > _bestHeightThisSession)
                _bestHeightThisSession = height;
        }
    }
}
