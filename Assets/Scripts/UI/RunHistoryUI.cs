using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TitanAscent.UI
{
    // ------------------------------------------------------------------
    // RunRecord — add to SaveManager.SaveData.runHistory
    // ------------------------------------------------------------------

    [Serializable]
    public class RunRecord
    {
        public string runDate;           // "yyyy-MM-dd HH:mm"
        public float  maxHeight;
        public int    totalFalls;
        public float  longestFall;
        public float  durationSeconds;
        public string modeType;          // "Normal", "Speedrun", "Challenge", etc.
        public bool   reached;           // true if summit was reached
    }

    /// <summary>
    /// Displays the player's past run history in a scrollable UI list.
    /// Supports sorting by date, height, or falls. Shows aggregate stats
    /// in a header. Up to 20 records are kept.
    /// </summary>
    public class RunHistoryUI : MonoBehaviour
    {
        // ------------------------------------------------------------------
        // Inspector
        // ------------------------------------------------------------------

        [Header("Layout")]
        [SerializeField] private RectTransform listContainer;
        [SerializeField] private GameObject    rowPrefab;      // Assign in Inspector

        [Header("Sort Buttons")]
        [SerializeField] private Button sortByDateButton;
        [SerializeField] private Button sortByHeightButton;
        [SerializeField] private Button sortByFallsButton;

        [Header("Aggregate Stats")]
        [SerializeField] private Text totalRunsText;
        [SerializeField] private Text averageHeightText;
        [SerializeField] private Text totalFallsText;
        [SerializeField] private Text summitCountText;

        [Header("Actions")]
        [SerializeField] private Button      clearHistoryButton;
        [SerializeField] private GameObject  confirmClearPanel;
        [SerializeField] private Button      confirmYesButton;
        [SerializeField] private Button      confirmNoButton;

        // ------------------------------------------------------------------
        // Constants
        // ------------------------------------------------------------------

        private const int MaxRecords = 20;

        // ------------------------------------------------------------------
        // Private state
        // ------------------------------------------------------------------

        private enum SortMode { Date, Height, Falls }

        private SortMode    currentSort = SortMode.Date;
        private bool        sortDescending = true;

        private Systems.SaveManager saveManager;
        private List<RunRecord>     records = new List<RunRecord>();
        private readonly List<GameObject> spawnedRows = new List<GameObject>();

        // ------------------------------------------------------------------
        // Lifecycle
        // ------------------------------------------------------------------

        private void Awake()
        {
            saveManager = FindFirstObjectByType<Systems.SaveManager>();
        }

        private void OnEnable()
        {
            // Wire buttons
            if (sortByDateButton   != null) sortByDateButton.onClick.AddListener(SortByDate);
            if (sortByHeightButton != null) sortByHeightButton.onClick.AddListener(SortByHeight);
            if (sortByFallsButton  != null) sortByFallsButton.onClick.AddListener(SortByFalls);

            if (clearHistoryButton != null) clearHistoryButton.onClick.AddListener(OnClearHistoryClicked);
            if (confirmYesButton   != null) confirmYesButton.onClick.AddListener(ConfirmClear);
            if (confirmNoButton    != null) confirmNoButton.onClick.AddListener(CancelClear);

            if (confirmClearPanel != null)
                confirmClearPanel.SetActive(false);

            RefreshFromSave();
        }

        private void OnDisable()
        {
            if (sortByDateButton   != null) sortByDateButton.onClick.RemoveListener(SortByDate);
            if (sortByHeightButton != null) sortByHeightButton.onClick.RemoveListener(SortByHeight);
            if (sortByFallsButton  != null) sortByFallsButton.onClick.RemoveListener(SortByFalls);
            if (clearHistoryButton != null) clearHistoryButton.onClick.RemoveListener(OnClearHistoryClicked);
            if (confirmYesButton   != null) confirmYesButton.onClick.RemoveListener(ConfirmClear);
            if (confirmNoButton    != null) confirmNoButton.onClick.RemoveListener(CancelClear);
        }

        // ------------------------------------------------------------------
        // Public API
        // ------------------------------------------------------------------

        /// <summary>
        /// Re-reads run history from SaveManager and rebuilds the UI list.
        /// Call this whenever the run history panel becomes visible.
        /// </summary>
        public void Refresh()
        {
            RefreshFromSave();
        }

        /// <summary>Add a run record and persist it (called at end of each run).</summary>
        public void AddRecord(RunRecord record)
        {
            if (saveManager == null) return;

            List<RunRecord> history = GetHistory();
            history.Insert(0, record);

            if (history.Count > MaxRecords)
                history.RemoveRange(MaxRecords, history.Count - MaxRecords);

            SaveHistory(history);
            RefreshFromSave();
        }

        // ------------------------------------------------------------------
        // Sort callbacks
        // ------------------------------------------------------------------

        private void SortByDate()
        {
            if (currentSort == SortMode.Date) sortDescending = !sortDescending;
            else { currentSort = SortMode.Date; sortDescending = true; }
            RebuildList();
        }

        private void SortByHeight()
        {
            if (currentSort == SortMode.Height) sortDescending = !sortDescending;
            else { currentSort = SortMode.Height; sortDescending = true; }
            RebuildList();
        }

        private void SortByFalls()
        {
            if (currentSort == SortMode.Falls) sortDescending = !sortDescending;
            else { currentSort = SortMode.Falls; sortDescending = true; }
            RebuildList();
        }

        // ------------------------------------------------------------------
        // Clear history
        // ------------------------------------------------------------------

        private void OnClearHistoryClicked()
        {
            if (confirmClearPanel != null)
                confirmClearPanel.SetActive(true);
        }

        private void ConfirmClear()
        {
            if (saveManager == null) return;
            saveManager.CurrentData.runHistory = new List<RunRecord>();
            saveManager.Save();
            records.Clear();
            RebuildList();
            UpdateAggregateStats();

            if (confirmClearPanel != null)
                confirmClearPanel.SetActive(false);
        }

        private void CancelClear()
        {
            if (confirmClearPanel != null)
                confirmClearPanel.SetActive(false);
        }

        // ------------------------------------------------------------------
        // Refresh & build
        // ------------------------------------------------------------------

        private void RefreshFromSave()
        {
            records = new List<RunRecord>(GetHistory());
            RebuildList();
            UpdateAggregateStats();
        }

        private void RebuildList()
        {
            // Destroy existing rows
            foreach (GameObject row in spawnedRows)
                Destroy(row);
            spawnedRows.Clear();

            if (listContainer == null || rowPrefab == null) return;

            // Sort a copy
            List<RunRecord> sorted = new List<RunRecord>(records);
            SortList(sorted);

            foreach (RunRecord r in sorted)
            {
                GameObject go = Instantiate(rowPrefab, listContainer);
                go.SetActive(true);
                PopulateRow(go, r);
                spawnedRows.Add(go);
            }
        }

        private void PopulateRow(GameObject row, RunRecord r)
        {
            // Expects child Texts named: Date, Height, Falls, Time, ModeBadge
            SetChildText(row, "Date",      r.runDate);
            SetChildText(row, "Height",    FormatHeight(r));
            SetChildText(row, "Falls",     r.totalFalls.ToString());
            SetChildText(row, "Time",      FormatTime(r.durationSeconds));
            SetChildText(row, "ModeBadge", r.modeType ?? "Normal");

            // Tint height gold if summit was reached
            Transform heightTf = row.transform.Find("Height");
            if (heightTf != null)
            {
                Text t = heightTf.GetComponent<Text>();
                if (t != null) t.color = r.reached ? new Color(1f, 0.84f, 0f) : Color.white;
            }
        }

        private void UpdateAggregateStats()
        {
            int   total       = records.Count;
            float avgHeight   = 0f;
            int   totalFalls  = 0;
            int   summits     = 0;

            foreach (RunRecord r in records)
            {
                avgHeight  += r.maxHeight;
                totalFalls += r.totalFalls;
                if (r.reached) summits++;
            }

            if (total > 0) avgHeight /= total;

            if (totalRunsText    != null) totalRunsText.text    = $"Runs: {total}";
            if (averageHeightText!= null) averageHeightText.text= $"Avg Height: {avgHeight:F0}m";
            if (totalFallsText   != null) totalFallsText.text   = $"Total Falls: {totalFalls}";
            if (summitCountText  != null) summitCountText.text  = $"Summits: {summits}";
        }

        // ------------------------------------------------------------------
        // Sorting helpers
        // ------------------------------------------------------------------

        private void SortList(List<RunRecord> list)
        {
            switch (currentSort)
            {
                case SortMode.Date:
                    list.Sort((a, b) =>
                        sortDescending
                            ? string.Compare(b.runDate, a.runDate, StringComparison.Ordinal)
                            : string.Compare(a.runDate, b.runDate, StringComparison.Ordinal));
                    break;

                case SortMode.Height:
                    list.Sort((a, b) =>
                        sortDescending
                            ? b.maxHeight.CompareTo(a.maxHeight)
                            : a.maxHeight.CompareTo(b.maxHeight));
                    break;

                case SortMode.Falls:
                    list.Sort((a, b) =>
                        sortDescending
                            ? b.totalFalls.CompareTo(a.totalFalls)
                            : a.totalFalls.CompareTo(b.totalFalls));
                    break;
            }
        }

        // ------------------------------------------------------------------
        // Save helpers
        // ------------------------------------------------------------------

        private List<RunRecord> GetHistory()
        {
            if (saveManager == null) return new List<RunRecord>();
            if (saveManager.CurrentData.runHistory == null)
                saveManager.CurrentData.runHistory = new List<RunRecord>();
            return saveManager.CurrentData.runHistory;
        }

        private void SaveHistory(List<RunRecord> history)
        {
            if (saveManager == null) return;
            saveManager.CurrentData.runHistory = history;
            saveManager.Save();
        }

        // ------------------------------------------------------------------
        // Formatting helpers
        // ------------------------------------------------------------------

        private static string FormatHeight(RunRecord r)
        {
            string suffix = r.reached ? " (SUMMIT)" : "m";
            return $"{r.maxHeight:F0}{suffix}";
        }

        private static string FormatTime(float seconds)
        {
            int m = (int)(seconds / 60);
            int s = (int)(seconds % 60);
            return $"{m}:{s:D2}";
        }

        private static void SetChildText(GameObject parent, string childName, string value)
        {
            Transform tf = parent.transform.Find(childName);
            if (tf == null) return;
            Text t = tf.GetComponent<Text>();
            if (t != null) t.text = value;
        }
    }
}
