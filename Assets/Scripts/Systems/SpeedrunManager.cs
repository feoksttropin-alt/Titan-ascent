using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace TitanAscent.Systems
{
    [Serializable]
    public class SpeedrunSplit
    {
        public string zoneName;
        public float  splitHeight;
        public float  personalBestTime;   // 0 = no PB yet
        public float  currentSplitTime;   // time when this split was crossed this run
        public bool   crossed;
    }

    public class SpeedrunManager : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        // Singleton
        // -----------------------------------------------------------------------

        public static SpeedrunManager Instance { get; private set; }

        // -----------------------------------------------------------------------
        // Split Definitions
        // -----------------------------------------------------------------------

        private static readonly (string name, float height)[] DefaultSplits =
        {
            ("TailBasin",       800f),
            ("TailSpires",      1800f),
            ("HindLegValley",   3000f),
            ("WingRoot",        4200f),
            ("SpineRidge",      5500f),
            ("TheGraveyard",    6500f),
            ("UpperBackStorm",  7800f),
            ("TheNeck",         9000f),
            ("TheCrown",        9999f),
            ("Summit",          10000f),
        };

        // -----------------------------------------------------------------------
        // Serialized UI References
        // -----------------------------------------------------------------------

        [Header("UI")]
        [SerializeField] private RectTransform splitListContainer;
        [SerializeField] private GameObject    splitRowPrefab;
        [SerializeField] private TextMeshProUGUI currentTimeText;

        [Header("Delta Colors")]
        [SerializeField] private Color aheadColor  = new Color(0.2f, 0.9f, 0.2f);
        [SerializeField] private Color behindColor = new Color(0.9f, 0.2f, 0.2f);
        [SerializeField] private Color noPBColor   = Color.white;

        // -----------------------------------------------------------------------
        // State
        // -----------------------------------------------------------------------

        private List<SpeedrunSplit> splits = new List<SpeedrunSplit>();
        private bool  isRunActive  = false;
        private bool  isPaused     = false;
        private float runStartTime = 0f;
        private float pausedAccum  = 0f;     // accumulated paused time
        private float pauseStartTime = 0f;

        private int   nextSplitIndex = 0;
        private FallTracker fallTracker;
        private SaveManager saveManager;

        // Split row TextMeshPro references for live update
        private readonly List<SplitRowUI> splitRows = new List<SplitRowUI>();

        public bool IsActive => isRunActive;

        // -----------------------------------------------------------------------
        // Nested helper to reference a spawned row's labels
        // -----------------------------------------------------------------------

        private class SplitRowUI
        {
            public TextMeshProUGUI nameLabel;
            public TextMeshProUGUI currentTimeLabel;
            public TextMeshProUGUI pbTimeLabel;
            public TextMeshProUGUI deltaLabel;
        }

        // -----------------------------------------------------------------------
        // Lifecycle
        // -----------------------------------------------------------------------

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            BuildSplits();
        }

        private void Start()
        {
            fallTracker = FindFirstObjectByType<FallTracker>();
            saveManager = FindFirstObjectByType<SaveManager>();

            if (fallTracker != null)
                fallTracker.OnNewHeightRecord.AddListener(HandleHeightUpdate);

            SpawnSplitRows();
        }

        private void OnDestroy()
        {
            if (fallTracker != null)
                fallTracker.OnNewHeightRecord.RemoveListener(HandleHeightUpdate);
        }

        private void Update()
        {
            if (!isRunActive || isPaused) return;

            float t = GetCurrentTime();
            if (currentTimeText != null)
                currentTimeText.text = GetFormattedTime(t);
        }

        // -----------------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------------

        public void StartSpeedrun()
        {
            LoadPBsFromSave();
            ResetAllCurrentTimes();

            runStartTime = Time.time;
            pausedAccum  = 0f;
            isPaused     = false;
            isRunActive  = true;
            nextSplitIndex = 0;

            RefreshAllRows();
            Debug.Log("[SpeedrunManager] Speedrun started.");
        }

        public void PauseTimer()
        {
            if (!isRunActive || isPaused) return;
            isPaused = true;
            pauseStartTime = Time.time;
        }

        public void ResumeTimer()
        {
            if (!isRunActive || !isPaused) return;
            pausedAccum += Time.time - pauseStartTime;
            isPaused = false;
        }

        public float GetCurrentTime()
        {
            if (!isRunActive) return 0f;
            float base_ = Time.time - runStartTime - pausedAccum;
            if (isPaused) base_ -= (Time.time - pauseStartTime);
            return Mathf.Max(0f, base_);
        }

        public string GetFormattedTime(float seconds)
        {
            int h   = Mathf.FloorToInt(seconds / 3600f);
            int m   = Mathf.FloorToInt((seconds % 3600f) / 60f);
            int s   = Mathf.FloorToInt(seconds % 60f);
            int ms  = Mathf.FloorToInt((seconds % 1f) * 1000f);
            return $"{h}:{m:00}:{s:00}.{ms:000}";
        }

        public float GetSplitDelta(int splitIndex)
        {
            if (splitIndex < 0 || splitIndex >= splits.Count) return 0f;
            var split = splits[splitIndex];
            if (split.personalBestTime <= 0f) return 0f;
            if (!split.crossed) return 0f;
            return split.currentSplitTime - split.personalBestTime;
        }

        public void EndSpeedrun(bool completed)
        {
            if (!isRunActive) return;
            isRunActive = false;

            if (completed)
            {
                SaveBeatenPBs();
                Debug.Log($"[SpeedrunManager] Run completed in {GetFormattedTime(GetCurrentTime())}");
            }
            else
            {
                Debug.Log("[SpeedrunManager] Run ended without completion.");
            }
        }

        // -----------------------------------------------------------------------
        // Height Update (split crossing)
        // -----------------------------------------------------------------------

        private void HandleHeightUpdate(float height)
        {
            if (!isRunActive) return;
            if (nextSplitIndex >= splits.Count) return;

            while (nextSplitIndex < splits.Count &&
                   height >= splits[nextSplitIndex].splitHeight)
            {
                CrossSplit(nextSplitIndex);
                nextSplitIndex++;
            }
        }

        private void CrossSplit(int index)
        {
            var split = splits[index];
            split.currentSplitTime = GetCurrentTime();
            split.crossed = true;

            float delta = GetSplitDelta(index);
            string deltaStr = FormatDelta(delta, split.personalBestTime);
            Debug.Log($"[SpeedrunManager] Split [{split.zoneName}]: {GetFormattedTime(split.currentSplitTime)} ({deltaStr})");

            UpdateSplitRow(index);
        }

        // -----------------------------------------------------------------------
        // Split Row UI
        // -----------------------------------------------------------------------

        private void SpawnSplitRows()
        {
            if (splitListContainer == null || splitRowPrefab == null) return;

            // Clear existing
            foreach (Transform child in splitListContainer)
                Destroy(child.gameObject);
            splitRows.Clear();

            foreach (var split in splits)
            {
                GameObject row = Instantiate(splitRowPrefab, splitListContainer);
                var ui = new SplitRowUI();

                // Attempt to find labels by name convention in the prefab
                ui.nameLabel        = FindLabel(row, "NameLabel");
                ui.currentTimeLabel = FindLabel(row, "CurrentTimeLabel");
                ui.pbTimeLabel      = FindLabel(row, "PBTimeLabel");
                ui.deltaLabel       = FindLabel(row, "DeltaLabel");

                if (ui.nameLabel != null) ui.nameLabel.text = split.zoneName;

                splitRows.Add(ui);
            }
        }

        private void RefreshAllRows()
        {
            for (int i = 0; i < splits.Count; i++)
                UpdateSplitRow(i);
        }

        private void UpdateSplitRow(int index)
        {
            if (index < 0 || index >= splitRows.Count) return;

            var split = splits[index];
            var ui    = splitRows[index];

            if (ui.currentTimeLabel != null)
                ui.currentTimeLabel.text = split.crossed ? GetFormattedTime(split.currentSplitTime) : "--:--.---";

            if (ui.pbTimeLabel != null)
                ui.pbTimeLabel.text = split.personalBestTime > 0f
                    ? GetFormattedTime(split.personalBestTime)
                    : "--:--.---";

            if (ui.deltaLabel != null)
            {
                float delta = GetSplitDelta(index);
                if (!split.crossed || split.personalBestTime <= 0f)
                {
                    ui.deltaLabel.text  = "";
                    ui.deltaLabel.color = noPBColor;
                }
                else
                {
                    ui.deltaLabel.text  = FormatDelta(delta, split.personalBestTime);
                    ui.deltaLabel.color = delta <= 0f ? aheadColor : behindColor;
                }
            }
        }

        // -----------------------------------------------------------------------
        // PB Persistence
        // -----------------------------------------------------------------------

        private void LoadPBsFromSave()
        {
            for (int i = 0; i < splits.Count; i++)
            {
                string key = $"SpeedrunSplit_PB_{i}_{splits[i].zoneName}";
                splits[i].personalBestTime = PlayerPrefs.GetFloat(key, 0f);
            }
        }

        private void SaveBeatenPBs()
        {
            bool anyBeaten = false;
            for (int i = 0; i < splits.Count; i++)
            {
                if (!splits[i].crossed) continue;
                float current = splits[i].currentSplitTime;
                float pb      = splits[i].personalBestTime;
                if (pb <= 0f || current < pb)
                {
                    splits[i].personalBestTime = current;
                    string key = $"SpeedrunSplit_PB_{i}_{splits[i].zoneName}";
                    PlayerPrefs.SetFloat(key, current);
                    anyBeaten = true;
                }
            }

            // Update the global speedrun PB using the summit split time
            if (splits.Count > 0 && splits[splits.Count - 1].crossed && saveManager != null)
                saveManager.UpdateSpeedrunPB(splits[splits.Count - 1].currentSplitTime);

            if (anyBeaten)
            {
                PlayerPrefs.Save();
                Debug.Log("[SpeedrunManager] New PBs saved.");
            }
        }

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        private void BuildSplits()
        {
            splits.Clear();
            foreach (var (name, height) in DefaultSplits)
            {
                splits.Add(new SpeedrunSplit
                {
                    zoneName  = name,
                    splitHeight = height,
                    personalBestTime = 0f,
                    currentSplitTime = 0f,
                    crossed   = false
                });
            }
        }

        private void ResetAllCurrentTimes()
        {
            foreach (var split in splits)
            {
                split.currentSplitTime = 0f;
                split.crossed = false;
            }
        }

        private string FormatDelta(float delta, float pb)
        {
            if (pb <= 0f) return "";
            string sign = delta <= 0f ? "-" : "+";
            float abs   = Mathf.Abs(delta);
            int m = Mathf.FloorToInt(abs / 60f);
            int s = Mathf.FloorToInt(abs % 60f);
            return m > 0 ? $"{sign}{m}:{s:00}" : $"{sign}0:{s:00}";
        }

        private static TextMeshProUGUI FindLabel(GameObject root, string childName)
        {
            Transform t = root.transform.Find(childName);
            return t != null ? t.GetComponent<TextMeshProUGUI>() : null;
        }
    }
}
