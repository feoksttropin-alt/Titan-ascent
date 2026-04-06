using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TitanAscent.Systems
{
    /// <summary>
    /// Live speedrun split display. Only visible when SpeedrunManager.IsActive.
    /// Positioned on the right edge of the screen as a vertical list of all 10 splits.
    /// Each row shows: zone name, split time, delta vs PB, highlighted current split.
    /// Subscribes to SpeedrunManager split events via polling (SpeedrunManager doesn't
    /// expose C# events directly, so we poll each frame while active).
    /// </summary>
    public class SpeedrunSplitDisplay : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        // Inspector
        // -----------------------------------------------------------------------

        [Header("References")]
        [SerializeField] private RectTransform    splitRowsContainer;
        [SerializeField] private TextMeshProUGUI  totalTimeText;

        [Header("Row Prefab / Style")]
        [SerializeField] private GameObject splitRowPrefab;

        [Header("Colors")]
        [SerializeField] private Color aheadColor    = new Color(0.2f, 0.9f, 0.2f, 1f);
        [SerializeField] private Color behindColor   = new Color(0.9f, 0.2f, 0.2f, 1f);
        [SerializeField] private Color noPBColor     = Color.white;
        [SerializeField] private Color uncrossedColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        [SerializeField] private Color activeRowBg   = new Color(1f, 1f, 1f, 0.15f);

        [Header("Animation")]
        [SerializeField] private float flashDuration = 0.4f;
        [SerializeField] private float pulsePeriod   = 1.2f;

        // -----------------------------------------------------------------------
        // Private state
        // -----------------------------------------------------------------------

        private SpeedrunManager _manager;
        private bool            _wasActive       = false;
        private int             _lastNextSplit    = 0;

        // Row references — built once
        private readonly List<SplitRow> _rows = new List<SplitRow>();

        private float _pulseTimer = 0f;

        // -----------------------------------------------------------------------
        // Nested row helper
        // -----------------------------------------------------------------------

        private class SplitRow
        {
            public GameObject     root;
            public Image          background;
            public TextMeshProUGUI nameLabel;
            public TextMeshProUGUI timeLabel;
            public TextMeshProUGUI deltaLabel;

            // Animation state
            public bool  isFlashing;
            public float flashTimer;
            public Color targetColor;
        }

        // -----------------------------------------------------------------------
        // Lifecycle
        // -----------------------------------------------------------------------

        private void Start()
        {
            _manager = SpeedrunManager.Instance != null
                ? SpeedrunManager.Instance
                : FindFirstObjectByType<SpeedrunManager>();

            BuildRows();
            SetContainerVisible(false);
        }

        private void Update()
        {
            if (_manager == null) return;

            bool nowActive = _manager.IsActive;

            // Show / hide container when run starts / ends
            if (nowActive != _wasActive)
            {
                _wasActive = nowActive;
                SetContainerVisible(nowActive);
                if (nowActive)
                {
                    _lastNextSplit = 0;
                    RefreshAllRows();
                }
            }

            if (!nowActive) return;

            // Update total time text
            if (totalTimeText != null)
                totalTimeText.text = "Total: " + FormatLong(_manager.GetCurrentTime());

            // Detect newly crossed splits by comparing next split index
            int nextSplit = GetNextSplitIndex();
            if (nextSplit != _lastNextSplit)
            {
                // Splits between _lastNextSplit and nextSplit - 1 were just crossed
                for (int i = _lastNextSplit; i < nextSplit && i < _rows.Count; i++)
                    StartCoroutine(FlashRow(i));

                _lastNextSplit = nextSplit;
                RefreshAllRows();
            }

            // Pulse the currently active split row
            _pulseTimer += Time.deltaTime;
            UpdateActivePulse(nextSplit);

            // Tick any ongoing flash animations
            TickFlashAnimations();
        }

        // -----------------------------------------------------------------------
        // Row building
        // -----------------------------------------------------------------------

        private void BuildRows()
        {
            if (splitRowsContainer == null) return;

            // Clear existing
            foreach (Transform child in splitRowsContainer)
                Destroy(child.gameObject);
            _rows.Clear();

            // Prefer split count and names from the manager; fall back to the known defaults
            int count = _manager != null ? _manager.SplitCount : 10;
            string[] fallbackNames =
            {
                "TailBasin", "TailSpires", "HindLegValley", "WingRoot", "SpineRidge",
                "TheGraveyard", "UpperBackStorm", "TheNeck", "TheCrown", "Summit"
            };

            for (int i = 0; i < count; i++)
            {
                string splitName = (_manager != null && !string.IsNullOrEmpty(_manager.GetSplitName(i)))
                    ? _manager.GetSplitName(i)
                    : (i < fallbackNames.Length ? fallbackNames[i] : $"Split {i + 1}");

                var row = new SplitRow();

                if (splitRowPrefab != null)
                {
                    row.root = Instantiate(splitRowPrefab, splitRowsContainer);
                }
                else
                {
                    row.root = CreateDefaultRowObject(splitRowsContainer);
                }

                row.background  = row.root.GetComponent<Image>();
                row.nameLabel   = FindTMP(row.root, "NameLabel");
                row.timeLabel   = FindTMP(row.root, "TimeLabel");
                row.deltaLabel  = FindTMP(row.root, "DeltaLabel");

                // Fallback: if no named children, grab any TMP in order
                if (row.nameLabel == null || row.timeLabel == null)
                {
                    TextMeshProUGUI[] all = row.root.GetComponentsInChildren<TextMeshProUGUI>();
                    if (all.Length >= 1 && row.nameLabel == null)  row.nameLabel  = all[0];
                    if (all.Length >= 2 && row.timeLabel == null)  row.timeLabel  = all[1];
                    if (all.Length >= 3 && row.deltaLabel == null) row.deltaLabel = all[2];
                }

                if (row.nameLabel != null)
                    row.nameLabel.text = splitName;
                if (row.timeLabel != null)
                    row.timeLabel.text = "--";
                if (row.deltaLabel != null)
                    row.deltaLabel.text = "";

                _rows.Add(row);
            }
        }

        private GameObject CreateDefaultRowObject(Transform parent)
        {
            var go = new GameObject("SplitRow", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(200f, 22f);

            Image img = go.GetComponent<Image>();
            img.color = Color.clear;

            // Name label
            var nameGO = new GameObject("NameLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            nameGO.transform.SetParent(go.transform, false);
            RectTransform nrt = nameGO.GetComponent<RectTransform>();
            nrt.anchorMin = new Vector2(0f, 0f);
            nrt.anchorMax = new Vector2(0.5f, 1f);
            nrt.offsetMin = nrt.offsetMax = Vector2.zero;
            var nTmp = nameGO.GetComponent<TextMeshProUGUI>();
            nTmp.fontSize = 11f;
            nTmp.color    = Color.white;

            // Time label
            var timeGO = new GameObject("TimeLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            timeGO.transform.SetParent(go.transform, false);
            RectTransform trt = timeGO.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0.5f, 0f);
            trt.anchorMax = new Vector2(0.75f, 1f);
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            var tTmp = timeGO.GetComponent<TextMeshProUGUI>();
            tTmp.fontSize  = 11f;
            tTmp.color     = Color.white;
            tTmp.alignment = TextAlignmentOptions.Right;

            // Delta label
            var deltaGO = new GameObject("DeltaLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            deltaGO.transform.SetParent(go.transform, false);
            RectTransform drt = deltaGO.GetComponent<RectTransform>();
            drt.anchorMin = new Vector2(0.75f, 0f);
            drt.anchorMax = new Vector2(1f, 1f);
            drt.offsetMin = drt.offsetMax = Vector2.zero;
            var dTmp = deltaGO.GetComponent<TextMeshProUGUI>();
            dTmp.fontSize  = 10f;
            dTmp.color     = Color.white;
            dTmp.alignment = TextAlignmentOptions.Right;

            return go;
        }

        // -----------------------------------------------------------------------
        // Row refresh
        // -----------------------------------------------------------------------

        private void RefreshAllRows()
        {
            int nextSplit = GetNextSplitIndex();
            for (int i = 0; i < _rows.Count; i++)
                RefreshRow(i, i == nextSplit);
        }

        // Alpha applied to rows that have not yet been crossed (fade-out for pending splits)
        private const float PendingRowAlpha  = 0.35f;
        private const float CrossedRowAlpha  = 1.0f;

        private void RefreshRow(int index, bool isActive)
        {
            if (index < 0 || index >= _rows.Count) return;
            SplitRow row = _rows[index];

            bool  crossed   = _manager.IsSplitCrossed(index);
            float rowAlpha  = (crossed || isActive) ? CrossedRowAlpha : PendingRowAlpha;

            // Sync name from manager (handles any runtime name changes)
            string splitName = _manager.GetSplitName(index);
            if (row.nameLabel != null)
            {
                row.nameLabel.text = splitName;
                Color nc = row.nameLabel.color;
                row.nameLabel.color = new Color(nc.r, nc.g, nc.b, rowAlpha);
            }

            // Highlight active row background
            if (row.background != null)
                row.background.color = isActive ? activeRowBg : Color.clear;

            // Time
            float splitTime = _manager.GetSplitTime(index);

            if (row.timeLabel != null)
            {
                row.timeLabel.text = crossed ? FormatShort(splitTime) : "--";
                Color tc = row.timeLabel.color;
                row.timeLabel.color = new Color(tc.r, tc.g, tc.b, rowAlpha);
            }

            // Delta
            float delta = _manager.GetSplitDelta(index);
            float pb    = _manager.GetSplitPB(index);

            if (row.deltaLabel != null)
            {
                if (!crossed || pb <= 0f)
                {
                    row.deltaLabel.text  = "";
                    row.deltaLabel.color = new Color(noPBColor.r, noPBColor.g, noPBColor.b, rowAlpha);
                }
                else
                {
                    string sign = delta <= 0f ? "-" : "+";
                    float  abs  = Mathf.Abs(delta);
                    Color  dc   = delta <= 0f ? aheadColor : behindColor;
                    row.deltaLabel.text  = $"{sign}{abs:F1}s";
                    row.deltaLabel.color = new Color(dc.r, dc.g, dc.b, rowAlpha);
                    row.targetColor      = dc;
                }
            }
        }

        // -----------------------------------------------------------------------
        // Flash animation
        // -----------------------------------------------------------------------

        private IEnumerator FlashRow(int index)
        {
            if (index < 0 || index >= _rows.Count) yield break;
            SplitRow row = _rows[index];
            row.isFlashing = true;
            row.flashTimer = 0f;

            float elapsed = 0f;
            while (elapsed < flashDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / flashDuration;
                // Flash white then fade to target delta color
                Color flashColor = Color.Lerp(Color.white, row.targetColor, t);
                if (row.timeLabel  != null) row.timeLabel.color  = flashColor;
                if (row.deltaLabel != null) row.deltaLabel.color = flashColor;
                yield return null;
            }

            row.isFlashing = false;
            RefreshRow(index, false);
        }

        private void TickFlashAnimations()
        {
            // handled via coroutines
        }

        // -----------------------------------------------------------------------
        // Active split pulse
        // -----------------------------------------------------------------------

        private void UpdateActivePulse(int nextSplit)
        {
            if (nextSplit < 0 || nextSplit >= _rows.Count) return;
            SplitRow row = _rows[nextSplit];
            if (row.isFlashing) return;

            float alpha = Mathf.Sin(_pulseTimer * (Mathf.PI * 2f / pulsePeriod)) * 0.5f + 0.5f;
            alpha = Mathf.Lerp(0.05f, 0.25f, alpha);

            if (row.background != null)
                row.background.color = new Color(1f, 1f, 1f, alpha);
        }

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        private int GetNextSplitIndex()
        {
            // Scan rows to find first uncrossed split
            for (int i = 0; i < _rows.Count; i++)
            {
                if (!_manager.IsSplitCrossed(i))
                    return i;
            }
            return _rows.Count;
        }

        private void SetContainerVisible(bool visible)
        {
            if (splitRowsContainer != null)
                splitRowsContainer.gameObject.SetActive(visible);
            if (totalTimeText != null)
                totalTimeText.gameObject.SetActive(visible);
        }

        private static string FormatShort(float seconds)
        {
            int m  = Mathf.FloorToInt(seconds / 60f);
            int s  = Mathf.FloorToInt(seconds % 60f);
            int ms = Mathf.FloorToInt((seconds % 1f) * 1000f);
            return $"{m}:{s:00}.{ms:000}";
        }

        private static string FormatLong(float seconds)
        {
            int m  = Mathf.FloorToInt(seconds / 60f);
            int s  = Mathf.FloorToInt(seconds % 60f);
            int ms = Mathf.FloorToInt((seconds % 1f) * 1000f);
            return $"{m:00}:{s:00}.{ms:000}";
        }

        private static TextMeshProUGUI FindTMP(GameObject root, string childName)
        {
            Transform t = root.transform.Find(childName);
            return t != null ? t.GetComponent<TextMeshProUGUI>() : null;
        }
    }
}
