using System;
using System.Collections.Generic;
using UnityEngine;

#if STEAMWORKS_NET
using Steamworks;
#endif

namespace TitanAscent.Systems
{
    [Serializable]
    public class LeaderboardEntry
    {
        public string playerName   = "You";
        public float  heightReached;
        public float  timeSeconds;
        public int    fallCount;
        public string date         = string.Empty;
        public bool   isLocalPlayer = false;
    }

    [Serializable]
    public class LeaderboardSaveData
    {
        public List<LeaderboardEntry> entries = new List<LeaderboardEntry>();
    }

    public class LeaderboardManager : MonoBehaviour
    {
        private const string SaveKey  = "Leaderboard_Local";
        private const int    MaxEntries = 10;

        private List<LeaderboardEntry> localEntries = new List<LeaderboardEntry>();

        private void Awake()
        {
            LoadFromPlayerPrefs();
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Adds a run to the local top-10, sorted by height reached (descending).
        /// </summary>
        public void SubmitRun(float height, float time, int falls)
        {
            LeaderboardEntry entry = new LeaderboardEntry
            {
                playerName    = "You",
                heightReached = height,
                timeSeconds   = time,
                fallCount     = falls,
                date          = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                isLocalPlayer = true,
            };

            localEntries.Add(entry);

            // Sort by height descending
            localEntries.Sort((a, b) => b.heightReached.CompareTo(a.heightReached));

            // Trim to top 10
            if (localEntries.Count > MaxEntries)
                localEntries.RemoveRange(MaxEntries, localEntries.Count - MaxEntries);

            SaveToPlayerPrefs();
        }

        /// <summary>Returns the top N entries (up to the number available).</summary>
        public List<LeaderboardEntry> GetTopEntries(int count)
        {
            int take = Mathf.Min(count, localEntries.Count);
            return localEntries.GetRange(0, take);
        }

        /// <summary>Returns the single best local player entry, or null if none.</summary>
        public LeaderboardEntry GetPlayerBestEntry()
        {
            foreach (LeaderboardEntry e in localEntries)
            {
                if (e.isLocalPlayer)
                    return e;
            }
            return null;
        }

        /// <summary>Formats seconds as "1h 23m 45s".</summary>
        public static string FormatTime(float seconds)
        {
            int total = Mathf.FloorToInt(seconds);
            int h = total / 3600;
            int m = (total % 3600) / 60;
            int s = total % 60;

            if (h > 0)
                return $"{h}h {m:D2}m {s:D2}s";
            if (m > 0)
                return $"{m}m {s:D2}s";
            return $"{s}s";
        }

        // ── Steam integration stub ────────────────────────────────────────────────
#if STEAMWORKS_NET
        /// <summary>Submits an entry to the Steam leaderboard (stub).</summary>
        public static void SubmitToSteamLeaderboard(LeaderboardEntry entry)
        {
            // TODO: Implement via SteamUserStats.FindOrCreateLeaderboard and
            // SteamUserStats.UploadLeaderboardScore once the Steam leaderboard
            // name is finalised.
        }

        /// <summary>Fetches entries from the Steam leaderboard (stub).</summary>
        public static void FetchSteamLeaderboard(System.Action<List<LeaderboardEntry>> callback)
        {
            // TODO: Use SteamUserStats.DownloadLeaderboardEntries and convert
            // SteamLeaderboardEntries_t to List<LeaderboardEntry>, then invoke callback.
            callback?.Invoke(new List<LeaderboardEntry>());
        }
#endif

        // ── Persistence ───────────────────────────────────────────────────────────

        private void SaveToPlayerPrefs()
        {
            LeaderboardSaveData data = new LeaderboardSaveData { entries = localEntries };
            string json = JsonUtility.ToJson(data);
            PlayerPrefs.SetString(SaveKey, json);
            PlayerPrefs.Save();
        }

        private void LoadFromPlayerPrefs()
        {
            if (!PlayerPrefs.HasKey(SaveKey)) return;

            try
            {
                string json = PlayerPrefs.GetString(SaveKey);
                LeaderboardSaveData data = JsonUtility.FromJson<LeaderboardSaveData>(json);
                if (data?.entries != null)
                    localEntries = data.entries;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LeaderboardManager] Failed to load leaderboard: {e.Message}");
                localEntries = new List<LeaderboardEntry>();
            }
        }
    }
}
