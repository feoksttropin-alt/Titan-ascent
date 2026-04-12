using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TitanAscent.Systems
{
    /// <summary>
    /// Analyzes difficulty from session data, tracking per-100m bands.
    /// Writes difficulty reports and detects spikes/valleys.
    /// </summary>
    public class DifficultyAnalyzer : MonoBehaviour
    {
        // ------------------------------------------------------------------
        // Inner types
        // ------------------------------------------------------------------

        [Serializable]
        private class BandData
        {
            public int   bandIndex;     // band 0 = 0–99m, band 1 = 100–199m, etc.
            public int   falls;
            public float totalTimeInBand;
            public float entryTime;     // Time.time when player entered this band
            public bool  inBand;

            public float DifficultyScore => (falls * 2f) + (totalTimeInBand / 60f);
        }

        // ------------------------------------------------------------------
        // Constants
        // ------------------------------------------------------------------

        private const float BandSize           = 100f;
        private const float SpikeThreshold     = 15f;
        private const float ValleyThreshold    = 3f;
        private const string ReportFileName    = "difficulty_report.txt";

        // ------------------------------------------------------------------
        // State
        // ------------------------------------------------------------------

        private readonly Dictionary<int, BandData> bands = new Dictionary<int, BandData>();
        private int currentBand = -1;
        private float sessionStartTime;

        // ------------------------------------------------------------------
        // Lifecycle
        // ------------------------------------------------------------------

        private void Awake()
        {
            sessionStartTime = Time.time;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<FallEndedEvent>(OnFallEnded);
            EventBus.Subscribe<NewHeightEvent>(OnNewHeight);
            EventBus.Subscribe<VictoryEvent>(OnVictory);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<FallEndedEvent>(OnFallEnded);
            EventBus.Unsubscribe<NewHeightEvent>(OnNewHeight);
            EventBus.Unsubscribe<VictoryEvent>(OnVictory);
        }

        private void OnApplicationQuit()
        {
            // Flush band time on quit
            UpdateCurrentBandTime();
            WriteSessionReport();
        }

        // ------------------------------------------------------------------
        // Event handlers
        // ------------------------------------------------------------------

        private void OnFallEnded(FallEndedEvent evt)
        {
            int band = HeightToBand(evt.data.startHeight);
            BandData data = GetOrCreateBand(band);
            data.falls++;

            float score = data.DifficultyScore;

            if (score > SpikeThreshold)
                Debug.Log($"[DifficultyAnalyzer] DIFFICULTY SPIKE at {band * BandSize:F0}m (score {score:F1})");
            else if (score < ValleyThreshold && data.falls > 0)
                Debug.Log($"[DifficultyAnalyzer] DIFFICULTY VALLEY at {band * BandSize:F0}m (score {score:F1})");
        }

        private void OnNewHeight(NewHeightEvent evt)
        {
            int band = HeightToBand(evt.height);
            if (band == currentBand) return;

            // Close out old band
            UpdateCurrentBandTime();

            // Enter new band
            currentBand = band;
            BandData data = GetOrCreateBand(band);
            data.entryTime = Time.time;
            data.inBand = true;
        }

        private void OnVictory(VictoryEvent evt)
        {
            UpdateCurrentBandTime();
            WriteSessionReport();
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private int HeightToBand(float height) => Mathf.FloorToInt(Mathf.Max(0f, height) / BandSize);

        private BandData GetOrCreateBand(int band)
        {
            if (!bands.TryGetValue(band, out BandData data))
            {
                data = new BandData { bandIndex = band };
                bands[band] = data;
            }

            return data;
        }

        private void UpdateCurrentBandTime()
        {
            if (currentBand < 0) return;
            if (!bands.TryGetValue(currentBand, out BandData data)) return;
            if (!data.inBand) return;

            data.totalTimeInBand += Time.time - data.entryTime;
            data.entryTime = Time.time;
        }

        // ------------------------------------------------------------------
        // Report writing
        // ------------------------------------------------------------------

        private void WriteSessionReport()
        {
            string path = Path.Combine(Application.persistentDataPath, ReportFileName);

            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("=== TITAN ASCENT — DIFFICULTY REPORT ===");
                sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"Session duration: {(Time.time - sessionStartTime) / 60f:F1} min");
                sb.AppendLine();
                sb.AppendLine($"{"Band (m)",-15} {"Falls",-8} {"Time (s)",-12} {"Score",-10} {"Flag"}");
                sb.AppendLine(new string('-', 55));

                List<int> sortedKeys = new List<int>(bands.Keys);
                sortedKeys.Sort();

                foreach (int key in sortedKeys)
                {
                    BandData d = bands[key];
                    float score = d.DifficultyScore;
                    string flag = score > SpikeThreshold ? "SPIKE"
                                : score < ValleyThreshold && d.falls > 0 ? "VALLEY"
                                : "";

                    sb.AppendLine($"{key * BandSize,6:F0}–{(key + 1) * BandSize - 1,6:F0}   {d.falls,-8} {d.totalTimeInBand,-12:F1} {score,-10:F1} {flag}");
                }

                File.WriteAllText(path, sb.ToString());
                Debug.Log($"[DifficultyAnalyzer] Report written to {path}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[DifficultyAnalyzer] Failed to write report: {e.Message}");
            }
        }

        // ------------------------------------------------------------------
        // Editor menu item
        // ------------------------------------------------------------------

#if UNITY_EDITOR
        [MenuItem("TitanAscent/Generate Difficulty Report")]
        public static void GenerateAggregateReport()
        {
            string dir = Application.persistentDataPath;
            string[] files = Directory.GetFiles(dir, "playtest_*.json");

            if (files.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    "Difficulty Report",
                    $"No playtest_*.json files found in:\n{dir}",
                    "OK");
                return;
            }

            // Per-band aggregated stats: bandIndex → (totalFalls, totalTime, sessionCount)
            Dictionary<int, (int falls, float time, int sessions)> aggregate =
                new Dictionary<int, (int, float, int)>();

            int parsedSessions = 0;

            foreach (string file in files)
            {
                try
                {
                    string json = File.ReadAllText(file);
                    PlaytestSession session = JsonUtility.FromJson<PlaytestSession>(json);
                    if (session?.events == null) continue;

                    // Track per-band falls and rough time via entry ordering
                    Dictionary<int, float> bandEntryTime = new Dictionary<int, float>();
                    int currentBandLocal = -1;

                    foreach (LogEntry entry in session.events)
                    {
                        int band = Mathf.FloorToInt(Mathf.Max(0f, entry.height) / BandSize);

                        // Track time-in-band via sequential entries
                        if (band != currentBandLocal)
                        {
                            // Approximate time in previous band (difference between consecutive event times)
                            if (currentBandLocal >= 0 && bandEntryTime.TryGetValue(currentBandLocal, out float entryT))
                            {
                                float elapsed = entry.sessionTimeSeconds - entryT;
                                if (elapsed > 0f)
                                {
                                    var prev = aggregate.TryGetValue(currentBandLocal, out var p) ? p : (0, 0f, 0);
                                    aggregate[currentBandLocal] = (prev.falls, prev.time + elapsed, prev.sessions);
                                }
                            }

                            currentBandLocal = band;
                            bandEntryTime[band] = entry.sessionTimeSeconds;
                        }

                        if (entry.eventType == LogEventType.FallLarge.ToString()    ||
                            entry.eventType == LogEventType.FallMedium.ToString()   ||
                            entry.eventType == LogEventType.FallSmall.ToString()    ||
                            entry.eventType == LogEventType.FallCatastrophic.ToString())
                        {
                            var cur = aggregate.TryGetValue(band, out var c) ? c : (0, 0f, 0);
                            aggregate[band] = (cur.falls + 1, cur.time, cur.sessions);
                        }
                    }

                    parsedSessions++;

                    // Count sessions per band
                    foreach (int b in bandEntryTime.Keys)
                    {
                        var cur = aggregate.TryGetValue(b, out var cc) ? cc : (0, 0f, 0);
                        if (cur.sessions == 0)
                            aggregate[b] = (cur.falls, cur.time, 1);
                        else
                            aggregate[b] = (cur.falls, cur.time, cur.sessions + 1);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[DifficultyAnalyzer] Skipping {file}: {e.Message}");
                }
            }

            if (parsedSessions == 0)
            {
                EditorUtility.DisplayDialog("Difficulty Report", "No valid sessions found.", "OK");
                return;
            }

            // Write aggregate report
            string outPath = Path.Combine(dir, "difficulty_report_aggregate.txt");
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== TITAN ASCENT — AGGREGATE DIFFICULTY REPORT ===");
            sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Sessions analysed: {parsedSessions} / {files.Length} files");
            sb.AppendLine();
            sb.AppendLine($"{"Band (m)",-15} {"Avg Falls",-12} {"Avg Time(s)",-14} {"Avg Score",-12} {"Flag"}");
            sb.AppendLine(new string('-', 65));

            List<int> keys = new List<int>(aggregate.Keys);
            keys.Sort();

            foreach (int key in keys)
            {
                var (falls, time, sessions) = aggregate[key];
                float avgFalls  = (float)falls  / sessions;
                float avgTime   = time           / sessions;
                float avgScore  = (avgFalls * 2f) + (avgTime / 60f);
                string flag     = avgScore > SpikeThreshold  ? "SPIKE"
                                : avgScore < ValleyThreshold && avgFalls > 0 ? "VALLEY"
                                : "";

                sb.AppendLine($"{key * BandSize,6:F0}–{(key + 1) * BandSize - 1,6:F0}   {avgFalls,-12:F1} {avgTime,-14:F1} {avgScore,-12:F1} {flag}");
            }

            File.WriteAllText(outPath, sb.ToString());
            Debug.Log($"[DifficultyAnalyzer] Aggregate report written to {outPath}");

            EditorUtility.DisplayDialog(
                "Difficulty Report",
                $"Aggregate report saved to:\n{outPath}",
                "OK");
        }
#endif
    }
}
