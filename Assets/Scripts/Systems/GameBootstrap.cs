using System;
using UnityEngine;
using TitanAscent.Audio;
using TitanAscent.UI;
using TitanAscent.Input;

namespace TitanAscent.Systems
{
    /// <summary>
    /// Runs before all other MonoBehaviours. Creates the persistent BOOT GameObject and
    /// ensures every singleton is present in the scene. Fires OnBootComplete when done.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class GameBootstrap : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        // Static event
        // -----------------------------------------------------------------------

        public static event Action OnBootComplete;

        // -----------------------------------------------------------------------
        // Singleton
        // -----------------------------------------------------------------------

        public static GameBootstrap Instance { get; private set; }

        // -----------------------------------------------------------------------
        // Lifecycle
        // -----------------------------------------------------------------------

        private void Awake()
        {
            // Only one BOOT instance across all scenes
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // ── Ensure every singleton exists ──────────────────────────────────
            EnsureComponent<GameManager>();
            EnsureComponent<AudioManager>();

            // MusicManager is optional — only add if the type exists in the project
            MusicManager mm = FindFirstObjectByType<MusicManager>();
            if (mm == null)
            {
                // AddComponent only when the class is compiled in (it is — but guard anyway)
                GetOrAdd<MusicManager>();
            }

            SaveManager sm = EnsureComponent<SaveManager>();
            sm.Load();                   // Load immediately after creation

            EnsureComponent<InputHandler>();
            EnsureComponent<BuildVersionManager>();

            // ── Hook application log to PlaytestLogger ─────────────────────────
            Application.logMessageReceived += OnLogMessage;

            // ── Signal boot complete ───────────────────────────────────────────
            OnBootComplete?.Invoke();
            Debug.Log("[GameBootstrap] Boot complete.");
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= OnLogMessage;
        }

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        /// <summary>
        /// Returns the first instance of T found in the scene, or adds one to this
        /// GameObject if none exists.
        /// </summary>
        private T EnsureComponent<T>() where T : Component
        {
            T found = FindFirstObjectByType<T>();
            if (found != null) return found;
            return gameObject.AddComponent<T>();
        }

        private T GetOrAdd<T>() where T : Component
        {
            T found = GetComponent<T>();
            return found != null ? found : gameObject.AddComponent<T>();
        }

        // -----------------------------------------------------------------------
        // Log interceptor
        // -----------------------------------------------------------------------

        private void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception) return;

            PlaytestLogger logger = PlaytestLogger.Instance;
            if (logger == null) return;

            // Log as additional data in the current playtest session
            string extra = $"[{type}] {condition}";
            if (!string.IsNullOrEmpty(stackTrace))
                extra += $"\n{stackTrace.Split('\n')[0]}"; // first line only

            logger.LogEvent(LogEventType.SessionEnd, 0f, 0, extra);
        }

        // -----------------------------------------------------------------------
        // Editor helper: create the BOOT object in a scene if it is missing
        // -----------------------------------------------------------------------

#if UNITY_EDITOR
        [UnityEditor.MenuItem("TitanAscent/Create Boot Object")]
        private static void CreateBootObject()
        {
            GameBootstrap existing = FindFirstObjectByType<GameBootstrap>();
            if (existing != null)
            {
                Debug.Log("[GameBootstrap] BOOT object already exists in scene.");
                return;
            }

            GameObject go = new GameObject("BOOT");
            go.AddComponent<GameBootstrap>();
            UnityEditor.EditorUtility.SetDirty(go);
            Debug.Log("[GameBootstrap] BOOT object created.");
        }
#endif
    }
}
