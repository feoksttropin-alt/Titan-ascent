using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TitanAscent.Scene
{
    /// <summary>
    /// Scene setup helper for the Tail Basin to Tail Spires vertical slice (0-1800 m).
    ///
    /// <para>
    /// SetupSlice() — logs a full human-readable setup report describing what geometry,
    /// anchors, and events need to be placed for this slice to be complete.
    /// </para>
    /// <para>
    /// ValidateSlice() — checks that all required components exist in the scene.
    /// </para>
    ///
    /// Editor menu items:
    ///   TitanAscent / Setup Vertical Slice
    ///   TitanAscent / Validate Vertical Slice
    /// </summary>
    public class VerticalSliceSetup : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        // Data types
        // -----------------------------------------------------------------------

        [Serializable]
        public struct ClimbSection
        {
            public string sectionName;
            public float heightStart;
            public float heightEnd;
            public string surfaceType;          // e.g. "rock", "scale", "membrane"
            public float anchorDensity;         // anchors per 100 m
            public float windStrength;          // 0-1 normalised
            public bool hasFallRisk;
            public float fallRiskHeight;        // height within section of the fall risk trigger
            public float expectedFallDistance;  // metres the player is expected to fall
            public bool hasRecovery;
            public float recoveryHeight;        // height within section of recovery anchor
            public float landmarkAtHeight;      // world height of any landmark; 0 = none
            public string landmarkName;
        }

        // -----------------------------------------------------------------------
        // Slice definition
        // -----------------------------------------------------------------------

        private static readonly ClimbSection[] SliceSections = new ClimbSection[]
        {
            new ClimbSection
            {
                sectionName         = "Tail Basin Floor",
                heightStart         = 0f,    heightEnd         = 150f,
                surfaceType         = "rock",
                anchorDensity       = 4f,    windStrength      = 0.05f,
                hasFallRisk         = false, fallRiskHeight    = 0f,
                expectedFallDistance= 0f,    hasRecovery       = false,
                recoveryHeight      = 0f,    landmarkAtHeight  = 80f,
                landmarkName        = "Titan's First Scale"
            },
            new ClimbSection
            {
                sectionName         = "Lower Tail Ridge",
                heightStart         = 150f,  heightEnd         = 350f,
                surfaceType         = "scale",
                anchorDensity       = 5f,    windStrength      = 0.1f,
                hasFallRisk         = true,  fallRiskHeight    = 280f,
                expectedFallDistance= 60f,   hasRecovery       = true,
                recoveryHeight      = 240f,  landmarkAtHeight  = 0f,
                landmarkName        = ""
            },
            new ClimbSection
            {
                sectionName         = "Tail Basin Overhang",
                heightStart         = 350f,  heightEnd         = 500f,
                surfaceType         = "membrane",
                anchorDensity       = 6f,    windStrength      = 0.15f,
                hasFallRisk         = true,  fallRiskHeight    = 460f,
                expectedFallDistance= 80f,   hasRecovery       = true,
                recoveryHeight      = 420f,  landmarkAtHeight  = 490f,
                landmarkName        = "Membrane Spur"
            },
            new ClimbSection
            {
                sectionName         = "First Spire Base",
                heightStart         = 500f,  heightEnd         = 650f,
                surfaceType         = "rock",
                anchorDensity       = 5f,    windStrength      = 0.2f,
                hasFallRisk         = false, fallRiskHeight    = 0f,
                expectedFallDistance= 0f,    hasRecovery       = false,
                recoveryHeight      = 0f,    landmarkAtHeight  = 0f,
                landmarkName        = ""
            },
            new ClimbSection
            {
                sectionName         = "Spire Gap Crossing",
                heightStart         = 650f,  heightEnd         = 800f,
                surfaceType         = "scale",
                anchorDensity       = 7f,    windStrength      = 0.3f,
                hasFallRisk         = true,  fallRiskHeight    = 740f,
                expectedFallDistance= 120f,  hasRecovery       = true,
                recoveryHeight      = 680f,  landmarkAtHeight  = 800f,
                landmarkName        = "Spire Gap Vista"
            },
            new ClimbSection
            {
                sectionName         = "Mid Tail Spires",
                heightStart         = 800f,  heightEnd         = 1000f,
                surfaceType         = "scale",
                anchorDensity       = 6f,    windStrength      = 0.35f,
                hasFallRisk         = true,  fallRiskHeight    = 950f,
                expectedFallDistance= 100f,  hasRecovery       = true,
                recoveryHeight      = 900f,  landmarkAtHeight  = 0f,
                landmarkName        = ""
            },
            new ClimbSection
            {
                sectionName         = "Crystalline Outcrop",
                heightStart         = 1000f, heightEnd         = 1150f,
                surfaceType         = "crystal",
                anchorDensity       = 4f,    windStrength      = 0.4f,
                hasFallRisk         = false, fallRiskHeight    = 0f,
                expectedFallDistance= 0f,    hasRecovery       = false,
                recoveryHeight      = 0f,    landmarkAtHeight  = 1100f,
                landmarkName        = "Crystalline Arch"
            },
            new ClimbSection
            {
                sectionName         = "Upper Spire Chimney",
                heightStart         = 1150f, heightEnd         = 1300f,
                surfaceType         = "rock",
                anchorDensity       = 8f,    windStrength      = 0.45f,
                hasFallRisk         = true,  fallRiskHeight    = 1260f,
                expectedFallDistance= 150f,  hasRecovery       = true,
                recoveryHeight      = 1200f, landmarkAtHeight  = 0f,
                landmarkName        = ""
            },
            new ClimbSection
            {
                sectionName         = "Spine Transition Ledge",
                heightStart         = 1300f, heightEnd         = 1450f,
                surfaceType         = "membrane",
                anchorDensity       = 5f,    windStrength      = 0.5f,
                hasFallRisk         = true,  fallRiskHeight    = 1400f,
                expectedFallDistance= 90f,   hasRecovery       = true,
                recoveryHeight      = 1360f, landmarkAtHeight  = 1450f,
                landmarkName        = "Transition Gate"
            },
            new ClimbSection
            {
                sectionName         = "Tail Spires Crown",
                heightStart         = 1450f, heightEnd         = 1600f,
                surfaceType         = "scale",
                anchorDensity       = 6f,    windStrength      = 0.55f,
                hasFallRisk         = false, fallRiskHeight    = 0f,
                expectedFallDistance= 0f,    hasRecovery       = false,
                recoveryHeight      = 0f,    landmarkAtHeight  = 0f,
                landmarkName        = ""
            },
            new ClimbSection
            {
                sectionName         = "Storm Shelf Approach",
                heightStart         = 1600f, heightEnd         = 1750f,
                surfaceType         = "rock",
                anchorDensity       = 5f,    windStrength      = 0.7f,
                hasFallRisk         = true,  fallRiskHeight    = 1700f,
                expectedFallDistance= 200f,  hasRecovery       = true,
                recoveryHeight      = 1650f, landmarkAtHeight  = 1720f,
                landmarkName        = "Storm Shelf Marker"
            },
            new ClimbSection
            {
                sectionName         = "Vertical Slice Cap",
                heightStart         = 1750f, heightEnd         = 1800f,
                surfaceType         = "rock",
                anchorDensity       = 3f,    windStrength      = 0.75f,
                hasFallRisk         = false, fallRiskHeight    = 0f,
                expectedFallDistance= 0f,    hasRecovery       = false,
                recoveryHeight      = 0f,    landmarkAtHeight  = 1800f,
                landmarkName        = "Vertical Slice Summit"
            }
        };

        // -----------------------------------------------------------------------
        // SetupSlice
        // -----------------------------------------------------------------------

        /// <summary>
        /// Logs a full setup report to the console describing what needs to be placed.
        /// </summary>
        [ContextMenu("Setup Vertical Slice")]
        public void SetupSlice()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=================================================================");
            sb.AppendLine("  TITAN ASCENT — VERTICAL SLICE SETUP REPORT");
            sb.AppendLine("  Tail Basin → Tail Spires  (0 – 1800 m)");
            sb.AppendLine("=================================================================\n");

            foreach (ClimbSection s in SliceSections)
            {
                float sectionHeight = s.heightEnd - s.heightStart;
                int requiredAnchors = Mathf.CeilToInt(sectionHeight / 100f * s.anchorDensity);

                sb.AppendLine($"[{s.sectionName}]  {s.heightStart:F0} m – {s.heightEnd:F0} m");
                sb.AppendLine($"  Surface type   : {s.surfaceType}");
                sb.AppendLine($"  Anchor density : {s.anchorDensity} per 100 m  →  ~{requiredAnchors} anchors needed");
                sb.AppendLine($"  Wind strength  : {s.windStrength:P0}");

                if (s.hasFallRisk)
                {
                    sb.AppendLine($"  FALL RISK at   : {s.fallRiskHeight:F0} m  (expected drop: {s.expectedFallDistance:F0} m)");
                    if (s.hasRecovery)
                        sb.AppendLine($"  RECOVERY anchor: {s.recoveryHeight:F0} m");
                    else
                        sb.AppendLine("  No recovery anchor — player expected to hit bottom.");
                }

                if (s.landmarkAtHeight > 0f && !string.IsNullOrEmpty(s.landmarkName))
                    sb.AppendLine($"  LANDMARK       : '{s.landmarkName}' at {s.landmarkAtHeight:F0} m");

                sb.AppendLine();
            }

            sb.AppendLine("=== GEOMETRY CHECKLIST ===");
            sb.AppendLine("  [ ] Tail Basin base mesh (0-150 m): boulder field, open rock face");
            sb.AppendLine("  [ ] Lower Tail Ridge (150-350 m): scale protrusions for grapple, one overhanging ledge");
            sb.AppendLine("  [ ] Overhang membrane section (350-500 m): flexible/translucent membrane geometry");
            sb.AppendLine("  [ ] Spire cluster #1 (500-800 m): three narrow spires, gap crossing, wind vortex volume");
            sb.AppendLine("  [ ] Mid spires and crystal outcrop (800-1150 m): dense anchor-rich section with crystal arch");
            sb.AppendLine("  [ ] Chimney formation (1150-1300 m): enclosed vertical shaft, high anchor density");
            sb.AppendLine("  [ ] Spine transition zone (1300-1450 m): membrane-covered ledge sequence");
            sb.AppendLine("  [ ] Crown and storm shelf (1450-1800 m): open face, heavy wind zone, slice summit marker");
            sb.AppendLine();

            sb.AppendLine("=== REQUIRED COMPONENTS ===");
            sb.AppendLine("  [ ] SceneBootstrapper");
            sb.AppendLine("  [ ] FallFunnel (minimum 1, recommended 1 per fall-risk zone)");
            sb.AppendLine("  [ ] LandmarkObject for each landmark entry above");
            sb.AppendLine("  [ ] SurfaceAnchorPoints at densities described per section");
            sb.AppendLine("  [ ] ZoneManager with Zone data for Tail Basin (Z0) and Tail Spires (Z1)");
            sb.AppendLine("  [ ] WindSystem active above 500 m");
            sb.AppendLine("  [ ] AtmosphereController covering 0-1800 m");

            Debug.Log(sb.ToString());
        }

        // -----------------------------------------------------------------------
        // ValidateSlice
        // -----------------------------------------------------------------------

        /// <summary>
        /// Checks the open scene for required components and reports missing items.
        /// </summary>
        [ContextMenu("Validate Vertical Slice")]
        public void ValidateSlice()
        {
            List<string> missing = new List<string>();
            List<string> warnings = new List<string>();

            // Core systems (9 required)
            CheckRequired<Systems.GameManager>(missing, "GameManager");
            CheckRequired<Systems.FallTracker>(missing, "FallTracker");
            CheckRequired<Systems.NarrationSystem>(missing, "NarrationSystem");
            CheckRequired<Systems.JuiceController>(missing, "JuiceController");
            CheckRequired<Systems.SaveManager>(missing, "SaveManager");
            CheckRequired<Systems.ZoneManager>(missing, "ZoneManager");
            CheckRequired<Environment.WindSystem>(missing, "WindSystem");
            CheckRequired<Environment.AtmosphereController>(missing, "AtmosphereController");
            CheckRequired<SceneBootstrapper>(missing, "SceneBootstrapper");

            // FallFunnel — at least 1
            FallFunnel[] funnels = FindObjectsByType<FallFunnel>(FindObjectsSortMode.None);
            if (funnels.Length == 0)
                missing.Add("FallFunnel (at least 1 required)");
            else if (funnels.Length < 5)
                warnings.Add($"Only {funnels.Length} FallFunnel(s) found; 1 per fall-risk section recommended (5+).");

            // LandmarkObject — at least 1
            LandmarkObject[] landmarks = FindObjectsByType<LandmarkObject>(FindObjectsSortMode.None);
            if (landmarks.Length == 0)
                missing.Add("LandmarkObject (at least 1 required)");
            else
            {
                int expectedLandmarks = 0;
                foreach (var s in SliceSections)
                    if (s.landmarkAtHeight > 0f) expectedLandmarks++;

                if (landmarks.Length < expectedLandmarks)
                    warnings.Add($"Found {landmarks.Length} LandmarkObject(s); slice spec calls for {expectedLandmarks}.");
            }

            // SurfaceAnchorPoints — validate density approximately
            Environment.SurfaceAnchorPoint[] anchors = FindObjectsByType<Environment.SurfaceAnchorPoint>(FindObjectsSortMode.None);
            int totalRequiredAnchors = 0;
            foreach (var s in SliceSections)
                totalRequiredAnchors += Mathf.CeilToInt((s.heightEnd - s.heightStart) / 100f * s.anchorDensity);

            if (anchors.Length == 0)
                missing.Add("SurfaceAnchorPoint (none found)");
            else if (anchors.Length < totalRequiredAnchors / 2)
                warnings.Add($"Only {anchors.Length} SurfaceAnchorPoint(s) found; spec requires approximately {totalRequiredAnchors}.");

            // Report results
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=================================================================");
            sb.AppendLine("  TITAN ASCENT — VERTICAL SLICE VALIDATION REPORT");
            sb.AppendLine("=================================================================\n");

            if (missing.Count == 0 && warnings.Count == 0)
            {
                sb.AppendLine("  ALL CHECKS PASSED — slice is ready for playtesting.");
            }
            else
            {
                if (missing.Count > 0)
                {
                    sb.AppendLine($"  MISSING ({missing.Count}):");
                    foreach (string item in missing)
                        sb.AppendLine($"    [MISSING]  {item}");
                    sb.AppendLine();
                }

                if (warnings.Count > 0)
                {
                    sb.AppendLine($"  WARNINGS ({warnings.Count}):");
                    foreach (string w in warnings)
                        sb.AppendLine($"    [WARN]     {w}");
                }
            }

            sb.AppendLine("\n=================================================================");

            if (missing.Count > 0)
                Debug.LogError(sb.ToString());
            else
                Debug.LogWarning(sb.ToString());
        }

        // -----------------------------------------------------------------------
        // Editor menu items
        // -----------------------------------------------------------------------

#if UNITY_EDITOR
        [MenuItem("TitanAscent/Setup Vertical Slice")]
        private static void MenuSetupSlice()
        {
            VerticalSliceSetup helper = FindOrCreateHelper();
            helper.SetupSlice();
        }

        [MenuItem("TitanAscent/Validate Vertical Slice")]
        private static void MenuValidateSlice()
        {
            VerticalSliceSetup helper = FindOrCreateHelper();
            helper.ValidateSlice();
        }

        private static VerticalSliceSetup FindOrCreateHelper()
        {
            VerticalSliceSetup existing = FindFirstObjectByType<VerticalSliceSetup>();
            if (existing != null) return existing;

            GameObject go = new GameObject("VerticalSliceSetup");
            return go.AddComponent<VerticalSliceSetup>();
        }
#endif

        // -----------------------------------------------------------------------
        // Utility
        // -----------------------------------------------------------------------

        private static void CheckRequired<T>(List<string> missingList, string label) where T : Component
        {
            if (FindFirstObjectByType<T>() == null)
                missingList.Add(label);
        }
    }
}
