using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TitanAscent.Systems
{
    /// <summary>
    /// Manages build versioning and metadata. Singleton — DontDestroyOnLoad.
    /// Displays the version string in the bottom-right corner of every scene.
    /// </summary>
    public class BuildVersionManager : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        // Singleton
        // -----------------------------------------------------------------------

        private static BuildVersionManager _instance;
        public static BuildVersionManager Instance => _instance;

        // -----------------------------------------------------------------------
        // Inspector fields
        // -----------------------------------------------------------------------

        [Header("Version Info")]
        [SerializeField] private string buildVersion = "0.3.1";
        [SerializeField] private string branch = "development";
        [SerializeField] private bool isDevBuild = true;

#if UNITY_EDITOR
        [Header("Auto-set at build")]
        [SerializeField]
#endif
        private string buildDate = "";

        // -----------------------------------------------------------------------
        // Properties
        // -----------------------------------------------------------------------

        public static string VersionString
        {
            get
            {
                if (_instance == null) return "v?.?.?";
                return $"v{_instance.buildVersion} ({_instance.branch})";
            }
        }

        public string BuildVersion => buildVersion;
        public string Branch => branch;
        public bool IsDevBuild => isDevBuild;
        public string BuildDate => buildDate;

        // -----------------------------------------------------------------------
        // Runtime overlay state
        // -----------------------------------------------------------------------

        private Canvas _overlayCanvas;
        private Text _versionText;
        private const string CanvasObjectName = "BuildVersionOverlay";

        // -----------------------------------------------------------------------
        // Lifecycle
        // -----------------------------------------------------------------------

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

#if UNITY_EDITOR
            if (string.IsNullOrEmpty(buildDate))
                buildDate = DateTime.Now.ToString("yyyy-MM-dd");
#endif

            SceneManager.sceneLoaded += OnSceneLoaded;
            CreateVersionOverlay();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, LoadSceneMode mode)
        {
            // Re-create if the overlay canvas was destroyed with the previous scene
            if (_overlayCanvas == null || _versionText == null)
                CreateVersionOverlay();
            else
                RefreshVersionText();
        }

        // -----------------------------------------------------------------------
        // Overlay construction
        // -----------------------------------------------------------------------

        private void CreateVersionOverlay()
        {
            // Destroy stale overlay if present
            if (_overlayCanvas != null)
            {
                Destroy(_overlayCanvas.gameObject);
                _overlayCanvas = null;
                _versionText = null;
            }

            // Canvas
            GameObject canvasGO = new GameObject(CanvasObjectName);
            DontDestroyOnLoad(canvasGO);
            canvasGO.transform.SetParent(transform, false);

            _overlayCanvas = canvasGO.AddComponent<Canvas>();
            _overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _overlayCanvas.sortingOrder = 9999;

            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // Text object — bottom-right anchor
            GameObject textGO = new GameObject("VersionText");
            textGO.transform.SetParent(canvasGO.transform, false);

            RectTransform rt = textGO.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-10f, 6f);
            rt.sizeDelta = new Vector2(300f, 24f);

            _versionText = textGO.AddComponent<Text>();
            _versionText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _versionText.fontSize = 13;
            _versionText.alignment = TextAnchor.LowerRight;
            _versionText.color = new Color(1f, 1f, 1f, 0.55f);
            _versionText.raycastTarget = false;

            RefreshVersionText();
        }

        private void RefreshVersionText()
        {
            if (_versionText == null) return;

            string label = VersionString;
            if (!string.IsNullOrEmpty(buildDate))
                label += $"  {buildDate}";
            if (isDevBuild)
                label += "  [DEV]";

            _versionText.text = label;
        }

        // -----------------------------------------------------------------------
        // Editor menu
        // -----------------------------------------------------------------------

#if UNITY_EDITOR
        [MenuItem("TitanAscent/Increment Build Version")]
        private static void IncrementBuildVersion()
        {
            BuildVersionManager[] found = FindObjectsOfType<BuildVersionManager>();
            BuildVersionManager target = found.Length > 0 ? found[0] : null;

            if (target == null)
            {
                // Try to find in prefabs / scene via selection
                UnityEngine.Debug.LogWarning("[BuildVersionManager] No BuildVersionManager found in open scene.");
                return;
            }

            // Bump patch version
            string ver = target.buildVersion;
            string[] parts = ver.Split('.');
            if (parts.Length == 3
                && int.TryParse(parts[0], out int major)
                && int.TryParse(parts[1], out int minor)
                && int.TryParse(parts[2], out int patch))
            {
                patch++;
                target.buildVersion = $"{major}.{minor}.{patch}";
                target.buildDate = DateTime.Now.ToString("yyyy-MM-dd");
                EditorUtility.SetDirty(target);
                UnityEngine.Debug.Log($"[BuildVersionManager] Version bumped to {target.buildVersion}");
            }
            else
            {
                UnityEngine.Debug.LogWarning($"[BuildVersionManager] Cannot parse version '{ver}'. Expected format: major.minor.patch");
            }
        }
#endif
    }
}
