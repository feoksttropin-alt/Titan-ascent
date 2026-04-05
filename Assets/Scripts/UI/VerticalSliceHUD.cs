#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using TitanAscent.Grapple;
using TitanAscent.Physics;
using TitanAscent.Systems;
using TitanAscent.Environment;

namespace TitanAscent.UI
{
    /// <summary>
    /// Extra HUD panel for vertical slice playtesting.
    /// Toggled via DebugMenu's static bool IsActive.
    /// Shows grapple state, rope tension, last swing gain, frustration level, zone info.
    /// Uses IMGUI OnGUI; renders in top-left corner.
    /// </summary>
    public class VerticalSliceHUD : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        // Static toggle (driven by DebugMenu or other systems)
        // -----------------------------------------------------------------------

        public static bool IsActive = false;

        // -----------------------------------------------------------------------
        // Inspector references
        // -----------------------------------------------------------------------

        [Header("References")]
        [SerializeField] private GrappleController   grappleController;
        [SerializeField] private RopeSimulator       ropeSimulator;
        [SerializeField] private SwingAnalyzer       swingAnalyzer;
        [SerializeField] private FrustrationDetector frustrationDetector;
        [SerializeField] private ZoneManager         zoneManager;

        // -----------------------------------------------------------------------
        // Constants
        // -----------------------------------------------------------------------

        private const float SliceMaxHeight = 1800f;
        private const float PanelWidth     = 260f;
        private const float PanelX         = 10f;
        private const float PanelY         = 10f;

        // -----------------------------------------------------------------------
        // Tracked swing data (from SwingAnalyzer events)
        // -----------------------------------------------------------------------

        private float  _lastSwingHeightGain = 0f;
        private string _lastTechnique       = "—";
        private FrustrationEvent _lastFrustration;
        private bool _hasFrustration = false;

        // -----------------------------------------------------------------------
        // GUI
        // -----------------------------------------------------------------------

        private GUIStyle _panelStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _headerStyle;
        private bool     _stylesInitialized = false;

        // -----------------------------------------------------------------------
        // Lifecycle
        // -----------------------------------------------------------------------

        private void Start()
        {
            // Auto-find references if not set
            if (grappleController  == null) grappleController  = FindFirstObjectByType<GrappleController>();
            if (ropeSimulator      == null) ropeSimulator      = FindFirstObjectByType<RopeSimulator>();
            if (swingAnalyzer      == null) swingAnalyzer      = FindFirstObjectByType<SwingAnalyzer>();
            if (frustrationDetector == null) frustrationDetector = FindFirstObjectByType<FrustrationDetector>();
            if (zoneManager        == null) zoneManager        = FindFirstObjectByType<ZoneManager>();

            // Subscribe to SwingAnalyzer events
            if (swingAnalyzer != null)
            {
                swingAnalyzer.OnPerfectRelease.AddListener(OnPerfectRelease);
                swingAnalyzer.OnSlingshotDetected.AddListener(OnSlingshot);
                swingAnalyzer.OnChainSwing.AddListener(OnChainSwing);
            }

            // Subscribe to FrustrationDetector
            if (frustrationDetector != null)
                frustrationDetector.OnFrustrationDetected += OnFrustrationDetected;
        }

        private void OnDestroy()
        {
            if (swingAnalyzer != null)
            {
                swingAnalyzer.OnPerfectRelease.RemoveListener(OnPerfectRelease);
                swingAnalyzer.OnSlingshotDetected.RemoveListener(OnSlingshot);
                swingAnalyzer.OnChainSwing.RemoveListener(OnChainSwing);
            }

            if (frustrationDetector != null)
                frustrationDetector.OnFrustrationDetected -= OnFrustrationDetected;
        }

        // -----------------------------------------------------------------------
        // Event handlers
        // -----------------------------------------------------------------------

        private void OnPerfectRelease(float heightGain)
        {
            _lastSwingHeightGain = heightGain;
            _lastTechnique       = "PerfectRelease";
        }

        private void OnSlingshot()
        {
            _lastTechnique = "Slingshot";
        }

        private void OnChainSwing(int chainCount)
        {
            _lastTechnique = $"ChainSwing x{chainCount}";
        }

        private void OnFrustrationDetected(FrustrationEvent evt)
        {
            _lastFrustration = evt;
            _hasFrustration  = true;
        }

        // -----------------------------------------------------------------------
        // IMGUI
        // -----------------------------------------------------------------------

        private void OnGUI()
        {
            if (!IsActive) return;

            InitStyles();

            // Determine content height dynamically
            float rowH   = 20f;
            float rows   = 9f;
            float height = rows * rowH + 16f;

            Rect panelRect = new Rect(PanelX, PanelY, PanelWidth, height);
            GUI.Box(panelRect, GUIContent.none, _panelStyle);

            GUILayout.BeginArea(new Rect(PanelX + 6f, PanelY + 6f, PanelWidth - 12f, height - 12f));

            GUI.Label(new Rect(0, 0, PanelWidth - 12f, rowH), "VERTICAL SLICE HUD", _headerStyle);
            float y = rowH + 2f;

            // Zone
            string zoneName  = "—";
            int    zoneIndex = 0;
            if (zoneManager != null && zoneManager.CurrentZone != null)
            {
                zoneName  = zoneManager.CurrentZone.name;
                zoneIndex = zoneManager.CurrentZoneIndex;
            }
            DrawRow(ref y, rowH, $"Zone: [{zoneIndex}] {zoneName}");

            // Grapple state
            string grappleState = grappleController != null
                ? grappleController.CurrentState.ToString()
                : "N/A";
            DrawRow(ref y, rowH, $"Grapple: {grappleState}");

            // Rope tension %
            float tension = ropeSimulator != null ? ropeSimulator.CurrentTension * 100f : 0f;
            DrawRow(ref y, rowH, $"Rope Tension: {tension:F0}%");

            // Last swing height gain
            DrawRow(ref y, rowH, $"Last Swing Gain: +{_lastSwingHeightGain:F1}m");

            // Last technique
            DrawRow(ref y, rowH, $"Technique: {_lastTechnique}");

            // Frustration level dot
            string frustStr = "Normal";
            Color  frustColor = Color.green;
            if (_hasFrustration && _lastFrustration != null)
            {
                switch (_lastFrustration.severity)
                {
                    case 1: frustStr = "Mild";   frustColor = Color.yellow; break;
                    case 2: frustStr = "High";   frustColor = Color.red;    break;
                    case 3: frustStr = "High";   frustColor = Color.red;    break;
                }
            }
            Color prevColor = GUI.color;
            GUI.color = frustColor;
            GUI.Label(new Rect(0, y, PanelWidth - 12f, rowH), $"● Frustration: {frustStr}", _labelStyle);
            GUI.color = prevColor;
            y += rowH;

            // Slice progress
            float playerHeight = 0f;
            if (grappleController != null)
                playerHeight = grappleController.transform.position.y;
            float progress = Mathf.Clamp01(playerHeight / SliceMaxHeight) * 100f;
            DrawRow(ref y, rowH, $"Slice Progress: {progress:F0}%");

            GUILayout.EndArea();
        }

        private void DrawRow(ref float y, float rowH, string text)
        {
            GUI.Label(new Rect(0, y, PanelWidth - 12f, rowH), text, _labelStyle);
            y += rowH;
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;
            _stylesInitialized = true;

            Texture2D bgTex = new Texture2D(1, 1);
            bgTex.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.65f));
            bgTex.Apply();

            _panelStyle = new GUIStyle(GUI.skin.box);
            _panelStyle.normal.background = bgTex;

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 11,
                alignment = TextAnchor.MiddleLeft
            };
            _labelStyle.normal.textColor = Color.white;

            _headerStyle = new GUIStyle(_labelStyle)
            {
                fontSize   = 12,
                fontStyle  = FontStyle.Bold
            };
            _headerStyle.normal.textColor = new Color(1f, 0.8f, 0.2f);
        }
    }
}
#endif
