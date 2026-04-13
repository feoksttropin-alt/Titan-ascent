#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.InputSystem;

namespace TitanAscent.Debug
{
    /// <summary>
    /// Real-time frame time bar-graph IMGUI overlay.
    /// Toggle: F4 (independent from DebugMenu F3 / backtick).
    /// Draws in bottom-left corner: 240x80 px panel, rolling 120-frame history.
    /// </summary>
    public class FrameTimeGraph : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        // Constants
        // -----------------------------------------------------------------------

        private const int   BufferSize      = 120;
        private const float PanelWidth      = 240f;
        private const float PanelHeight     = 80f;
        private const float BarWidth        = 2f;         // px per bar
        private const float MaxDisplayMs    = 50f;        // 50 ms = full bar height
        private const float FpsThreshold60  = 1000f / 60f; // 16.67 ms
        private const float FpsThreshold30  = 1000f / 30f; // 33.33 ms
        private const float LabelHeight     = 16f;
        private const float Padding         = 6f;

        private static readonly Color ColorGreen  = new Color(0.2f, 0.9f, 0.2f, 1f);
        private static readonly Color ColorYellow = new Color(0.95f, 0.85f, 0.1f, 1f);
        private static readonly Color ColorRed    = new Color(0.95f, 0.2f, 0.2f, 1f);
        private static readonly Color PanelBg     = new Color(0f, 0f, 0f, 0.7f);
        private static readonly Color LineColor60  = new Color(0.2f, 0.9f, 0.2f, 0.7f);
        private static readonly Color LineColor30  = new Color(0.95f, 0.85f, 0.1f, 0.7f);

        // -----------------------------------------------------------------------
        // State
        // -----------------------------------------------------------------------

        private bool    _visible       = false;
        private float[] _frameTimes    = new float[BufferSize];
        private int     _writeIndex    = 0;

        // FixedUpdate count per second
        private int   _fixedUpdateCount      = 0;
        private int   _fixedUpdatesPerSecond = 0;
        private float _fixedUpdateTimer      = 0f;

        // Cached textures for colored bars
        private Texture2D _texGreen;
        private Texture2D _texYellow;
        private Texture2D _texRed;
        private Texture2D _texWhite;

        // GUI style
        private GUIStyle _labelStyle;

        // -----------------------------------------------------------------------
        // Lifecycle
        // -----------------------------------------------------------------------

        private void Awake()
        {
            _texGreen  = MakeTex(ColorGreen);
            _texYellow = MakeTex(ColorYellow);
            _texRed    = MakeTex(ColorRed);
            _texWhite  = MakeTex(Color.white);
        }

        private void OnDestroy()
        {
            Destroy(_texGreen);
            Destroy(_texYellow);
            Destroy(_texRed);
            Destroy(_texWhite);
        }

        private void Update()
        {
            // Toggle
            if (Keyboard.current != null && Keyboard.current.f4Key.wasPressedThisFrame)
                _visible = !_visible;

            // Record frame time (ms)
            _frameTimes[_writeIndex] = Time.deltaTime * 1000f;
            _writeIndex = (_writeIndex + 1) % BufferSize;

            // FixedUpdate counter (accumulated per second)
            _fixedUpdateTimer += Time.deltaTime;
            if (_fixedUpdateTimer >= 1f)
            {
                _fixedUpdatesPerSecond = _fixedUpdateCount;
                _fixedUpdateCount      = 0;
                _fixedUpdateTimer     -= 1f;
            }
        }

        private void FixedUpdate()
        {
            _fixedUpdateCount++;
        }

        // -----------------------------------------------------------------------
        // GUI
        // -----------------------------------------------------------------------

        private void OnGUI()
        {
            if (!_visible) return;

            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize  = 10,
                    alignment = TextAnchor.MiddleLeft
                };
                _labelStyle.normal.textColor = Color.white;
            }

            float screenH = Screen.height;
            float totalH  = LabelHeight + PanelHeight + Padding * 2f + 14f; // +14 for FixedUpdate line
            float panelX  = Padding;
            float panelY  = screenH - totalH - Padding;

            // Background
            DrawRect(new Rect(panelX, panelY, PanelWidth, totalH), PanelBg);

            // Compute stats over rolling buffer
            float minMs = float.MaxValue, maxMs = 0f, sumMs = 0f;
            for (int i = 0; i < BufferSize; i++)
            {
                float v = _frameTimes[i];
                if (v < minMs) minMs = v;
                if (v > maxMs) maxMs = v;
                sumMs += v;
            }
            float avgMs = sumMs / BufferSize;
            if (minMs == float.MaxValue) minMs = 0f;

            // Stats label
            float labelY = panelY + Padding;
            GUI.Label(new Rect(panelX + Padding, labelY, PanelWidth - Padding * 2f, LabelHeight),
                $"min: {minMs:F1}ms  avg: {avgMs:F1}ms  max: {maxMs:F1}ms", _labelStyle);

            // Bar graph area
            float graphX = panelX + Padding;
            float graphY = labelY + LabelHeight + 2f;
            float graphW = PanelWidth - Padding * 2f;
            float graphH = PanelHeight;

            // Draw bars
            int barsToShow = Mathf.FloorToInt(graphW / BarWidth);
            barsToShow = Mathf.Min(barsToShow, BufferSize);

            for (int i = 0; i < barsToShow; i++)
            {
                // Read in chronological order from the rolling buffer
                int bufIdx = (_writeIndex - barsToShow + i + BufferSize) % BufferSize;
                float ms   = _frameTimes[bufIdx];
                float normH = Mathf.Clamp01(ms / MaxDisplayMs) * graphH;

                float barX = graphX + i * BarWidth;
                float barY = graphY + graphH - normH;

                Texture2D barTex;
                if (ms <= FpsThreshold60)       barTex = _texGreen;
                else if (ms <= FpsThreshold30)  barTex = _texYellow;
                else                            barTex = _texRed;

                GUI.DrawTexture(new Rect(barX, barY, BarWidth - 1f, normH), barTex);
            }

            // Horizontal reference lines
            float line60Y = graphY + graphH - (FpsThreshold60 / MaxDisplayMs) * graphH;
            float line30Y = graphY + graphH - (FpsThreshold30 / MaxDisplayMs) * graphH;

            DrawHorizontalLine(new Rect(graphX, line60Y, graphW, 1f), LineColor60);
            DrawHorizontalLine(new Rect(graphX, line30Y, graphW, 1f), LineColor30);

            // Line labels
            _labelStyle.normal.textColor = LineColor60;
            GUI.Label(new Rect(graphX + graphW - 30f, line60Y - 11f, 30f, 12f), "60fps", _labelStyle);
            _labelStyle.normal.textColor = LineColor30;
            GUI.Label(new Rect(graphX + graphW - 30f, line30Y - 11f, 30f, 12f), "30fps", _labelStyle);
            _labelStyle.normal.textColor = Color.white;

            // FixedUpdate count
            float fixedY = graphY + graphH + 2f;
            GUI.Label(new Rect(graphX, fixedY, graphW, 14f),
                $"FixedUpdate/s: {_fixedUpdatesPerSecond}", _labelStyle);
        }

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        private static void DrawRect(Rect r, Color c)
        {
            Color prev = GUI.color;
            GUI.color  = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color  = prev;
        }

        private static void DrawHorizontalLine(Rect r, Color c)
        {
            DrawRect(r, c);
        }

        private static Texture2D MakeTex(Color c)
        {
            Texture2D t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }
    }
}
#endif
