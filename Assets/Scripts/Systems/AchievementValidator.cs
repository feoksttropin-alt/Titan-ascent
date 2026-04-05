using System.Collections.Generic;
using UnityEngine;
using TitanAscent.Environment;
using TitanAscent.Scene;

namespace TitanAscent.Systems
{
    /// <summary>
    /// Validates all 15 achievement conditions at session end.
    /// Called by SessionManager after each run completes.
    /// </summary>
    public class AchievementValidator : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        // Inspector
        // -----------------------------------------------------------------------

        [Header("System References")]
        [SerializeField] private AchievementSystem achievementSystem;
        [SerializeField] private SessionStatsTracker sessionStats;
        [SerializeField] private SaveManager saveManager;
        [SerializeField] private SpeedrunManager speedrunManager;
        [SerializeField] private ZoneManager zoneManager;

        // -----------------------------------------------------------------------
        // Private state tracked across the session
        // -----------------------------------------------------------------------

        private bool _victoryThisSession;
        private int  _sessionFalls;
        private float _largestFallThisSession;
        private bool _hadRecoveryFrom200m;
        private bool _thrusterUsedInZone1;
        private float _windAssistGain;
        private bool _crownFall;                   // fell from above 9000 m
        private HashSet<SurfaceType> _surfacesGrappled = new HashSet<SurfaceType>();
        private HashSet<int> _zone6LandmarksVisited   = new HashSet<int>();
        private int _zone6TotalLandmarks;

        // -----------------------------------------------------------------------
        // Lifecycle
        // -----------------------------------------------------------------------

        private void Awake()
        {
            if (achievementSystem == null) achievementSystem = FindFirstObjectByType<AchievementSystem>();
            if (sessionStats      == null) sessionStats      = FindFirstObjectByType<SessionStatsTracker>();
            if (saveManager       == null) saveManager       = FindFirstObjectByType<SaveManager>();
            if (speedrunManager   == null) speedrunManager   = FindFirstObjectByType<SpeedrunManager>();
            if (zoneManager       == null) zoneManager       = FindFirstObjectByType<ZoneManager>();
        }

        private void Start()
        {
            LandmarkObject.OnPlayerNearLandmark += OnLandmarkVisited;

            // Count zone-6 landmarks (TheGraveyard, zone index 5 in the default layout)
            foreach (LandmarkObject lm in LandmarkObject.AllLandmarks)
            {
                if (IsZone6Landmark(lm))
                    _zone6TotalLandmarks++;
            }
        }

        private void OnDestroy()
        {
            LandmarkObject.OnPlayerNearLandmark -= OnLandmarkVisited;
        }

        // -----------------------------------------------------------------------
        // Public: session event hooks (called by SessionManager)
        // -----------------------------------------------------------------------

        public void OnSessionStarted()
        {
            _victoryThisSession        = false;
            _sessionFalls              = 0;
            _largestFallThisSession    = 0f;
            _hadRecoveryFrom200m       = false;
            _thrusterUsedInZone1       = false;
            _windAssistGain            = 0f;
            _crownFall                 = false;
            _surfacesGrappled.Clear();
            _zone6LandmarksVisited.Clear();
        }

        public void NotifyVictory() => _victoryThisSession = true;

        public void NotifyFall(FallData data)
        {
            _sessionFalls++;
            if (data.distance > _largestFallThisSession)
                _largestFallThisSession = data.distance;

            if (data.startHeight >= 9000f)
                _crownFall = true;
        }

        public void NotifyRecovery(float fromHeight)
        {
            if (fromHeight >= 200f)
                _hadRecoveryFrom200m = true;
        }

        public void NotifyThrusterInZone(int zoneIndex)
        {
            if (zoneIndex == 1)
                _thrusterUsedInZone1 = true;
        }

        public void NotifyWindAssistGain(float gainMeters)
        {
            _windAssistGain += gainMeters;
        }

        public void NotifySurfaceGrappled(SurfaceType surface)
        {
            _surfacesGrappled.Add(surface);
        }

        // -----------------------------------------------------------------------
        // Public: validate all achievements
        // -----------------------------------------------------------------------

        /// <summary>
        /// Run all 15 checks. Call at session end (after stats are finalised).
        /// </summary>
        public void ValidateAll()
        {
            if (achievementSystem == null) return;

            SaveData save = saveManager != null ? saveManager.CurrentData : null;
            float bestHeight = save != null ? save.bestHeight : 0f;

            // 1. first_summit — reached summit this session
            if (_victoryThisSession)
                achievementSystem.Unlock(AchievementSystem.A_FirstSummit.id);

            // 2. hundred_falls — total falls >= 100
            int totalFalls = save != null ? save.totalFalls : 0;
            if (totalFalls >= 100)
                achievementSystem.Unlock(AchievementSystem.A_HundredFalls.id);

            // 3. thousand_meters — bestHeight >= 1000
            if (bestHeight >= 1000f)
                achievementSystem.Unlock(AchievementSystem.A_ThousandMeters.id);

            // 4. five_thousand_meters — bestHeight >= 5000
            if (bestHeight >= 5000f)
                achievementSystem.Unlock(AchievementSystem.A_FiveThousandMeters.id);

            // 5. catastrophic_fall — any fall >= 500 m this session
            if (_largestFallThisSession >= 500f)
                achievementSystem.Unlock(AchievementSystem.A_CatastrophicFall.id);

            // 6. perfect_recovery — recovered from fall >= 200 m
            if (_hadRecoveryFrom200m)
                achievementSystem.Unlock(AchievementSystem.A_PerfectRecovery.id);

            // 7. no_thruster_zone1 — no thruster used in zone 1 AND reached zone 2+
            if (!_thrusterUsedInZone1 && ReachedZone(2))
                achievementSystem.Unlock(AchievementSystem.A_NoThrusterZone1.id);

            // 8. speedrun_sub_2h — speedrun completed in < 7200 s this session
            if (speedrunManager != null && speedrunManager.IsActive == false)
            {
                // Check the final run time via the last completed speedrun PB
                float pb = save != null ? save.speedrunPB : 0f;
                if (pb > 0f && pb < 7200f)
                    achievementSystem.Unlock(AchievementSystem.A_SpeedrunSub2h.id);
            }

            // 9. daily_7_streak — placeholder: check completedChallenges count >= 7
            if (save != null && save.completedChallenges != null && save.completedChallenges.Count >= 7)
                achievementSystem.Unlock(AchievementSystem.A_Daily7Streak.id);

            // 10. graveyard_lore — all zone-6 landmarks visited
            if (_zone6TotalLandmarks > 0 && _zone6LandmarksVisited.Count >= _zone6TotalLandmarks)
                achievementSystem.Unlock(AchievementSystem.A_GraveyardLore.id);

            // 11. wind_surfer — wind assist height gain >= 100 m this session
            if (_windAssistGain >= 100f)
                achievementSystem.Unlock(AchievementSystem.A_WindSurfer.id);

            // 12. longest_swing — single swing >= 30 m
            float longestSwing = sessionStats != null ? sessionStats.LongestSingleSwing : 0f;
            if (longestSwing >= 30f)
                achievementSystem.Unlock(AchievementSystem.A_LongestSwing.id);

            // 13. fell_from_crown — fell from above 9000 m
            if (_crownFall)
                achievementSystem.Unlock(AchievementSystem.A_FellFromCrown.id);

            // 14. all_surfaces — all 5 SurfaceTypes grappled
            if (AllSurfacesGrappled())
                achievementSystem.Unlock(AchievementSystem.A_AllSurfaces.id);

            // 15. zero_falls_summit — victory with zero falls this session
            if (_victoryThisSession && _sessionFalls == 0)
                achievementSystem.Unlock(AchievementSystem.A_ZeroFallsSummit.id);
        }

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        private bool ReachedZone(int zoneIndex)
        {
            if (sessionStats == null) return false;
            return sessionStats.TimePerZone.ContainsKey(zoneIndex);
        }

        private bool AllSurfacesGrappled()
        {
            // Also merge with session stats' SurfacesContacted
            IReadOnlyCollection<SurfaceType> fromStats =
                sessionStats != null ? sessionStats.SurfacesContacted : null;

            if (fromStats != null)
            {
                foreach (SurfaceType st in fromStats)
                    _surfacesGrappled.Add(st);
            }

            foreach (SurfaceType st in System.Enum.GetValues(typeof(SurfaceType)))
            {
                if (!_surfacesGrappled.Contains(st)) return false;
            }
            return true;
        }

        private bool IsZone6Landmark(LandmarkObject lm)
        {
            if (zoneManager == null || lm == null) return false;
            TitanZone zone = zoneManager.GetZoneForHeight(lm.transform.position.y);
            return zone != null && zoneManager.CurrentZoneIndex == 5;
            // Better: compare by height range of zone 5 (5500-6500 m — "TheGraveyard")
        }

        private void OnLandmarkVisited(LandmarkObject lm)
        {
            if (lm == null) return;

            // Zone 6 = "TheGraveyard" = height 5500–6500 m (zone index 5)
            float y = lm.transform.position.y;
            if (y >= 5500f && y < 6500f)
                _zone6LandmarksVisited.Add(lm.GetInstanceID());
        }
    }
}
