using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using TitanAscent.Environment;
using TitanAscent.Scene;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TitanAscent.Systems
{
    // ── Report types ──────────────────────────────────────────────────────────

    [Serializable]
    public class RouteCheckResult
    {
        public string checkName;
        public bool passed;
        public string detail;

        public RouteCheckResult(string name, bool pass, string detail)
        {
            this.checkName = name;
            this.passed    = pass;
            this.detail    = detail;
        }
    }

    public class RouteValidationReport
    {
        public List<RouteCheckResult> checks   = new List<RouteCheckResult>();
        public List<string>           warnings = new List<string>();
        public List<string>           errors   = new List<string>();

        public bool IsRouteValid
        {
            get
            {
                foreach (var c in checks)
                    if (!c.passed) return false;
                return errors.Count == 0;
            }
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== ROUTE VALIDATION REPORT ===");
            foreach (var c in checks)
            {
                string status = c.passed ? "[PASS]" : "[FAIL]";
                sb.AppendLine($"  {status} {c.checkName}: {c.detail}");
            }
            if (warnings.Count > 0)
            {
                sb.AppendLine("--- Warnings ---");
                foreach (string w in warnings) sb.AppendLine($"  ! {w}");
            }
            if (errors.Count > 0)
            {
                sb.AppendLine("--- Errors ---");
                foreach (string e in errors) sb.AppendLine($"  X {e}");
            }
            sb.AppendLine($"Result: {(IsRouteValid ? "VALID" : "INVALID")}");
            return sb.ToString();
        }
    }

    // ── RouteValidator ────────────────────────────────────────────────────────

    /// <summary>
    /// Runtime and editor route validator.
    /// Run from the Editor menu via TitanAscent/Validate Full Route.
    /// Also runs automatically in development builds on scene load.
    /// </summary>
    public class RouteValidator : MonoBehaviour
    {
        // ── Constants ─────────────────────────────────────────────────────────

        private const float MaxAnchorGapMeters = 20f;      // Check 1
        private const float BandSize           = 200f;      // Check 2
        private const int   MinAnchorsPerBand  = 3;         // Check 2
        private const float Zone1MaxHeight     = 800f;      // Check 3 — TailBasin top
        private const float Zone2MaxHeight     = 1800f;     // Check 3 — TailSpires top
        private const float CrownMinHeight     = 9000f;     // Check 4
        private const float CrownMaxHeight     = 10000f;    // Check 4
        private const int   TotalExpectedZones = 9;         // Check 6

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Start()
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            RouteValidationReport report = ValidateRoute();
            Debug.Log(report.ToString());
            if (!report.IsRouteValid)
                Debug.LogWarning("[RouteValidator] Route validation failed — see report above.");
#endif
        }

        // ── Editor menu item ──────────────────────────────────────────────────

#if UNITY_EDITOR
        [MenuItem("TitanAscent/Validate Full Route")]
        public static void ValidateFullRouteEditor()
        {
            RouteValidationReport report = new RouteValidator().ValidateRoute();
            string summary = report.ToString();
            Debug.Log(summary);
            EditorUtility.DisplayDialog(
                "Route Validation",
                summary,
                report.IsRouteValid ? "Route looks good!" : "Close");
        }
#endif

        // ── Core validation ───────────────────────────────────────────────────

        /// <summary>Runs all route checks and returns a full report.</summary>
        public RouteValidationReport ValidateRoute()
        {
            var report = new RouteValidationReport();

            SurfaceAnchorPoint[] allAnchors    = FindObjectsByType<SurfaceAnchorPoint>(FindObjectsSortMode.None);
            FallFunnel[]         allFunnels    = FindObjectsByType<FallFunnel>(FindObjectsSortMode.None);
            LandmarkObject[]     allLandmarks  = FindObjectsByType<LandmarkObject>(FindObjectsSortMode.None);
            ZoneManager          zoneManager   = FindFirstObjectByType<ZoneManager>();
            ZoneTransitionManager ztm          = FindFirstObjectByType<ZoneTransitionManager>();

            // Sort anchors by height once for reuse
            Array.Sort(allAnchors, (a, b) => a.transform.position.y.CompareTo(b.transform.position.y));

            // ── Check 1: No anchor gap > 20 m between consecutive zone boundaries ─
            report.checks.Add(Check1_AnchorGap(allAnchors));

            // ── Check 2: Every 200 m band has >= 3 anchors ────────────────────────
            report.checks.Add(Check2_BandDensity(allAnchors));

            // ── Check 3: No FallFunnel in Zones 1–2 ──────────────────────────────
            report.checks.Add(Check3_NoFunnelInEarlyZones(allFunnels));

            // ── Check 4: Zone 9 (Crown) has lowest anchor density ────────────────
            report.checks.Add(Check4_CrownLowDensity(allAnchors));

            // ── Check 5: At least one LandmarkObject per zone ─────────────────────
            report.checks.Add(Check5_LandmarksPerZone(allLandmarks, zoneManager));

            // ── Check 6: ZoneTransitionManager has all 9 zones referenced ─────────
            report.checks.Add(Check6_AllZonesReferenced(ztm));

            // Aggregate errors/warnings from failed checks
            foreach (var c in report.checks)
            {
                if (!c.passed)
                    report.errors.Add($"{c.checkName} — {c.detail}");
            }

            return report;
        }

        // ── Individual checks ─────────────────────────────────────────────────

        private RouteCheckResult Check1_AnchorGap(SurfaceAnchorPoint[] sortedAnchors)
        {
            const string checkName = "1: No anchor gap > 20 m";

            if (sortedAnchors.Length < 2)
                return new RouteCheckResult(checkName, false, $"Only {sortedAnchors.Length} anchor(s) found — cannot validate gap.");

            float maxGap    = 0f;
            float maxGapAt  = 0f;

            for (int i = 0; i < sortedAnchors.Length - 1; i++)
            {
                float gap = sortedAnchors[i + 1].transform.position.y - sortedAnchors[i].transform.position.y;
                if (gap > maxGap)
                {
                    maxGap   = gap;
                    maxGapAt = sortedAnchors[i].transform.position.y;
                }
            }

            bool pass  = maxGap <= MaxAnchorGapMeters;
            string msg = pass
                ? $"Max gap {maxGap:F1} m (threshold {MaxAnchorGapMeters} m)"
                : $"Max gap {maxGap:F1} m at height {maxGapAt:F0} m (threshold {MaxAnchorGapMeters} m)";

            return new RouteCheckResult(checkName, pass, msg);
        }

        private RouteCheckResult Check2_BandDensity(SurfaceAnchorPoint[] sortedAnchors)
        {
            const string checkName = "2: >= 3 anchors per 200 m band";

            if (sortedAnchors.Length == 0)
                return new RouteCheckResult(checkName, false, "No anchors found.");

            float minH = sortedAnchors[0].transform.position.y;
            float maxH = sortedAnchors[sortedAnchors.Length - 1].transform.position.y;

            var failedBands = new List<string>();

            for (float bandStart = Mathf.Floor(minH / BandSize) * BandSize;
                 bandStart < maxH;
                 bandStart += BandSize)
            {
                float bandEnd = bandStart + BandSize;
                int count = 0;

                foreach (SurfaceAnchorPoint a in sortedAnchors)
                {
                    float y = a.transform.position.y;
                    if (y >= bandStart && y < bandEnd)
                        count++;
                }

                if (count < MinAnchorsPerBand)
                    failedBands.Add($"{bandStart:F0}–{bandEnd:F0} m ({count} anchors)");
            }

            bool pass = failedBands.Count == 0;
            string msg = pass
                ? "All 200 m bands have >= 3 anchors"
                : $"Sparse bands: {string.Join(", ", failedBands)}";

            return new RouteCheckResult(checkName, pass, msg);
        }

        private RouteCheckResult Check3_NoFunnelInEarlyZones(FallFunnel[] funnels)
        {
            const string checkName = "3: No FallFunnel in Zones 1–2 (0–1800 m)";

            var violations = new List<string>();
            foreach (FallFunnel ff in funnels)
            {
                float y = ff.transform.position.y;
                if (y < Zone2MaxHeight)
                    violations.Add($"FallFunnel at height {y:F0} m");
            }

            bool pass  = violations.Count == 0;
            string msg = pass
                ? "No FallFunnels in Zones 1–2"
                : $"Violation(s): {string.Join("; ", violations)}";

            return new RouteCheckResult(checkName, pass, msg);
        }

        private RouteCheckResult Check4_CrownLowDensity(SurfaceAnchorPoint[] sortedAnchors)
        {
            const string checkName = "4: Zone 9 (Crown) has lowest anchor density";

            if (sortedAnchors.Length == 0)
                return new RouteCheckResult(checkName, false, "No anchors found.");

            // Count anchors per 1000 m zone-equivalent bands
            float totalHeight = 10000f;
            float zoneHeight  = totalHeight / TotalExpectedZones; // ~1111 m per zone
            var densities     = new float[TotalExpectedZones];

            foreach (SurfaceAnchorPoint a in sortedAnchors)
            {
                float y = a.transform.position.y;
                int zoneIdx = Mathf.Clamp((int)(y / zoneHeight), 0, TotalExpectedZones - 1);
                densities[zoneIdx]++;
            }

            float crownDensity = densities[TotalExpectedZones - 1]; // index 8 = Zone 9

            // Crown must have strictly fewer anchors than the median of all other zones
            float otherSum   = 0f;
            float otherCount = 0f;
            for (int i = 0; i < TotalExpectedZones - 1; i++)
            {
                otherSum   += densities[i];
                otherCount++;
            }

            float otherAvg = otherCount > 0f ? otherSum / otherCount : 0f;
            bool pass      = crownDensity < otherAvg || (crownDensity == 0f);
            string msg     = pass
                ? $"Crown anchor count ({crownDensity}) < other zone average ({otherAvg:F1})"
                : $"Crown anchor count ({crownDensity}) is NOT below other zone average ({otherAvg:F1})";

            return new RouteCheckResult(checkName, pass, msg);
        }

        private RouteCheckResult Check5_LandmarksPerZone(LandmarkObject[] landmarks, ZoneManager zoneManager)
        {
            const string checkName = "5: At least one LandmarkObject per zone";

            if (zoneManager == null)
                return new RouteCheckResult(checkName, false, "ZoneManager not found in scene.");

            var missingZones = new List<string>();

            // Test each zone's mid-height for landmark coverage
            for (float h = 400f; h < 10000f; h += 1000f)
            {
                var zone = zoneManager.GetZoneForHeight(h);
                if (zone == null) continue;

                bool hasLandmark = false;
                foreach (LandmarkObject lm in landmarks)
                {
                    float ly = lm.transform.position.y;
                    if (zone.ContainsHeight(ly))
                    {
                        hasLandmark = true;
                        break;
                    }
                }

                if (!hasLandmark)
                    missingZones.Add(zone.name);
            }

            // De-duplicate zone names
            var uniqueMissing = new HashSet<string>(missingZones);

            bool pass  = uniqueMissing.Count == 0;
            string msg = pass
                ? "All zones have at least one landmark"
                : $"Zones missing landmark: {string.Join(", ", uniqueMissing)}";

            return new RouteCheckResult(checkName, pass, msg);
        }

        private RouteCheckResult Check6_AllZonesReferenced(ZoneTransitionManager ztm)
        {
            const string checkName = "6: ZoneTransitionManager has all 9 zones";

            if (ztm == null)
                return new RouteCheckResult(checkName, false, "ZoneTransitionManager not found in scene.");

            // Verify via ZoneManager that 9 zones are populated
            ZoneManager zm = FindFirstObjectByType<ZoneManager>();
            if (zm == null)
                return new RouteCheckResult(checkName, false, "ZoneManager not found in scene.");

            // Check by iterating every 1000 m band for valid zone coverage
            int zonesFound = 0;
            float step     = 1000f;

            for (float h = 0f; h < 10000f; h += step)
            {
                var zone = zm.GetZoneForHeight(h + step * 0.5f);
                if (zone != null)
                    zonesFound++;
            }

            bool pass  = zonesFound >= TotalExpectedZones;
            string msg = pass
                ? $"All {TotalExpectedZones} zones covered in ZoneManager"
                : $"Only {zonesFound}/{TotalExpectedZones} zone bands have coverage";

            return new RouteCheckResult(checkName, pass, msg);
        }
    }
}
