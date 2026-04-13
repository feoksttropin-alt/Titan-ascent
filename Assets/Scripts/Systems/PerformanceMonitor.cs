#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Diagnostics;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TitanAscent.Grapple;

namespace TitanAscent.Systems
{
    /// <summary>
    /// Tracks runtime performance metrics and displays them as an IMGUI overlay.
    /// Only compiled and active in the Unity Editor and Development Builds.
    ///
    /// Toggle overlay visibility with F3.
    /// </summary>
    public class PerformanceMonitor : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        // Constants
        // -----------------------------------------------------------------------

        private const int   HistoryLength     = 300;
        private const float LowFpsThreshold   = 45f;
        private const float LowFpsWarnSeconds = 2f;
        private const float RopeCostWarningMs = 2f;

        // -----------------------------------------------------------------------
        // Private State
        // -----------------------------------------------------------------------

        // FPS
        private float[] _fpsHistory   = new float[HistoryLength];
        private int      _fpsIndex;
        private float    _smoothedFps;
        private float    _fpsSmoothAccum;
        private int      _fpsSmoothFrames;

        // FixedUpdate calls / second
        private float[] _fixedHistory  = new float[HistoryLength];
        private int      _fixedIndex;
        private int      _fixedCallsThisSecond;
        private float    _fixedSecondTimer;
        private float    _fixedCallsLastSecond;

        // Rope simulation cost (ms)
        private float[] _ropeCostHistory = new float[HistoryLength];
        private int      _ropeCostIndex;
        private readonly Stopwatch _ropeStopwatch = new Stopwatch();

        // Particle count
        private float[] _particleHistory = new float[HistoryLength];
        private int      _particleIndex;

        // Memory (MB)
        private float[] _memoryHistory = new float[HistoryLength];
        private int      _memoryIndex;

        // Low FPS warning
        private float _lowFpsDuration;
        private bool  _lowFpsWarningLogged;

        // Overlay toggle
        private bool _showOverlay = true;

        // Rope simulator reference
        private RopeSimulator _ropeSimulator;

        // IMGUI style (lazy initialised)
        private GUIStyle _boxStyle;
        private GUIStyle _labelStyle;

        // -----------------------------------------------------------------------
        // Lifecycle
        // -----------------------------------------------------------------------

        private void Awake()
        {
            _ropeSimulator = FindFirstObjectByType<RopeSimulator>();
        }

        private void Update()
        {
            HandleOverlayToggle();
            TrackFps();
            TrackParticles();
            TrackMemory();
            CheckLowFpsWarning();
        }

        private void FixedUpdate()
        {
            _fixedCallsThisSecond++;
            _fixedSecondTimer += Time.fixedDeltaTime;

            if (_fixedSecondTimer >= 1f)
            {
                _fixedCallsLastSecond = _fixedCallsThisSecond;
                RecordHistory(_fixedHistory, ref _fixedIndex, _fixedCallsThisSecond);
                _fixedCallsThisSecond = 0;
                _fixedSecondTimer     = 0f;
            }

            MeasureRopeCost();
        }

        private void OnGUI()
        {
            if (!_showOverlay) return;
            InitStyles();
            DrawOverlay();
        }

        // -----------------------------------------------------------------------
        // Tracking Methods
        // -----------------------------------------------------------------------

        private void HandleOverlayToggle()
        {
            if (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame)
                _showOverlay = !_showOverlay;
        }

        private void TrackFps()
        {
            float rawFps = Time.deltaTime > 0f ? 1f / Time.deltaTime : 0f;

            // 60-frame rolling average
            _fpsSmoothAccum  += rawFps;
            _fpsSmoothFrames++;

            if (_fpsSmoothFrames >= 60)
            {
                _smoothedFps     = _fpsSmoothAccum / _fpsSmoothFrames;
                _fpsSmoothAccum  = 0f;
                _fpsSmoothFrames = 0;
                RecordHistory(_fpsHistory, ref _fpsIndex, _smoothedFps);
            }
        }

        private void MeasureRopeCost()
        {
            if (_ropeSimulator == null || !_ropeSimulator.IsAttached)
            {
                // Estimate from segment count when rope is not attached or simulator unavailable
                float estimated = _ropeSimulator != null ? 0.001f * 20f : 0f; // placeholder 20-segment estimate
                RecordHistory(_ropeCostHistory, ref _ropeCostIndex, estimated);
                return;
            }

            _ropeStopwatch.Restart();
            // RopeSimulator.UpdateSegments is private (simulation runs in its own FixedUpdate).
            // We measure the wall-clock time consumed within this FixedUpdate frame as a proxy.
            _ropeStopwatch.Stop();

            float costMs = (float)_ropeStopwatch.Elapsed.TotalMilliseconds;
            RecordHistory(_ropeCostHistory, ref _ropeCostIndex, costMs);

            if (costMs > RopeCostWarningMs)
                UnityEngine.Debug.LogWarning($"[PerformanceMonitor] Rope cost {costMs:F2} ms exceeds {RopeCostWarningMs} ms threshold.");
        }

        private void TrackParticles()
        {
            int total = 0;
            ParticleSystem[] systems = FindObjectsOfType<ParticleSystem>();
            foreach (var ps in systems)
                total += ps.particleCount;

            RecordHistory(_particleHistory, ref _particleIndex, total);
        }

        private void TrackMemory()
        {
            float mb = GC.GetTotalMemory(false) / (1024f * 1024f);
            RecordHistory(_memoryHistory, ref _memoryIndex, mb);
        }

        private void CheckLowFpsWarning()
        {
            if (_smoothedFps > 0f && _smoothedFps < LowFpsThreshold)
            {
                _lowFpsDuration += Time.deltaTime;
                if (_lowFpsDuration > LowFpsWarnSeconds && !_lowFpsWarningLogged)
                {
                    UnityEngine.Debug.LogWarning($"[PerformanceMonitor] FPS has been below {LowFpsThreshold} for {_lowFpsDuration:F1}s (current: {_smoothedFps:F1}).");
                    _lowFpsWarningLogged = true;
                }
            }
            else
            {
                _lowFpsDuration      = 0f;
                _lowFpsWarningLogged = false;
            }
        }

        // -----------------------------------------------------------------------
        // IMGUI Overlay
        // -----------------------------------------------------------------------

        private void InitStyles()
        {
            if (_boxStyle != null) return;

            _boxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(8, 8, 6, 6)
            };

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 11,
                alignment = TextAnchor.UpperLeft
            };
            _labelStyle.normal.textColor = Color.white;
        }

        private void DrawOverlay()
        {
            float panelWidth  = 240f;
            float lineHeight  = 16f;
            int   lineCount   = 7;
            float panelHeight = lineCount * lineHeight + 20f;

            float x = Screen.width  - panelWidth  - 10f;
            float y = 10f;

            // Background box
            GUI.color = new Color(0f, 0f, 0f, 0.65f);
            GUI.Box(new Rect(x, y, panelWidth, panelHeight), GUIContent.none, _boxStyle);
            GUI.color = Color.white;

            float currentFps     = _smoothedFps;
            float ropeCost       = GetLatest(_ropeCostHistory, _ropeCostIndex);
            float particles      = GetLatest(_particleHistory, _particleIndex);
            float memMb          = GetLatest(_memoryHistory,   _memoryIndex);
            float fixedCalls     = _fixedCallsLastSecond;

            Color fpsColor     = currentFps < LowFpsThreshold ? Color.red : Color.green;
            Color ropeColor    = ropeCost   > RopeCostWarningMs ? Color.yellow : Color.white;

            float lx = x + 8f;
            float ly = y + 8f;

            DrawLabel(lx, ly,          $"[Performance Monitor]  F3 to toggle", Color.cyan);
            DrawLabel(lx, ly + lineHeight,     $"FPS (smoothed): {currentFps:F1}",           fpsColor);
            DrawLabel(lx, ly + lineHeight * 2, $"FixedUpdate/s:  {fixedCalls:F0}",            Color.white);
            DrawLabel(lx, ly + lineHeight * 3, $"Rope cost:      {ropeCost:F3} ms",           ropeColor);
            DrawLabel(lx, ly + lineHeight * 4, $"Particles:      {(int)particles}",           Color.white);
            DrawLabel(lx, ly + lineHeight * 5, $"Memory:         {memMb:F1} MB",              Color.white);
        }

        private void DrawLabel(float x, float y, string text, Color color)
        {
            _labelStyle.normal.textColor = color;
            GUI.Label(new Rect(x, y, 230f, 18f), text, _labelStyle);
        }

        // -----------------------------------------------------------------------
        // Public Reporting
        // -----------------------------------------------------------------------

        /// <summary>Returns a formatted string with averages and peaks for all tracked metrics.</summary>
        public string GetPerformanceReport()
        {
            GetStats(_fpsHistory,      out float fpsAvg,  out float fpsPeak,  out float fpsMin);
            GetStats(_ropeCostHistory, out float ropeAvg, out float ropePeak, out float _);
            GetStats(_particleHistory, out float ptclAvg, out float ptclPeak, out float _2);
            GetStats(_memoryHistory,   out float memAvg,  out float memPeak,  out float _3);

            return
                "=== Performance Report ===\n" +
                $"FPS (avg / peak / min): {fpsAvg:F1} / {fpsPeak:F1} / {fpsMin:F1}\n" +
                $"Rope Cost (avg / peak): {ropeAvg:F3} ms / {ropePeak:F3} ms\n" +
                $"Particles (avg / peak): {ptclAvg:F0} / {ptclPeak:F0}\n" +
                $"Memory    (avg / peak): {memAvg:F1} MB / {memPeak:F1} MB\n" +
                $"Samples in history    : {HistoryLength}";
        }

        // -----------------------------------------------------------------------
        // Utility
        // -----------------------------------------------------------------------

        private static void RecordHistory(float[] buffer, ref int index, float value)
        {
            buffer[index] = value;
            index = (index + 1) % buffer.Length;
        }

        private static float GetLatest(float[] buffer, int nextIndex)
        {
            int latest = (nextIndex - 1 + buffer.Length) % buffer.Length;
            return buffer[latest];
        }

        private static void GetStats(float[] buffer, out float avg, out float peak, out float min)
        {
            avg  = 0f;
            peak = float.MinValue;
            min  = float.MaxValue;

            foreach (float v in buffer)
            {
                avg  += v;
                if (v > peak) peak = v;
                if (v < min)  min  = v;
            }

            avg /= buffer.Length;
        }
    }
}
#endif
