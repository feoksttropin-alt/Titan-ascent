using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using TitanAscent.Environment;

namespace TitanAscent.Systems
{
    [Serializable]
    public struct Achievement
    {
        public string id;
        public string displayName;
        public string description;
        public bool isUnlocked;
        public string unlockedAt;   // DateTime.ToString("o")
        public string iconName;
        public bool isSecret;
    }

    [Serializable]
    public struct GameStateSnapshot
    {
        public float currentHeight;
        public int totalFalls;
        public float longestFall;
        public int totalClimbs;
        public HashSet<int> zonesVisited;
        public HashSet<SurfaceType> surfacesGrappled;
        public bool thrusterUsedInZone1;
    }

    [Serializable]
    public class AchievementSaveData
    {
        public List<string> unlockedIds = new List<string>();
        public List<string> unlockedDates = new List<string>();
    }

    public class AchievementSystem : MonoBehaviour
    {
        // ── Pre-defined achievements ────────────────────────────────────────────
        public static readonly Achievement A_FirstSummit = new Achievement
        {
            id = "first_summit", displayName = "The Crown",
            description = "Reach the summit for the first time",
            iconName = "icon_crown"
        };
        public static readonly Achievement A_HundredFalls = new Achievement
        {
            id = "hundred_falls", displayName = "Dedicated Student",
            description = "Fall 100 times",
            iconName = "icon_falls"
        };
        public static readonly Achievement A_ThousandMeters = new Achievement
        {
            id = "thousand_meters", displayName = "Above the Clouds",
            description = "Reach 1,000m",
            iconName = "icon_clouds"
        };
        public static readonly Achievement A_FiveThousandMeters = new Achievement
        {
            id = "five_thousand_meters", displayName = "Halfway There",
            description = "Reach 5,000m",
            iconName = "icon_halfway"
        };
        public static readonly Achievement A_CatastrophicFall = new Achievement
        {
            id = "catastrophic_fall", displayName = "The Long Way Down",
            description = "Fall more than 500m in one drop",
            iconName = "icon_longfall"
        };
        public static readonly Achievement A_PerfectRecovery = new Achievement
        {
            id = "perfect_recovery", displayName = "Against All Odds",
            description = "Recover from a fall of more than 200m",
            iconName = "icon_recovery"
        };
        public static readonly Achievement A_NoThrusterZone1 = new Achievement
        {
            id = "no_thruster_zone1", displayName = "Purist",
            description = "Complete Zone 1 without using thrusters",
            iconName = "icon_purist"
        };
        public static readonly Achievement A_SpeedrunSub2h = new Achievement
        {
            id = "speedrun_sub_2h", displayName = "Efficiency",
            description = "Summit in under 2 hours",
            iconName = "icon_speedrun"
        };
        public static readonly Achievement A_Daily7Streak = new Achievement
        {
            id = "daily_7_streak", displayName = "Dedicated",
            description = "Complete 7 daily challenges",
            iconName = "icon_streak"
        };
        public static readonly Achievement A_GraveyardLore = new Achievement
        {
            id = "graveyard_lore", displayName = "Historian",
            description = "Find all landmarks in The Graveyard",
            iconName = "icon_historian"
        };
        public static readonly Achievement A_WindSurfer = new Achievement
        {
            id = "wind_surfer", displayName = "Wind Reader",
            description = "Use a wind column to gain 100m of height",
            iconName = "icon_wind"
        };
        public static readonly Achievement A_LongestSwing = new Achievement
        {
            id = "longest_swing", displayName = "The Pendulum",
            description = "Complete a swing arc of more than 30m",
            iconName = "icon_swing"
        };
        public static readonly Achievement A_FellFromCrown = new Achievement
        {
            id = "fell_from_crown", displayName = "So Close",
            description = "Fall from above 9,000m",
            iconName = "icon_soclose"
        };
        public static readonly Achievement A_AllSurfaces = new Achievement
        {
            id = "all_surfaces", displayName = "Student of the Titan",
            description = "Grapple onto every surface type",
            iconName = "icon_surfaces"
        };
        public static readonly Achievement A_ZeroFallsSummit = new Achievement
        {
            id = "zero_falls_summit", displayName = "Ghost",
            description = "SECRET: Reach the summit with zero falls",
            iconName = "icon_ghost",
            isSecret = true
        };

        private static readonly Achievement[] AllDefinitions =
        {
            A_FirstSummit, A_HundredFalls, A_ThousandMeters, A_FiveThousandMeters,
            A_CatastrophicFall, A_PerfectRecovery, A_NoThrusterZone1, A_SpeedrunSub2h,
            A_Daily7Streak, A_GraveyardLore, A_WindSurfer, A_LongestSwing,
            A_FellFromCrown, A_AllSurfaces, A_ZeroFallsSummit
        };

        // ── Events ──────────────────────────────────────────────────────────────
        public UnityEvent<Achievement> OnAchievementUnlocked = new UnityEvent<Achievement>();

        // ── Save key ────────────────────────────────────────────────────────────
        private const string SaveKey = "TitanAscent_Achievements";

        // ── Runtime state ───────────────────────────────────────────────────────
        private Dictionary<string, Achievement> achievements = new Dictionary<string, Achievement>();

        [SerializeField] private SaveManager saveManager;

        private void Awake()
        {
            if (saveManager == null)
                saveManager = FindFirstObjectByType<SaveManager>();

            // Populate working dictionary from static definitions
            foreach (Achievement def in AllDefinitions)
                achievements[def.id] = def;

            LoadUnlockState();
        }

        // ── Public API ──────────────────────────────────────────────────────────

        /// <summary>Unlocks the achievement with the given id. Fires event and saves.</summary>
        public void Unlock(string id)
        {
            if (!achievements.TryGetValue(id, out Achievement a)) return;
            if (a.isUnlocked) return;

            a.isUnlocked = true;
            a.unlockedAt = DateTime.UtcNow.ToString("o");
            achievements[id] = a;

            SaveUnlockState();
            OnAchievementUnlocked?.Invoke(a);
        }

        /// <summary>Returns true if the achievement with the given id is unlocked.</summary>
        public bool IsUnlocked(string id)
        {
            return achievements.TryGetValue(id, out Achievement a) && a.isUnlocked;
        }

        /// <summary>Returns all achievements (locked and unlocked).</summary>
        public IReadOnlyList<Achievement> GetAll()
        {
            Achievement[] result = new Achievement[AllDefinitions.Length];
            for (int i = 0; i < AllDefinitions.Length; i++)
                result[i] = achievements[AllDefinitions[i].id];
            return result;
        }

        /// <summary>
        /// Checks all auto-unlock conditions against the supplied snapshot.
        /// Call this periodically (e.g. once per second) from GameManager.
        /// </summary>
        public void CheckAutoAchievements(GameStateSnapshot snapshot)
        {
            // Height milestones
            if (snapshot.currentHeight >= 1000f)
                Unlock("thousand_meters");

            if (snapshot.currentHeight >= 5000f)
                Unlock("five_thousand_meters");

            if (snapshot.currentHeight >= 9000f)
                Unlock("fell_from_crown");   // We just track reaching this height; the fall itself triggers separately

            // Fall count
            if (snapshot.totalFalls >= 100)
                Unlock("hundred_falls");

            // Catastrophic single fall
            if (snapshot.longestFall > 500f)
                Unlock("catastrophic_fall");

            // No thruster in zone 1
            if (!snapshot.thrusterUsedInZone1 && snapshot.zonesVisited != null && snapshot.zonesVisited.Count > 1)
                Unlock("no_thruster_zone1");

            // All surface types grappled
            if (snapshot.surfacesGrappled != null)
            {
                bool hasAll = true;
                foreach (SurfaceType st in Enum.GetValues(typeof(SurfaceType)))
                {
                    if (!snapshot.surfacesGrappled.Contains(st)) { hasAll = false; break; }
                }
                if (hasAll)
                    Unlock("all_surfaces");
            }
        }

        // ── Persistence ─────────────────────────────────────────────────────────

        private void SaveUnlockState()
        {
            AchievementSaveData data = new AchievementSaveData();
            foreach (KeyValuePair<string, Achievement> kvp in achievements)
            {
                if (kvp.Value.isUnlocked)
                {
                    data.unlockedIds.Add(kvp.Key);
                    data.unlockedDates.Add(kvp.Value.unlockedAt ?? string.Empty);
                }
            }

            string json = JsonUtility.ToJson(data);
            PlayerPrefs.SetString(SaveKey, json);
            PlayerPrefs.Save();
        }

        private void LoadUnlockState()
        {
            if (!PlayerPrefs.HasKey(SaveKey)) return;

            try
            {
                string json = PlayerPrefs.GetString(SaveKey);
                AchievementSaveData data = JsonUtility.FromJson<AchievementSaveData>(json);
                if (data == null) return;

                for (int i = 0; i < data.unlockedIds.Count; i++)
                {
                    string id = data.unlockedIds[i];
                    if (!achievements.TryGetValue(id, out Achievement a)) continue;

                    a.isUnlocked = true;
                    a.unlockedAt = i < data.unlockedDates.Count ? data.unlockedDates[i] : string.Empty;
                    achievements[id] = a;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AchievementSystem] Failed to load achievements: {e.Message}");
            }
        }
    }
}
