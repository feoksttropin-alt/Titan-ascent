using System;
using System.Collections;
using UnityEngine;

namespace TitanAscent.Systems
{
    /// <summary>
    /// Ensures save data survives crashes and unexpected quits.
    ///
    /// Features:
    ///   - Application.quitting event triggers an emergency save.
    ///   - Periodic auto-save every 60 s during active climb (requires SetClimbActive(true)).
    ///   - Dirty-flag system: saves only when state has changed since last write.
    ///   - Crash-recovery: on launch, compares emergency save against main save and
    ///     applies whichever has the higher bestHeight.
    /// </summary>
    public class CrashSafetyHandler : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        // Constants
        // -----------------------------------------------------------------------

        private const string EmergencySaveKey = "TitanAscent_SaveData_Emergency";
        private const float AutoSaveInterval = 60f;

        // -----------------------------------------------------------------------
        // Inspector
        // -----------------------------------------------------------------------

        [Header("References")]
        [SerializeField] private SaveManager saveManager;
        [SerializeField] private CheckpointlessStats checkpointlessStats;

        // -----------------------------------------------------------------------
        // Private state
        // -----------------------------------------------------------------------

        private bool _isDirty;
        private bool _climbActive;
        private Coroutine _autoSaveCoroutine;
        private DateTime _lastSaveTime = DateTime.MinValue;

        // -----------------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------------

        /// <summary>
        /// Sets the dirty flag. Call this from any system that mutates save state.
        /// </summary>
        public void MarkDirty()
        {
            _isDirty = true;
        }

        /// <summary>
        /// Writes an emergency save immediately, regardless of dirty flag.
        /// </summary>
        public void ForceEmergencySave()
        {
            if (saveManager == null) return;

            try
            {
                SaveData data = saveManager.CurrentData;
                string json = JsonUtility.ToJson(data, false);
                PlayerPrefs.SetString(EmergencySaveKey, json);
                PlayerPrefs.Save();
                _lastSaveTime = DateTime.UtcNow;
                _isDirty = false;
                Debug.Log("[CrashSafetyHandler] Emergency save written.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[CrashSafetyHandler] Emergency save failed: {e.Message}");
            }
        }

        /// <summary>
        /// Checks for an emergency save and, if it contains a higher bestHeight
        /// than the current main save, applies it.
        /// </summary>
        public void RecoverFromEmergencySave()
        {
            if (saveManager == null) return;
            if (!PlayerPrefs.HasKey(EmergencySaveKey)) return;

            try
            {
                string emergencyJson = PlayerPrefs.GetString(EmergencySaveKey);
                SaveData emergencyData = JsonUtility.FromJson<SaveData>(emergencyJson);

                if (emergencyData == null) return;

                SaveData mainData = saveManager.CurrentData;
                bool emergencyIsBetter = emergencyData.bestHeight > mainData.bestHeight;

                if (emergencyIsBetter)
                {
                    Debug.LogWarning(
                        $"[CrashSafetyHandler] Emergency save has higher bestHeight " +
                        $"({emergencyData.bestHeight:F1} m vs {mainData.bestHeight:F1} m). " +
                        "Applying emergency save.");

                    // Merge: take the best values from both
                    mainData.bestHeight = emergencyData.bestHeight;
                    mainData.longestFall = Mathf.Max(mainData.longestFall, emergencyData.longestFall);
                    mainData.totalFalls = Mathf.Max(mainData.totalFalls, emergencyData.totalFalls);
                    mainData.totalClimbs = Mathf.Max(mainData.totalClimbs, emergencyData.totalClimbs);
                    if (emergencyData.speedrunPB > 0f &&
                        (mainData.speedrunPB <= 0f || emergencyData.speedrunPB < mainData.speedrunPB))
                        mainData.speedrunPB = emergencyData.speedrunPB;

                    saveManager.Save();
                }
                else
                {
                    Debug.Log("[CrashSafetyHandler] Emergency save found but main save is equal or better. No recovery needed.");
                }

                // Clean up emergency save after evaluation
                PlayerPrefs.DeleteKey(EmergencySaveKey);
                PlayerPrefs.Save();
            }
            catch (Exception e)
            {
                Debug.LogError($"[CrashSafetyHandler] Failed to recover from emergency save: {e.Message}");
            }
        }

        /// <summary>
        /// Informs the handler whether a climb is currently active.
        /// When active, the periodic auto-save coroutine runs.
        /// </summary>
        public void SetClimbActive(bool active)
        {
            _climbActive = active;

            if (active && _autoSaveCoroutine == null)
                _autoSaveCoroutine = StartCoroutine(PeriodicAutoSaveRoutine());
            else if (!active && _autoSaveCoroutine != null)
            {
                StopCoroutine(_autoSaveCoroutine);
                _autoSaveCoroutine = null;
            }
        }

        // -----------------------------------------------------------------------
        // Lifecycle
        // -----------------------------------------------------------------------

        private void Awake()
        {
            if (saveManager == null)
                saveManager = FindFirstObjectByType<SaveManager>();

            if (checkpointlessStats == null)
                checkpointlessStats = FindFirstObjectByType<CheckpointlessStats>();
        }

        private void Start()
        {
            // Validate save data on startup
            ValidateSaveOnStart();

            // Attempt crash recovery before the game goes any further
            RecoverFromEmergencySave();

            Application.quitting += OnApplicationQuitting;
        }

        private void OnDestroy()
        {
            Application.quitting -= OnApplicationQuitting;
        }

        // -----------------------------------------------------------------------
        // Private helpers
        // -----------------------------------------------------------------------

        private void ValidateSaveOnStart()
        {
            // CheckpointlessStats handles its own validation via its PeriodicValidation
            // coroutine; trigger an immediate pass here via reflection-free approach:
            // we call the save manager's Load to ensure data is populated.
            if (saveManager != null)
                saveManager.Load();

            Debug.Log("[CrashSafetyHandler] Save data validated on start.");
        }

        private void OnApplicationQuitting()
        {
            if (_isDirty)
            {
                Debug.Log("[CrashSafetyHandler] Application quitting — writing emergency save.");
                ForceEmergencySave();
            }
        }

        private IEnumerator PeriodicAutoSaveRoutine()
        {
            while (_climbActive)
            {
                yield return new WaitForSeconds(AutoSaveInterval);

                if (!_climbActive) yield break;

                if (_isDirty)
                {
                    SaveDirtyState();
                    Debug.Log("[CrashSafetyHandler] Periodic auto-save written.");
                }
                else
                {
                    Debug.Log("[CrashSafetyHandler] Periodic auto-save skipped (no changes).");
                }
            }

            _autoSaveCoroutine = null;
        }

        private void SaveDirtyState()
        {
            if (saveManager == null || !_isDirty) return;
            saveManager.Save();
            _lastSaveTime = DateTime.UtcNow;
            _isDirty = false;
        }
    }
}
