using System.Collections.Generic;
using System.Text;
using UnityEngine;
using TitanAscent.Grapple;
using TitanAscent.Environment;
using TitanAscent.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TitanAscent.Systems
{
    // -----------------------------------------------------------------------
    // Result Types
    // -----------------------------------------------------------------------

    public enum QAStatus
    {
        Pass,
        Warning,
        Error,
        Info
    }

    public class QAResult
    {
        public string   checkName;
        public QAStatus status;
        public string   message;

        public QAResult(string name, QAStatus s, string msg)
        {
            checkName = name;
            status    = s;
            message   = msg;
        }
    }

    // -----------------------------------------------------------------------
    // QAValidator
    // -----------------------------------------------------------------------

    /// <summary>
    /// Runtime QA check system. Runs a suite of validation checks and logs results.
    ///
    /// In the Editor, invoke via menu: TitanAscent / Run QA Validation.
    /// In Dev builds, automatically runs on game start.
    /// </summary>
    public class QAValidator : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        // Auto-run in dev builds
        // -----------------------------------------------------------------------

        private void Start()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var results = RunAllChecks();
            LogResultsToConsole(results);
#endif
        }

        // -----------------------------------------------------------------------
        // Editor Menu Item
        // -----------------------------------------------------------------------

#if UNITY_EDITOR
        [MenuItem("TitanAscent/Run QA Validation")]
        public static void RunFromMenu()
        {
            var validator = FindFirstObjectByType<QAValidator>();
            if (validator == null)
            {
                // Create a temporary instance for editor-only execution
                var go = new GameObject("_QAValidator_Temp");
                validator = go.AddComponent<QAValidator>();
            }

            var results = validator.RunAllChecks();
            LogResultsToConsole(results);
            ShowEditorDialog(results);

            // Clean up temporary object
            if (validator.gameObject.name == "_QAValidator_Temp")
                DestroyImmediate(validator.gameObject);
        }

        private static void ShowEditorDialog(List<QAResult> results)
        {
            int pass = 0, warn = 0, error = 0;
            var sb = new StringBuilder();

            foreach (var r in results)
            {
                switch (r.status)
                {
                    case QAStatus.Pass:    pass++;  break;
                    case QAStatus.Warning: warn++;  break;
                    case QAStatus.Error:   error++; break;
                }
                string prefix = r.status == QAStatus.Pass    ? "[PASS] "
                              : r.status == QAStatus.Warning ? "[WARN] "
                              : r.status == QAStatus.Error   ? "[ERR]  "
                              :                                "[INFO] ";
                sb.AppendLine($"{prefix}{r.checkName}: {r.message}");
            }

            string title   = $"QA Results — {pass} passed, {warn} warnings, {error} errors";
            string message = sb.ToString();
            EditorUtility.DisplayDialog(title, message.Length > 0 ? message : "No checks ran.", "OK");
        }
#endif

        // -----------------------------------------------------------------------
        // Core Check Runner
        // -----------------------------------------------------------------------

        /// <summary>Runs all QA checks and returns the full result list.</summary>
        public List<QAResult> RunAllChecks()
        {
            var results = new List<QAResult>();

            results.Add(CheckSceneBootstrapper());
            results.Add(CheckGameManager());
            results.Add(CheckFallTrackerRigidbody());
            results.Add(CheckGrappleController());
            results.Add(CheckRopeSimulator());
            results.Add(CheckAudioManager());
            results.Add(CheckNarrationSystem());
            results.Add(CheckHUDController());
            results.AddRange(CheckSurfaceAnchorPoints());
            results.Add(CheckSaveManager());
            results.Add(CheckZoneManagerZoneCount());
            results.AddRange(CheckMissingScripts());
            results.Add(CheckPhysicsGravity());
            results.Add(CheckPlayerTag());

            int pass = 0, warn = 0, error = 0;
            foreach (var r in results)
            {
                switch (r.status)
                {
                    case QAStatus.Pass:    pass++;  break;
                    case QAStatus.Warning: warn++;  break;
                    case QAStatus.Error:   error++; break;
                }
            }

            Debug.Log($"[QAValidator] {pass} passed, {warn} warnings, {error} errors.");
            return results;
        }

        // -----------------------------------------------------------------------
        // Individual Checks
        // -----------------------------------------------------------------------

        private QAResult CheckSceneBootstrapper()
        {
            const string name = "SceneBootstrapper present";
            var obj = FindFirstObjectByType<Scene.SceneBootstrapper>();
            return obj != null
                ? Pass(name, "Found in scene.")
                : Error(name, "No SceneBootstrapper found in scene. Add one to a root GameObject.");
        }

        private QAResult CheckGameManager()
        {
            const string name = "GameManager instance";
            return GameManager.Instance != null
                ? Pass(name, "Instance is valid.")
                : Error(name, "GameManager.Instance is null. Ensure GameManager is present and Awake has run.");
        }

        private QAResult CheckFallTrackerRigidbody()
        {
            const string name = "FallTracker Rigidbody reference";
            var ft = FindFirstObjectByType<FallTracker>();
            if (ft == null) return Error(name, "FallTracker not found in scene.");

            var rb = ft.GetComponent<Rigidbody>();
            return rb != null
                ? Pass(name, "Rigidbody is attached to FallTracker GameObject.")
                : Error(name, "FallTracker exists but has no Rigidbody on the same GameObject.");
        }

        private QAResult CheckGrappleController()
        {
            const string name = "GrappleController on player";
            var gc = FindFirstObjectByType<GrappleController>();
            return gc != null
                ? Pass(name, "GrappleController found.")
                : Error(name, "No GrappleController found. Ensure it is on the player GameObject.");
        }

        private QAResult CheckRopeSimulator()
        {
            const string name = "RopeSimulator present";
            var rs = FindFirstObjectByType<RopeSimulator>();
            return rs != null
                ? Pass(name, "RopeSimulator found.")
                : Error(name, "No RopeSimulator found in scene.");
        }

        private QAResult CheckAudioManager()
        {
            const string name = "AudioManager instance";
            var am = FindFirstObjectByType<Audio.AudioManager>();
            return am != null
                ? Pass(name, "AudioManager found.")
                : Warn(name, "AudioManager not found. Audio will be silent.");
        }

        private QAResult CheckNarrationSystem()
        {
            const string name = "NarrationSystem present";
            var ns = FindFirstObjectByType<NarrationSystem>();
            return ns != null
                ? Pass(name, "NarrationSystem found.")
                : Warn(name, "NarrationSystem not found. Narration lines will not play.");
        }

        private QAResult CheckHUDController()
        {
            const string name = "HUDController present";
            var hud = FindFirstObjectByType<HUDController>();
            return hud != null
                ? Pass(name, "HUDController found.")
                : Warn(name, "HUDController not found. HUD elements will not update.");
        }

        private List<QAResult> CheckSurfaceAnchorPoints()
        {
            var results = new List<QAResult>();
            var anchors = FindObjectsByType<SurfaceAnchorPoint>(FindObjectsSortMode.None);

            if (anchors.Length == 0)
            {
                results.Add(Info("SurfaceAnchorPoints", "No SurfaceAnchorPoints found in scene."));
                return results;
            }

            foreach (var anchor in anchors)
            {
                if (anchor.AnchorSurfaceType == SurfaceType.None)
                {
                    results.Add(Warn(
                        $"SurfaceAnchorPoint '{anchor.name}'",
                        $"SurfaceType is None on '{anchor.gameObject.name}'. Assign a valid SurfaceType."));
                }
            }

            if (results.Count == 0)
                results.Add(Pass("SurfaceAnchorPoints", $"All {anchors.Length} anchor(s) have valid SurfaceTypes."));

            return results;
        }

        private QAResult CheckSaveManager()
        {
            const string name = "SaveManager write/read test";
            const string testKey = "QAValidator_TestKey_a3f7";

            try
            {
                PlayerPrefs.SetString(testKey, "qa_ok");
                PlayerPrefs.Save();
                string read = PlayerPrefs.GetString(testKey, "");
                PlayerPrefs.DeleteKey(testKey);
                PlayerPrefs.Save();

                return read == "qa_ok"
                    ? Pass(name, "Write/read cycle succeeded.")
                    : Error(name, "Read value did not match written value.");
            }
            catch (System.Exception e)
            {
                return Error(name, $"Exception during write/read test: {e.Message}");
            }
        }

        private QAResult CheckZoneManagerZoneCount()
        {
            const string name = "ZoneManager zone count";
            const int    expectedZones = 9;

            var zm = FindFirstObjectByType<ZoneManager>();
            if (zm == null)
                return Error(name, "ZoneManager not found in scene.");

            // Probe all height positions and count unique zone names
            var seen = new System.Collections.Generic.HashSet<string>();
            for (int h = 0; h <= 10000; h += 50)
            {
                var zone = zm.GetZoneForHeight(h);
                if (zone != null) seen.Add(zone.name);
            }

            int zoneCount = seen.Count;
            return zoneCount == expectedZones
                ? Pass(name, $"Exactly {expectedZones} zones configured.")
                : Error(name, $"Expected {expectedZones} zones, detected approximately {zoneCount} unique zones. Verify ZoneManager configuration.");
        }

        private List<QAResult> CheckMissingScripts()
        {
            var results     = new List<QAResult>();
            var rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();

            foreach (var go in rootObjects)
            {
                var components = go.GetComponentsInChildren<Component>(true);
                foreach (var c in components)
                {
                    if (c == null)
                    {
                        results.Add(Error(
                            "Missing Script",
                            $"Missing script component detected on '{go.name}' or one of its children."));
                    }
                }
            }

            if (results.Count == 0)
                results.Add(Pass("Missing Scripts", "No missing script components found on root GameObjects."));

            return results;
        }

        private QAResult CheckPhysicsGravity()
        {
            const string name = "Physics.gravity (tuned)";
            Vector3 unityDefault = new Vector3(0f, -9.81f, 0f);

            if (Vector3.Distance(Physics.gravity, unityDefault) < 0.001f)
                return Info(name, $"Physics.gravity is default Unity ({Physics.gravity}). Titan Ascent expects tuned gravity (e.g. y ≈ -17.6 for gravityScale 1.8).");

            return Pass(name, $"Physics.gravity is {Physics.gravity} — non-default, as expected.");
        }

        private QAResult CheckPlayerTag()
        {
            const string name = "Player tag set";
            var player = GameObject.FindWithTag("Player");
            return player != null
                ? Pass(name, $"Object with tag 'Player' found: '{player.name}'.")
                : Error(name, "No GameObject with tag 'Player' found. Tag your player prefab root with 'Player'.");
        }

        // -----------------------------------------------------------------------
        // Factory helpers
        // -----------------------------------------------------------------------

        private static QAResult Pass(string n, string m)  => new QAResult(n, QAStatus.Pass,    m);
        private static QAResult Warn(string n, string m)  => new QAResult(n, QAStatus.Warning, m);
        private static QAResult Error(string n, string m) => new QAResult(n, QAStatus.Error,   m);
        private static QAResult Info(string n, string m)  => new QAResult(n, QAStatus.Info,    m);

        // -----------------------------------------------------------------------
        // Logging
        // -----------------------------------------------------------------------

        private static void LogResultsToConsole(List<QAResult> results)
        {
            foreach (var r in results)
            {
                string msg = $"[QAValidator] [{r.status}] {r.checkName}: {r.message}";
                switch (r.status)
                {
                    case QAStatus.Error:   Debug.LogError(msg);   break;
                    case QAStatus.Warning: Debug.LogWarning(msg); break;
                    default:               Debug.Log(msg);        break;
                }
            }
        }
    }
}
