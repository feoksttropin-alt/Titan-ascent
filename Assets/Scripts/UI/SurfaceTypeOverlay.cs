#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using UnityEngine;
using TitanAscent.Environment;
using TitanAscent.Debug;

namespace TitanAscent.UI
{
    /// <summary>
    /// Debug overlay that renders surface type labels above visible SurfaceAnchorPoint
    /// components within 80m of the player. Toggle via DebugMenu or SurfaceTypeOverlay.IsActive.
    /// Only compiled in Editor and Development builds.
    /// </summary>
    public class SurfaceTypeOverlay : MonoBehaviour
    {
        // ── Static Toggle ────────────────────────────────────────────────────
        /// <summary>Set by DebugMenu to enable/disable the overlay at runtime.</summary>
        public static bool IsActive { get; set; }

        // ── Inspector ────────────────────────────────────────────────────────

        [Header("Scan Settings")]
        [SerializeField] private float scanRadius = 80f;
        [SerializeField] private float scanInterval = 0.25f;

        // ── Internal ─────────────────────────────────────────────────────────

        private Transform playerTransform;
        private Camera    mainCamera;

        private List<SurfaceAnchorPoint> visibleAnchors = new List<SurfaceAnchorPoint>();
        private float nextScanTime;

        // Cached connectivity data: anchor -> connectivity color
        private AnchorValidator anchorValidator;
        private Dictionary<SurfaceAnchorPoint, Color> connectivityColors = new Dictionary<SurfaceAnchorPoint, Color>();

        private static readonly GUIStyle LabelStyle  = new GUIStyle();
        private static readonly GUIStyle ShadowStyle = new GUIStyle();

        private static bool stylesInitialized;

        // ── Unity Lifecycle ──────────────────────────────────────────────────

        private void Awake()
        {
            mainCamera = Camera.main;
        }

        private void Start()
        {
            Player.PlayerController player = FindFirstObjectByType<Player.PlayerController>();
            if (player != null) playerTransform = player.transform;

            anchorValidator = FindFirstObjectByType<AnchorValidator>();
        }

        private void Update()
        {
            if (!IsActive) return;
            if (mainCamera == null) mainCamera = Camera.main;

            if (Time.time >= nextScanTime)
            {
                ScanAnchors();
                nextScanTime = Time.time + scanInterval;
            }
        }

        private void OnGUI()
        {
            if (!IsActive || mainCamera == null) return;

            EnsureStyles();

            foreach (SurfaceAnchorPoint anchor in visibleAnchors)
            {
                if (anchor == null) continue;

                // Label position: slightly above the anchor
                Vector3 worldPos    = anchor.transform.position + Vector3.up * 0.6f;
                Vector3 screenPos   = mainCamera.WorldToScreenPoint(worldPos);

                // Only draw if in front of camera
                if (screenPos.z <= 0f) continue;

                // Flip Y for GUI (GUI origin is top-left, screen origin is bottom-left)
                float guiX = screenPos.x;
                float guiY = Screen.height - screenPos.y;

                string surfaceName = anchor.AnchorSurfaceType.ToString();
                float  grip        = anchor.HoldStrength;
                string label       = $"{surfaceName}\ngrip:{grip:F2}";

                // Surface type color
                Color labelColor = GetSurfaceColor(anchor.AnchorSurfaceType);

                // Connectivity dot
                Color dotColor = GetConnectivityColor(anchor);

                // Draw shadow for readability
                ShadowStyle.normal.textColor = Color.black;
                Rect shadowRect = new Rect(guiX - 49f, guiY - 19f, 100f, 50f);
                GUI.Label(shadowRect, label, ShadowStyle);

                // Draw label
                LabelStyle.normal.textColor = labelColor;
                Rect labelRect = new Rect(guiX - 50f, guiY - 20f, 100f, 50f);
                GUI.Label(labelRect, label, LabelStyle);

                // Draw connectivity dot (5x5 px)
                Color prev = GUI.color;
                GUI.color  = dotColor;
                GUI.DrawTexture(new Rect(guiX + 30f, guiY - 20f, 6f, 6f), Texture2D.whiteTexture);
                GUI.color  = prev;
            }
        }

        // ── Private ──────────────────────────────────────────────────────────

        private void ScanAnchors()
        {
            visibleAnchors.Clear();
            connectivityColors.Clear();

            Vector3 origin = playerTransform != null
                ? playerTransform.position
                : (mainCamera != null ? mainCamera.transform.position : Vector3.zero);

            SurfaceAnchorPoint[] all = FindObjectsOfType<SurfaceAnchorPoint>();
            foreach (SurfaceAnchorPoint anchor in all)
            {
                float dist = Vector3.Distance(origin, anchor.transform.position);
                if (dist > scanRadius) continue;

                visibleAnchors.Add(anchor);
            }
        }

        private Color GetSurfaceColor(SurfaceType type)
        {
            switch (type)
            {
                case SurfaceType.ScaleArmor:   return new Color(0.75f, 0.75f, 0.75f);
                case SurfaceType.BoneRidge:    return Color.white;
                case SurfaceType.CrystalSurface: return Color.cyan;
                case SurfaceType.MuscleSkin:   return new Color(1f, 0.3f, 0.3f);
                case SurfaceType.WingMembrane: return new Color(0.4f, 0.6f, 1f);
                default:                       return Color.white;
            }
        }

        private Color GetConnectivityColor(SurfaceAnchorPoint anchor)
        {
            if (connectivityColors.TryGetValue(anchor, out Color cached))
                return cached;

            // Derive from anchor's own visual state / hold strength as a proxy
            // (AnchorValidator's full data is not exposed per-anchor externally)
            float strength = anchor.HoldStrength;
            Color c;
            if (strength >= 0.7f)        c = Color.green;
            else if (strength >= 0.35f)  c = Color.yellow;
            else                         c = Color.red;

            connectivityColors[anchor] = c;
            return c;
        }

        private static void EnsureStyles()
        {
            if (stylesInitialized) return;
            stylesInitialized = true;

            LabelStyle.fontSize   = 10;
            LabelStyle.fontStyle  = FontStyle.Bold;
            LabelStyle.alignment  = TextAnchor.UpperCenter;

            ShadowStyle.fontSize  = 10;
            ShadowStyle.fontStyle = FontStyle.Bold;
            ShadowStyle.alignment = TextAnchor.UpperCenter;
            // Offset applied at draw site
        }
    }
}
#endif
