#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TitanAscent.Debug
{
    // -----------------------------------------------------------------------
    // Data structures mirroring PlaytestLogger's JSON schema
    // -----------------------------------------------------------------------

    [Serializable]
    internal class ReportLogEntry
    {
        public string eventType;
        public float  height;
        public float  sessionTimeSeconds;
        public int    zoneIndex;
        public string additionalData;
    }

    [Serializable]
    internal class ReportSession
    {
        public string sessionId;
        public string startTime;
        public string buildVersion;
        public List<ReportLogEntry> events = new List<ReportLogEntry>();
    }

    // -----------------------------------------------------------------------
    // Generator
    // -----------------------------------------------------------------------

    public static class PlaytestReportGenerator
    {
        private const int   HeightBandSize  = 100;  // metres per band
        private const int   MaxHeight       = 10000;
        private const int   NumBands        = MaxHeight / HeightBandSize; // 100 bands
        private const int   BarChartWidth   = 40;   // chars
        private const float HotspotMultiplier = 2f;

        // -----------------------------------------------------------------------
        // Editor menu entry
        // -----------------------------------------------------------------------

#if UNITY_EDITOR
        [MenuItem("TitanAscent/Generate Playtest Report")]
        public static void GenerateReportFromMenu()
        {
            GenerateReport();
        }
#endif

        // -----------------------------------------------------------------------
        // Core generation (callable at runtime too)
        // -----------------------------------------------------------------------

        public static void GenerateReport()
        {
            string dataPath = Application.persistentDataPath;
            string[] jsonFiles = Directory.GetFiles(dataPath, "playtest_*.json");

            if (jsonFiles.Length == 0)
            {
                UnityEngine.Debug.LogWarning("[PlaytestReportGenerator] No playtest_*.json files found in " + dataPath);
                return;
            }

            List<ReportSession> sessions = ParseSessions(jsonFiles);
            string html = BuildHtml(sessions);

            string timestamp  = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string reportPath = Path.Combine(dataPath, $"playtest_report_{timestamp}.html");

            File.WriteAllText(reportPath, html, Encoding.UTF8);
            UnityEngine.Debug.Log("[PlaytestReportGenerator] Report written to: " + reportPath);
            Application.OpenURL("file://" + reportPath);
        }

        // -----------------------------------------------------------------------
        // Runtime quick summary for DebugMenu display
        // -----------------------------------------------------------------------

        public static string GetQuickSummary()
        {
            string dataPath = Application.persistentDataPath;

            string[] jsonFiles;
            try
            {
                jsonFiles = Directory.GetFiles(dataPath, "playtest_*.json");
            }
            catch (Exception e)
            {
                return "Error reading playtest files: " + e.Message;
            }

            if (jsonFiles.Length == 0)
                return "No playtest sessions found.";

            List<ReportSession> sessions = ParseSessions(jsonFiles);
            SummaryStats stats = ComputeSummary(sessions);

            var sb = new StringBuilder();
            sb.AppendLine($"Sessions : {stats.totalSessions}");
            sb.AppendLine($"Falls    : {stats.totalFalls}");
            sb.AppendLine($"Avg MaxH : {stats.avgMaxHeight:F0} m");
            sb.AppendLine($"Play Time: {FormatSeconds(stats.totalPlayTimeSeconds)}");
            return sb.ToString();
        }

        // -----------------------------------------------------------------------
        // Parsing
        // -----------------------------------------------------------------------

        private static List<ReportSession> ParseSessions(string[] paths)
        {
            var sessions = new List<ReportSession>();
            foreach (string path in paths)
            {
                try
                {
                    string json = File.ReadAllText(path, Encoding.UTF8);
                    ReportSession session = JsonUtility.FromJson<ReportSession>(json);
                    if (session != null)
                        sessions.Add(session);
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogWarning($"[PlaytestReportGenerator] Could not parse {path}: {e.Message}");
                }
            }
            return sessions;
        }

        // -----------------------------------------------------------------------
        // Statistics
        // -----------------------------------------------------------------------

        private struct SummaryStats
        {
            public int   totalSessions;
            public int   totalFalls;
            public float avgMaxHeight;
            public float totalPlayTimeSeconds;
        }

        private static SummaryStats ComputeSummary(List<ReportSession> sessions)
        {
            int   totalFalls     = 0;
            float sumMaxHeight   = 0f;
            float totalPlayTime  = 0f;

            foreach (ReportSession session in sessions)
            {
                float sessionMaxHeight = 0f;
                float sessionEndTime   = 0f;

                foreach (ReportLogEntry entry in session.events)
                {
                    if (IsFallEvent(entry.eventType))
                        totalFalls++;

                    if (entry.height > sessionMaxHeight)
                        sessionMaxHeight = entry.height;

                    if (entry.sessionTimeSeconds > sessionEndTime)
                        sessionEndTime = entry.sessionTimeSeconds;
                }

                sumMaxHeight  += sessionMaxHeight;
                totalPlayTime += sessionEndTime;
            }

            float avgMax = sessions.Count > 0 ? sumMaxHeight / sessions.Count : 0f;

            return new SummaryStats
            {
                totalSessions       = sessions.Count,
                totalFalls          = totalFalls,
                avgMaxHeight        = avgMax,
                totalPlayTimeSeconds = totalPlayTime
            };
        }

        private static int[] ComputeFallsPerBand(List<ReportSession> sessions)
        {
            int[] bands = new int[NumBands];
            foreach (ReportSession session in sessions)
            {
                foreach (ReportLogEntry entry in session.events)
                {
                    if (!IsFallEvent(entry.eventType)) continue;
                    int band = Mathf.Clamp(Mathf.FloorToInt(entry.height / HeightBandSize), 0, NumBands - 1);
                    bands[band]++;
                }
            }
            return bands;
        }

        private static bool IsFallEvent(string eventType)
        {
            return eventType == "FallSmall" ||
                   eventType == "FallMedium" ||
                   eventType == "FallLarge" ||
                   eventType == "FallCatastrophic";
        }

        // -----------------------------------------------------------------------
        // HTML Building
        // -----------------------------------------------------------------------

        private static string BuildHtml(List<ReportSession> sessions)
        {
            SummaryStats stats  = ComputeSummary(sessions);
            int[]        bands  = ComputeFallsPerBand(sessions);
            float        avgFallsPerBand = stats.totalFalls > 0 ? (float)stats.totalFalls / NumBands : 0f;

            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html><head><meta charset='utf-8'>");
            sb.AppendLine("<title>Titan Ascent Playtest Report</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("body{font-family:monospace;background:#1a1a2e;color:#e0e0e0;margin:20px;}");
            sb.AppendLine("h1{color:#f0a500;}h2{color:#a0c4ff;border-bottom:1px solid #444;padding-bottom:4px;}");
            sb.AppendLine("table{border-collapse:collapse;width:100%;margin-bottom:20px;}");
            sb.AppendLine("th{background:#2a2a4e;color:#f0a500;padding:6px 10px;text-align:left;}");
            sb.AppendLine("td{padding:4px 10px;border-bottom:1px solid #333;}");
            sb.AppendLine("tr:nth-child(even){background:#1e1e3a;}");
            sb.AppendLine(".stat{display:inline-block;margin:8px 20px 8px 0;background:#2a2a4e;padding:10px 20px;border-radius:4px;}");
            sb.AppendLine(".stat-value{font-size:2em;color:#f0a500;display:block;}");
            sb.AppendLine(".stat-label{font-size:0.8em;color:#a0c4ff;}");
            sb.AppendLine("pre{background:#0d0d1a;padding:12px;border-radius:4px;overflow-x:auto;}");
            sb.AppendLine(".hotspot{background:#3a1a1a;border-left:4px solid #f04040;padding:8px 12px;margin:4px 0;border-radius:0 4px 4px 0;}");
            sb.AppendLine(".rec{background:#1a3a1a;border-left:4px solid #40f080;padding:8px 12px;margin:4px 0;border-radius:0 4px 4px 0;}");
            sb.AppendLine("</style></head><body>");

            // Header
            sb.AppendLine("<h1>Titan Ascent — Playtest Report</h1>");
            sb.AppendLine($"<p>Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>");

            // Summary stats
            sb.AppendLine("<h2>Summary</h2>");
            AppendStat(sb, stats.totalSessions.ToString(),              "Total Sessions");
            AppendStat(sb, stats.totalFalls.ToString(),                 "Total Falls");
            AppendStat(sb, $"{stats.avgMaxHeight:F0} m",                "Avg Max Height");
            AppendStat(sb, FormatSeconds(stats.totalPlayTimeSeconds),   "Total Play Time");
            sb.AppendLine("<br><br>");

            // ASCII bar chart
            sb.AppendLine("<h2>Falls per 100m Height Band</h2>");
            sb.AppendLine("<pre>");
            int maxBandFalls = 0;
            foreach (int v in bands) if (v > maxBandFalls) maxBandFalls = v;

            for (int i = NumBands - 1; i >= 0; i--)
            {
                int    barLen = maxBandFalls > 0 ? Mathf.RoundToInt((float)bands[i] / maxBandFalls * BarChartWidth) : 0;
                string bar    = new string('#', barLen).PadRight(BarChartWidth);
                sb.AppendLine($"{i * HeightBandSize,5}m |{bar}| {bands[i]}");
            }
            sb.AppendLine("</pre>");

            // Per-session table
            sb.AppendLine("<h2>Per-Session Data</h2>");
            sb.AppendLine("<table><tr><th>Date</th><th>Max Height (m)</th><th>Falls</th><th>Zones Reached</th><th>Time</th><th>Completed</th></tr>");

            foreach (ReportSession session in sessions)
            {
                float sessionMaxHeight = 0f;
                float sessionEndTime   = 0f;
                int   fallCount        = 0;
                bool  completed        = false;
                var   zonesVisited     = new HashSet<int>();

                foreach (ReportLogEntry entry in session.events)
                {
                    if (entry.height > sessionMaxHeight) sessionMaxHeight = entry.height;
                    if (entry.sessionTimeSeconds > sessionEndTime) sessionEndTime = entry.sessionTimeSeconds;
                    if (IsFallEvent(entry.eventType)) fallCount++;
                    if (entry.eventType == "Victory") completed = true;
                    zonesVisited.Add(entry.zoneIndex);
                }

                string dateStr = "N/A";
                if (!string.IsNullOrEmpty(session.startTime))
                {
                    if (DateTime.TryParse(session.startTime, out DateTime dt))
                        dateStr = dt.ToString("yyyy-MM-dd HH:mm");
                }

                sb.AppendLine($"<tr><td>{dateStr}</td><td>{sessionMaxHeight:F0}</td><td>{fallCount}</td><td>{zonesVisited.Count}</td><td>{FormatSeconds(sessionEndTime)}</td><td>{(completed ? "Yes" : "No")}</td></tr>");
            }
            sb.AppendLine("</table>");

            // Frustration events log
            sb.AppendLine("<h2>Frustration Events Log</h2>");
            sb.AppendLine("<table><tr><th>Session</th><th>Event</th><th>Height (m)</th><th>Session Time</th></tr>");
            bool anyFrustration = false;
            foreach (ReportSession session in sessions)
            {
                foreach (ReportLogEntry entry in session.events)
                {
                    if (entry.eventType != "PlayerStuck") continue;
                    anyFrustration = true;
                    sb.AppendLine($"<tr><td>{session.sessionId.Substring(0, 8)}…</td><td>{entry.eventType}</td><td>{entry.height:F0}</td><td>{FormatSeconds(entry.sessionTimeSeconds)}</td></tr>");
                }
            }
            if (!anyFrustration)
                sb.AppendLine("<tr><td colspan='4'>No frustration events recorded.</td></tr>");
            sb.AppendLine("</table>");

            // Auto-recommendations (hotspot detection)
            sb.AppendLine("<h2>Recommendations</h2>");
            bool anyRecs = false;
            for (int i = 0; i < NumBands; i++)
            {
                if (avgFallsPerBand > 0f && bands[i] > avgFallsPerBand * HotspotMultiplier)
                {
                    int lo = i * HeightBandSize;
                    int hi = lo + HeightBandSize;
                    sb.AppendLine($"<div class='hotspot'>HOTSPOT: {lo}m–{hi}m — {bands[i]} falls. Consider adding recovery opportunity.</div>");
                    anyRecs = true;
                }
            }
            if (!anyRecs)
                sb.AppendLine("<div class='rec'>No hotspots detected. Difficulty distribution looks balanced.</div>");

            sb.AppendLine("</body></html>");
            return sb.ToString();
        }

        private static void AppendStat(StringBuilder sb, string value, string label)
        {
            sb.AppendLine($"<div class='stat'><span class='stat-value'>{value}</span><span class='stat-label'>{label}</span></div>");
        }

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        private static string FormatSeconds(float totalSeconds)
        {
            int h = Mathf.FloorToInt(totalSeconds / 3600f);
            int m = Mathf.FloorToInt((totalSeconds % 3600f) / 60f);
            int s = Mathf.FloorToInt(totalSeconds % 60f);
            return h > 0 ? $"{h}h {m:D2}m {s:D2}s" : $"{m:D2}m {s:D2}s";
        }
    }
}
#endif
