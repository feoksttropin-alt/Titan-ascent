using UnityEngine;

namespace TitanAscent.Systems
{
    /// <summary>
    /// Placeholder analytics system ready for Steam or custom analytics integration.
    ///
    /// All public methods are static so calling code never needs a reference to an instance.
    /// In dev builds, each event is logged to the Unity console.
    /// Real backend hooks are provided as stub comments throughout.
    ///
    /// Events defined:
    ///   session_start, climb_start, zone_entered, height_record, fall_occurred,
    ///   recovery_attempted, summit_reached, session_end
    /// </summary>
    public static class AnalyticsStub
    {
        // -----------------------------------------------------------------------
        // session_start
        // -----------------------------------------------------------------------

        /// <summary>
        /// Called once when the game session begins.
        /// </summary>
        /// <param name="buildVersion">Current build version string (e.g. "0.3.1").</param>
        /// <param name="platform">Runtime platform name (e.g. "WindowsPlayer").</param>
        public static void TrackSessionStart(string buildVersion, string platform)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Analytics] session_start | build_version={buildVersion} platform={platform}");
#endif
            // --- Steam Analytics stub ---
            // Steamworks.SteamUserStats.SetStat("sessions_played", sessionCount);
            // Steamworks.SteamUserStats.StoreStats();

            // --- Custom backend stub ---
            // AnalyticsBackend.Post("session_start", new {
            //     build_version = buildVersion,
            //     platform      = platform,
            //     timestamp     = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            // });
        }

        // -----------------------------------------------------------------------
        // climb_start
        // -----------------------------------------------------------------------

        /// <summary>
        /// Called when the player begins a new climb attempt.
        /// </summary>
        /// <param name="challengeModifier">Active challenge modifier name, or "none".</param>
        /// <param name="mode">Game mode, e.g. "standard", "speedrun", "challenge".</param>
        public static void TrackClimbStart(string challengeModifier, string mode)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Analytics] climb_start | challenge_modifier={challengeModifier} mode={mode}");
#endif
            // --- Custom backend stub ---
            // AnalyticsBackend.Post("climb_start", new {
            //     challenge_modifier = challengeModifier,
            //     mode               = mode
            // });
        }

        // -----------------------------------------------------------------------
        // zone_entered
        // -----------------------------------------------------------------------

        /// <summary>
        /// Called when the player enters a new zone.
        /// </summary>
        /// <param name="zoneIndex">Zero-based zone index.</param>
        /// <param name="zoneName">Human-readable zone name.</param>
        /// <param name="sessionTime">Elapsed seconds since session start.</param>
        public static void TrackZoneEntered(int zoneIndex, string zoneName, float sessionTime)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Analytics] zone_entered | zone_index={zoneIndex} zone_name={zoneName} session_time={sessionTime:F1}s");
#endif
            // --- Custom backend stub ---
            // AnalyticsBackend.Post("zone_entered", new {
            //     zone_index   = zoneIndex,
            //     zone_name    = zoneName,
            //     session_time = sessionTime
            // });
        }

        // -----------------------------------------------------------------------
        // height_record
        // -----------------------------------------------------------------------

        /// <summary>
        /// Called when the player reaches a new personal best height.
        /// </summary>
        /// <param name="height">New record height in metres.</param>
        /// <param name="sessionTime">Elapsed seconds since session start.</param>
        /// <param name="fallCount">Total falls in the current session so far.</param>
        public static void TrackHeightRecord(float height, float sessionTime, int fallCount)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Analytics] height_record | height={height:F1}m session_time={sessionTime:F1}s fall_count={fallCount}");
#endif
            // --- Custom backend stub ---
            // AnalyticsBackend.Post("height_record", new {
            //     height       = height,
            //     session_time = sessionTime,
            //     fall_count   = fallCount
            // });
        }

        // -----------------------------------------------------------------------
        // fall_occurred
        // -----------------------------------------------------------------------

        /// <summary>
        /// Called immediately when a fall is completed.
        /// </summary>
        /// <param name="height">Height at which the fall started (metres).</param>
        /// <param name="fallDistance">Total distance fallen (metres).</param>
        /// <param name="severity">Severity label: "small", "large", or "catastrophic".</param>
        /// <param name="zoneIndex">Zone index where the fall occurred.</param>
        public static void TrackFallOccurred(float height, float fallDistance, string severity, int zoneIndex)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Analytics] fall_occurred | height={height:F1}m fall_distance={fallDistance:F1}m severity={severity} zone_index={zoneIndex}");
#endif
            // --- Custom backend stub ---
            // AnalyticsBackend.Post("fall_occurred", new {
            //     height        = height,
            //     fall_distance = fallDistance,
            //     severity      = severity,
            //     zone_index    = zoneIndex
            // });
        }

        // -----------------------------------------------------------------------
        // recovery_attempted
        // -----------------------------------------------------------------------

        /// <summary>
        /// Called when the player attempts an emergency recovery grapple.
        /// </summary>
        /// <param name="height">Current height when recovery was attempted.</param>
        /// <param name="fallDistance">Distance fallen before the attempt.</param>
        /// <param name="succeeded">True if the grapple attached before hitting the ground.</param>
        public static void TrackRecoveryAttempted(float height, float fallDistance, bool succeeded)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Analytics] recovery_attempted | height={height:F1}m fall_distance={fallDistance:F1}m succeeded={succeeded}");
#endif
            // --- Custom backend stub ---
            // AnalyticsBackend.Post("recovery_attempted", new {
            //     height        = height,
            //     fall_distance = fallDistance,
            //     succeeded     = succeeded
            // });
        }

        // -----------------------------------------------------------------------
        // summit_reached
        // -----------------------------------------------------------------------

        /// <summary>
        /// Called when the player reaches the summit (victory condition).
        /// </summary>
        /// <param name="timeSeconds">Total elapsed climb time in seconds.</param>
        /// <param name="totalFalls">Total number of falls during the run.</param>
        /// <param name="longestFall">Longest single fall distance during the run (metres).</param>
        public static void TrackSummitReached(float timeSeconds, int totalFalls, float longestFall)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Analytics] summit_reached | time={timeSeconds:F1}s total_falls={totalFalls} longest_fall={longestFall:F1}m");
#endif
            // --- Steam Achievements stub ---
            // Steamworks.SteamUserStats.SetAchievement("ACH_SUMMIT");
            // Steamworks.SteamUserStats.StoreStats();

            // --- Custom backend stub ---
            // AnalyticsBackend.Post("summit_reached", new {
            //     time_seconds  = timeSeconds,
            //     total_falls   = totalFalls,
            //     longest_fall  = longestFall
            // });
        }

        // -----------------------------------------------------------------------
        // session_end
        // -----------------------------------------------------------------------

        /// <summary>
        /// Called when the session ends for any reason.
        /// </summary>
        /// <param name="maxHeight">Highest point reached this session (metres).</param>
        /// <param name="totalFalls">Total falls this session.</param>
        /// <param name="sessionTime">Total session duration in seconds.</param>
        /// <param name="reason">How the session ended: "quit", "death", or "victory".</param>
        public static void TrackSessionEnd(float maxHeight, int totalFalls, float sessionTime, string reason)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Analytics] session_end | max_height={maxHeight:F1}m total_falls={totalFalls} session_time={sessionTime:F1}s reason={reason}");
#endif
            // --- Custom backend stub ---
            // AnalyticsBackend.Post("session_end", new {
            //     max_height   = maxHeight,
            //     total_falls  = totalFalls,
            //     session_time = sessionTime,
            //     reason       = reason
            // });
        }
    }
}
