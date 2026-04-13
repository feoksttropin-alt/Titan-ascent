#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using UnityEngine;
using TitanAscent.Player;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TitanAscent.Systems
{
    /// <summary>
    /// Debug tool for bookmarking heights during development.
    ///
    /// - Stores up to 10 named height bookmarks.
    /// - Ctrl+1–0 to teleport; Ctrl+Shift+1–0 to save current height as that slot.
    /// - Persists via EditorPrefs (editor) or PlayerPrefs (dev builds).
    /// - IMGUI panel drawn when called from DebugMenu.
    /// - "Pin to height" places a persistent LineRenderer marker in world space.
    ///
    /// Only compiled in Editor and Development builds.
    /// </summary>
    public class PathBookmarkSystem : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        // Data types
        // -----------------------------------------------------------------------

        [Serializable]
        public class HeightBookmark
        {
            public string name = "";
            public float height = 0f;
            public int zoneIndex = 0;
            public string notes = "";
        }

        // -----------------------------------------------------------------------
        // Constants
        // -----------------------------------------------------------------------

        private const int MaxBookmarks = 10;
        private const string PrefPrefix = "DevBookmark_";
        private const float PinLineWidth = 0.1f;
        private const float PinLineLength = 80f;

        // -----------------------------------------------------------------------
        // Inspector
        // -----------------------------------------------------------------------

        [Header("References")]
        [SerializeField] private PlayerController playerController;

        // -----------------------------------------------------------------------
        // Private state
        // -----------------------------------------------------------------------

        private HeightBookmark[] _bookmarks = new HeightBookmark[MaxBookmarks];
        private string[] _editNameBuffers = new string[MaxBookmarks];
        private List<GameObject> _pinMarkers = new List<GameObject>();

        // Keyboard codes for 1–0 in order
        private static readonly KeyCode[] DigitKeys =
        {
            KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5,
            KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9, KeyCode.Alpha0
        };

        // -----------------------------------------------------------------------
        // Lifecycle
        // -----------------------------------------------------------------------

        private void Awake()
        {
            if (playerController == null)
                playerController = FindFirstObjectByType<PlayerController>();

            LoadAllBookmarks();

            for (int i = 0; i < MaxBookmarks; i++)
                _editNameBuffers[i] = _bookmarks[i].name;
        }

        private void Update()
        {
            HandleKeyboardShortcuts();
        }

        // -----------------------------------------------------------------------
        // Keyboard shortcuts
        // -----------------------------------------------------------------------

        private void HandleKeyboardShortcuts()
        {
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            if (!ctrl) return;

            for (int i = 0; i < MaxBookmarks; i++)
            {
                if (!Input.GetKeyDown(DigitKeys[i])) continue;

                if (shift)
                    SaveCurrentHeightAsBookmark(i);
                else
                    TeleportToBookmark(i);
            }
        }

        // -----------------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------------

        /// <summary>Teleports the player to bookmark at <paramref name="slotIndex"/> (0-based).</summary>
        public void TeleportToBookmark(int slotIndex)
        {
            if (!IsValidSlot(slotIndex)) return;
            HeightBookmark bm = _bookmarks[slotIndex];

            if (playerController == null) return;

            Rigidbody rb = playerController.GetComponent<Rigidbody>();
            playerController.transform.position = new Vector3(0f, bm.height, 0f);
            if (rb != null) rb.linearVelocity = Vector3.zero;

            Debug.Log($"[PathBookmark] Teleported to '{bm.name}' at {bm.height:F1} m (slot {slotIndex + 1})");
        }

        /// <summary>Saves the player's current height as bookmark at <paramref name="slotIndex"/>.</summary>
        public void SaveCurrentHeightAsBookmark(int slotIndex)
        {
            if (!IsValidSlot(slotIndex) || playerController == null) return;

            HeightBookmark bm = _bookmarks[slotIndex];
            bm.height = playerController.transform.position.y;
            if (string.IsNullOrEmpty(bm.name))
                bm.name = $"Slot {slotIndex + 1}";

            _editNameBuffers[slotIndex] = bm.name;
            SaveBookmark(slotIndex);

            Debug.Log($"[PathBookmark] Saved height {bm.height:F1} m to slot {slotIndex + 1} ('{bm.name}')");
        }

        /// <summary>Deletes a bookmark and removes its world-space pin if present.</summary>
        public void DeleteBookmark(int slotIndex)
        {
            if (!IsValidSlot(slotIndex)) return;

            RemovePinMarker(slotIndex);

            _bookmarks[slotIndex] = new HeightBookmark();
            _editNameBuffers[slotIndex] = "";
            DeletePersistedBookmark(slotIndex);

            Debug.Log($"[PathBookmark] Deleted bookmark at slot {slotIndex + 1}");
        }

        /// <summary>
        /// Places a persistent visual line marker in world space at the bookmarked height.
        /// Uses a LineRenderer in a distinct teal colour to differentiate from HeightMarker.
        /// </summary>
        public void PinToHeight(int slotIndex)
        {
            if (!IsValidSlot(slotIndex)) return;

            HeightBookmark bm = _bookmarks[slotIndex];

            // Remove any existing pin for this slot first
            RemovePinMarker(slotIndex);

            // Ensure list is large enough
            while (_pinMarkers.Count <= slotIndex)
                _pinMarkers.Add(null);

            GameObject markerGO = new GameObject($"BookmarkPin_Slot{slotIndex + 1}");
            LineRenderer lr = markerGO.AddComponent<LineRenderer>();

            Color pinColor = new Color(0.1f, 0.9f, 0.85f, 0.8f);
            lr.positionCount = 2;
            lr.startWidth = PinLineWidth;
            lr.endWidth = PinLineWidth;
            lr.startColor = pinColor;
            lr.endColor = pinColor;
            lr.useWorldSpace = true;

            Material mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = pinColor;
            lr.material = mat;

            float y = bm.height;
            lr.SetPosition(0, new Vector3(-PinLineLength * 0.5f, y, 0f));
            lr.SetPosition(1, new Vector3(PinLineLength * 0.5f, y, 0f));

            _pinMarkers[slotIndex] = markerGO;

            Debug.Log($"[PathBookmark] Pinned slot {slotIndex + 1} ('{bm.name}') at {bm.height:F1} m");
        }

        // -----------------------------------------------------------------------
        // IMGUI panel — called by DebugMenu
        // -----------------------------------------------------------------------

        /// <summary>Draw the bookmark panel inside the caller's IMGUI layout.</summary>
        public void DrawIMGUIPanel()
        {
            GUILayout.Label("── HEIGHT BOOKMARKS ──");

            for (int i = 0; i < MaxBookmarks; i++)
            {
                HeightBookmark bm = _bookmarks[i];
                bool hasData = !string.IsNullOrEmpty(bm.name) || bm.height != 0f;

                GUILayout.BeginHorizontal();

                // Slot index label
                GUILayout.Label($"[{i + 1}]", GUILayout.Width(24f));

                // Editable name field
                string newName = GUILayout.TextField(_editNameBuffers[i], GUILayout.Width(90f));
                if (newName != _editNameBuffers[i])
                {
                    _editNameBuffers[i] = newName;
                    _bookmarks[i].name = newName;
                    SaveBookmark(i);
                }

                // Height display
                GUILayout.Label(hasData ? $"{bm.height:F0} m" : "—", GUILayout.Width(54f));

                // Teleport button
                GUI.enabled = hasData;
                if (GUILayout.Button("Go", GUILayout.Width(30f)))
                    TeleportToBookmark(i);

                // Pin button
                if (GUILayout.Button("Pin", GUILayout.Width(32f)))
                    PinToHeight(i);

                // Delete button
                if (GUILayout.Button("Del", GUILayout.Width(32f)))
                    DeleteBookmark(i);

                GUI.enabled = true;
                GUILayout.EndHorizontal();

                // Show notes on its own line if present
                if (!string.IsNullOrEmpty(bm.notes))
                    GUILayout.Label($"   {bm.notes}", GUILayout.ExpandWidth(true));
            }

            GUILayout.Space(4f);
            if (GUILayout.Button("Save Current Height to Selected...", GUILayout.ExpandWidth(true)))
                Debug.Log("[PathBookmark] Use Ctrl+Shift+[1-0] to save the current height to a slot.");
        }

        // -----------------------------------------------------------------------
        // Persistence
        // -----------------------------------------------------------------------

        private void LoadAllBookmarks()
        {
            for (int i = 0; i < MaxBookmarks; i++)
            {
                _bookmarks[i] = new HeightBookmark();
                string key = PrefPrefix + i;
                string json = ReadPref(key);
                if (!string.IsNullOrEmpty(json))
                {
                    try
                    {
                        JsonUtility.FromJsonOverwrite(json, _bookmarks[i]);
                    }
                    catch
                    {
                        _bookmarks[i] = new HeightBookmark();
                    }
                }
            }
        }

        private void SaveBookmark(int slotIndex)
        {
            string key = PrefPrefix + slotIndex;
            string json = JsonUtility.ToJson(_bookmarks[slotIndex]);
            WritePref(key, json);
        }

        private void DeletePersistedBookmark(int slotIndex)
        {
            string key = PrefPrefix + slotIndex;
            DeletePref(key);
        }

        private static string ReadPref(string key)
        {
#if UNITY_EDITOR
            return EditorPrefs.GetString(key, "");
#else
            return PlayerPrefs.GetString(key, "");
#endif
        }

        private static void WritePref(string key, string value)
        {
#if UNITY_EDITOR
            EditorPrefs.SetString(key, value);
#else
            PlayerPrefs.SetString(key, value);
            PlayerPrefs.Save();
#endif
        }

        private static void DeletePref(string key)
        {
#if UNITY_EDITOR
            EditorPrefs.DeleteKey(key);
#else
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
#endif
        }

        // -----------------------------------------------------------------------
        // Pin helpers
        // -----------------------------------------------------------------------

        private void RemovePinMarker(int slotIndex)
        {
            if (slotIndex < _pinMarkers.Count && _pinMarkers[slotIndex] != null)
            {
                Destroy(_pinMarkers[slotIndex]);
                _pinMarkers[slotIndex] = null;
            }
        }

        // -----------------------------------------------------------------------
        // Utility
        // -----------------------------------------------------------------------

        private static bool IsValidSlot(int slotIndex) =>
            slotIndex >= 0 && slotIndex < MaxBookmarks;
    }
}
#endif
