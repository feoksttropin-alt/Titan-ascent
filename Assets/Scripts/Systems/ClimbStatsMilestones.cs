using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace TitanAscent.Systems
{
    // ── Milestone stat enum ───────────────────────────────────────────────────

    public enum MilestoneStat
    {
        TotalFalls,
        TotalClimbs,
        BestHeight,
        TotalDistanceFallen,
    }

    // ── Milestone definition ──────────────────────────────────────────────────

    [Serializable]
    public struct Milestone
    {
        public MilestoneStat stat;
        public int           threshold;
        public string        narrationLine;  // empty = no narration
    }

    // ── ClimbStatsMilestones ──────────────────────────────────────────────────

    /// <summary>
    /// Fires events when cumulative stats cross milestone thresholds.
    /// Persists completion state via SaveManager (PlayerPrefs key).
    /// Triggers AchievementSystem unlocks and optional NarrationSystem lines.
    /// </summary>
    public class ClimbStatsMilestones : MonoBehaviour
    {
        // ── Events ────────────────────────────────────────────────────────────

        /// <summary>Fires when any milestone threshold is crossed for the first time.</summary>
        public UnityEvent<MilestoneStat, int> OnMilestoneReached = new UnityEvent<MilestoneStat, int>();

        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("References")]
        [SerializeField] private SaveManager       saveManager;
        [SerializeField] private AchievementSystem achievementSystem;
        [SerializeField] private NarrationSystem   narrationSystem;
        [SerializeField] private FallTracker       fallTracker;

        // ── Persistence key ───────────────────────────────────────────────────

        private const string MilestoneSaveKey = "TitanAscent_Milestones";

        // ── Milestone tables ──────────────────────────────────────────────────

        private static readonly Milestone[] FallMilestones =
        {
            new Milestone { stat = MilestoneStat.TotalFalls, threshold =  10,  narrationLine = "" },
            new Milestone { stat = MilestoneStat.TotalFalls, threshold =  25,  narrationLine = "" },
            new Milestone { stat = MilestoneStat.TotalFalls, threshold =  50,  narrationLine = "" },
            new Milestone { stat = MilestoneStat.TotalFalls, threshold = 100,  narrationLine = "100 falls. The titan has noticed." },
            new Milestone { stat = MilestoneStat.TotalFalls, threshold = 250,  narrationLine = "250 falls. You are made of patience." },
            new Milestone { stat = MilestoneStat.TotalFalls, threshold = 500,  narrationLine = "500 falls. The mountain has your name." },
        };

        private static readonly Milestone[] ClimbMilestones =
        {
            new Milestone { stat = MilestoneStat.TotalClimbs, threshold =  1,  narrationLine = "" },
            new Milestone { stat = MilestoneStat.TotalClimbs, threshold =  5,  narrationLine = "" },
            new Milestone { stat = MilestoneStat.TotalClimbs, threshold = 10,  narrationLine = "Ten ascents. The titan is familiar now." },
            new Milestone { stat = MilestoneStat.TotalClimbs, threshold = 25,  narrationLine = "Twenty-five attempts. Persistence is a kind of power." },
        };

        private static readonly Milestone[] HeightMilestones =
        {
            new Milestone { stat = MilestoneStat.BestHeight, threshold =  500,  narrationLine = "" },
            new Milestone { stat = MilestoneStat.BestHeight, threshold = 1000,  narrationLine = "1000 meters. The world below has shrunk." },
            new Milestone { stat = MilestoneStat.BestHeight, threshold = 2000,  narrationLine = "2000 meters. Few reach this far." },
            new Milestone { stat = MilestoneStat.BestHeight, threshold = 3000,  narrationLine = "" },
            new Milestone { stat = MilestoneStat.BestHeight, threshold = 5000,  narrationLine = "Halfway. The summit is still above the clouds." },
            new Milestone { stat = MilestoneStat.BestHeight, threshold = 7000,  narrationLine = "7000 meters. The storm is behind you." },
            new Milestone { stat = MilestoneStat.BestHeight, threshold = 9000,  narrationLine = "9000 meters. The crown is close enough to touch." },
            new Milestone { stat = MilestoneStat.BestHeight, threshold = 10000, narrationLine = "The summit. You did it." },
        };

        private static readonly Milestone[] DistanceFallenMilestones =
        {
            new Milestone { stat = MilestoneStat.TotalDistanceFallen, threshold =  1000,  narrationLine = "" },
            new Milestone { stat = MilestoneStat.TotalDistanceFallen, threshold =  5000,  narrationLine = "5 kilometers fallen, total." },
            new Milestone { stat = MilestoneStat.TotalDistanceFallen, threshold = 10000,  narrationLine = "10 kilometers fallen across all attempts." },
        };

        // ── Runtime state ─────────────────────────────────────────────────────

        // Set of "stat:threshold" strings for completed milestones
        private readonly HashSet<string> _completedMilestones = new HashSet<string>();

        // Running cumulative distance fallen (not persisted between sessions in SaveData by default;
        // stored in PlayerPrefs under MilestoneSaveKey JSON)
        private float _totalDistanceFallen;

        // Tracks live fall progress
        private float _currentFallStartHeight;

        [Serializable]
        private class MilestoneSaveData
        {
            public List<string> completed         = new List<string>();
            public float        totalDistanceFallen = 0f;
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            if (saveManager == null)
                saveManager = FindFirstObjectByType<SaveManager>();
            if (achievementSystem == null)
                achievementSystem = FindFirstObjectByType<AchievementSystem>();
            if (narrationSystem == null)
                narrationSystem = FindFirstObjectByType<NarrationSystem>();
            if (fallTracker == null)
                fallTracker = FindFirstObjectByType<FallTracker>();

            LoadMilestoneState();
        }

        private void Start()
        {
            if (fallTracker != null)
                fallTracker.OnFallCompleted.AddListener(OnFallCompleted);

            // Initial checks against existing save data
            if (saveManager != null)
                CheckAllMilestones(saveManager.CurrentData);
        }

        private void OnDestroy()
        {
            if (fallTracker != null)
                fallTracker.OnFallCompleted.RemoveListener(OnFallCompleted);
        }

        // ── Fall tracking ─────────────────────────────────────────────────────

        private void OnFallCompleted(FallData data)
        {
            _totalDistanceFallen += data.distance;
            SaveMilestoneState();

            if (saveManager != null)
                CheckAllMilestones(saveManager.CurrentData);
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Called by GameManager / SessionManager after each save to check if any milestone has been crossed.
        /// </summary>
        public void CheckAllMilestones(SaveData data)
        {
            if (data == null) return;

            CheckGroup(FallMilestones,          data.totalFalls,         (int)data.totalFalls);
            CheckGroup(ClimbMilestones,         data.totalClimbs,        (int)data.totalClimbs);
            CheckGroup(HeightMilestones,        (int)data.bestHeight,    (int)data.bestHeight);
            CheckGroup(DistanceFallenMilestones,(int)_totalDistanceFallen,(int)_totalDistanceFallen);
        }

        // ── Internal milestone logic ──────────────────────────────────────────

        private void CheckGroup(Milestone[] milestones, int currentValue, int displayValue)
        {
            foreach (Milestone m in milestones)
            {
                if (currentValue < m.threshold) continue;

                string key = $"{m.stat}:{m.threshold}";
                if (_completedMilestones.Contains(key)) continue;

                // Mark complete
                _completedMilestones.Add(key);
                SaveMilestoneState();

                // Fire event
                OnMilestoneReached?.Invoke(m.stat, m.threshold);

                // Narration
                if (!string.IsNullOrEmpty(m.narrationLine))
                    narrationSystem?.NarrateRaw(m.narrationLine);

                // Achievement unlocks keyed by stat/threshold
                TryUnlockAchievement(m.stat, m.threshold);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[ClimbStatsMilestones] Milestone reached: {m.stat} >= {m.threshold}");
#endif
            }
        }

        private void TryUnlockAchievement(MilestoneStat stat, int threshold)
        {
            if (achievementSystem == null) return;

            switch (stat)
            {
                case MilestoneStat.TotalFalls when threshold == 100:
                    achievementSystem.Unlock("hundred_falls");
                    break;
                case MilestoneStat.BestHeight when threshold == 1000:
                    achievementSystem.Unlock("thousand_meters");
                    break;
                case MilestoneStat.BestHeight when threshold == 5000:
                    achievementSystem.Unlock("five_thousand_meters");
                    break;
                case MilestoneStat.BestHeight when threshold == 10000:
                    achievementSystem.Unlock("first_summit");
                    break;
            }
        }

        // ── Persistence ───────────────────────────────────────────────────────

        private void SaveMilestoneState()
        {
            MilestoneSaveData data = new MilestoneSaveData
            {
                totalDistanceFallen = _totalDistanceFallen
            };
            data.completed.AddRange(_completedMilestones);

            string json = JsonUtility.ToJson(data);
            PlayerPrefs.SetString(MilestoneSaveKey, json);
            PlayerPrefs.Save();
        }

        private void LoadMilestoneState()
        {
            if (!PlayerPrefs.HasKey(MilestoneSaveKey)) return;

            try
            {
                string json = PlayerPrefs.GetString(MilestoneSaveKey);
                MilestoneSaveData data = JsonUtility.FromJson<MilestoneSaveData>(json);
                if (data == null) return;

                _totalDistanceFallen = data.totalDistanceFallen;
                foreach (string key in data.completed)
                    _completedMilestones.Add(key);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ClimbStatsMilestones] Failed to load milestone state: {e.Message}");
            }
        }
    }
}
