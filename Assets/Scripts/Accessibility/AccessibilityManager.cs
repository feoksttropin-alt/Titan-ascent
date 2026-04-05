using System;
using System.Collections.Generic;
using UnityEngine;
using TitanAscent.Systems;
using TitanAscent.Environment;
using TitanAscent.UI;

namespace TitanAscent.Accessibility
{
    public enum ColorBlindMode
    {
        None,
        Deuteranopia,
        Protanopia,
        Tritanopia
    }

    /// <summary>
    /// Central singleton that stores, persists, and applies all accessibility settings.
    /// </summary>
    public class AccessibilityManager : MonoBehaviour
    {
        // ── Singleton ──────────────────────────────────────────────────────────
        public static AccessibilityManager Instance { get; private set; }

        // ── Events ─────────────────────────────────────────────────────────────
        public static event Action OnAccessibilitySettingsChanged;

        // ── PlayerPrefs keys ───────────────────────────────────────────────────
        private const string PrefPrefix        = "Accessibility_";
        private const string PrefReduceMotion  = PrefPrefix + "ReduceMotion";
        private const string PrefHighContrast  = PrefPrefix + "HighContrastAnchors";
        private const string PrefLargeSubs     = PrefPrefix + "LargeSubtitles";
        private const string PrefSubBackground = PrefPrefix + "SubtitleBackground";
        private const string PrefColorBlind    = PrefPrefix + "ColorBlindMode";
        private const string PrefReduceParticles = PrefPrefix + "ReduceParticles";
        private const string PrefSlowFallCam   = PrefPrefix + "SlowFallCamera";
        private const string PrefNarrationOnly = PrefPrefix + "NarrationOnly";

        // ── Settings ───────────────────────────────────────────────────────────
        public bool ReduceMotion       { get; private set; } = false;
        public bool HighContrastAnchors{ get; private set; } = false;
        public bool LargeSubtitles     { get; private set; } = false;
        public bool SubtitleBackground { get; private set; } = true;
        public ColorBlindMode ColorBlindMode { get; private set; } = ColorBlindMode.None;
        public bool ReduceParticles    { get; private set; } = false;
        public bool SlowFallCamera     { get; private set; } = false;
        public bool NarrationOnly      { get; private set; } = false;

        // ── Cached references ──────────────────────────────────────────────────
        [Header("Optional Runtime References")]
        [SerializeField] private JuiceController juiceController;
        [SerializeField] private NarrationUI narrationUI;
        [SerializeField] private ColorBlindFilter colorBlindFilter;

        // ── Normal/large subtitle font sizes ──────────────────────────────────
        private const float NormalFontSize = 18f;
        private const float LargeFontSize  = 28f;

        // ──────────────────────────────────────────────────────────────────────
        // Lifecycle
        // ──────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            LoadAll();
        }

        private void Start()
        {
            ApplyAll();
        }

        // ──────────────────────────────────────────────────────────────────────
        // Load / Save
        // ──────────────────────────────────────────────────────────────────────

        private void LoadAll()
        {
            ReduceMotion        = PlayerPrefs.GetInt(PrefReduceMotion,  0) == 1;
            HighContrastAnchors = PlayerPrefs.GetInt(PrefHighContrast,  0) == 1;
            LargeSubtitles      = PlayerPrefs.GetInt(PrefLargeSubs,     0) == 1;
            SubtitleBackground  = PlayerPrefs.GetInt(PrefSubBackground, 1) == 1;
            ColorBlindMode      = (ColorBlindMode)PlayerPrefs.GetInt(PrefColorBlind, 0);
            ReduceParticles     = PlayerPrefs.GetInt(PrefReduceParticles, 0) == 1;
            SlowFallCamera      = PlayerPrefs.GetInt(PrefSlowFallCam,   0) == 1;
            NarrationOnly       = PlayerPrefs.GetInt(PrefNarrationOnly, 0) == 1;
        }

        private void SaveAll()
        {
            PlayerPrefs.SetInt(PrefReduceMotion,   ReduceMotion        ? 1 : 0);
            PlayerPrefs.SetInt(PrefHighContrast,   HighContrastAnchors ? 1 : 0);
            PlayerPrefs.SetInt(PrefLargeSubs,      LargeSubtitles      ? 1 : 0);
            PlayerPrefs.SetInt(PrefSubBackground,  SubtitleBackground  ? 1 : 0);
            PlayerPrefs.SetInt(PrefColorBlind,     (int)ColorBlindMode);
            PlayerPrefs.SetInt(PrefReduceParticles,ReduceParticles     ? 1 : 0);
            PlayerPrefs.SetInt(PrefSlowFallCam,    SlowFallCamera      ? 1 : 0);
            PlayerPrefs.SetInt(PrefNarrationOnly,  NarrationOnly       ? 1 : 0);
            PlayerPrefs.Save();
        }

        // ──────────────────────────────────────────────────────────────────────
        // Public API
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Pushes every setting to every relevant system.</summary>
        public void ApplyAll()
        {
            ApplyReduceMotion(ReduceMotion);
            ApplyHighContrastAnchors(HighContrastAnchors);
            ApplyLargeSubtitles(LargeSubtitles);
            ApplySubtitleBackground(SubtitleBackground);
            ApplyColorBlindMode(ColorBlindMode);
            ApplyReduceParticles(ReduceParticles);
            ApplySlowFallCamera(SlowFallCamera);
            ApplyNarrationOnly(NarrationOnly);

            OnAccessibilitySettingsChanged?.Invoke();
        }

        /// <summary>Disables JuiceController shake and flash effects.</summary>
        public void SetReduceMotion(bool value)
        {
            ReduceMotion = value;
            PlayerPrefs.SetInt(PrefReduceMotion, value ? 1 : 0);
            ApplyReduceMotion(value);
            OnAccessibilitySettingsChanged?.Invoke();
        }

        /// <summary>Calls SetHighContrastMode on all SurfaceAnchorPoint instances.</summary>
        public void SetHighContrastAnchors(bool value)
        {
            HighContrastAnchors = value;
            PlayerPrefs.SetInt(PrefHighContrast, value ? 1 : 0);
            ApplyHighContrastAnchors(value);
            OnAccessibilitySettingsChanged?.Invoke();
        }

        public void SetLargeSubtitles(bool value)
        {
            LargeSubtitles = value;
            PlayerPrefs.SetInt(PrefLargeSubs, value ? 1 : 0);
            ApplyLargeSubtitles(value);
            OnAccessibilitySettingsChanged?.Invoke();
        }

        public void SetSubtitleBackground(bool value)
        {
            SubtitleBackground = value;
            PlayerPrefs.SetInt(PrefSubBackground, value ? 1 : 0);
            ApplySubtitleBackground(value);
            OnAccessibilitySettingsChanged?.Invoke();
        }

        /// <summary>Adjusts color palettes across UI and VFX.</summary>
        public void SetColorBlindMode(ColorBlindMode mode)
        {
            ColorBlindMode = mode;
            PlayerPrefs.SetInt(PrefColorBlind, (int)mode);
            ApplyColorBlindMode(mode);
            OnAccessibilitySettingsChanged?.Invoke();
        }

        public void SetReduceParticles(bool value)
        {
            ReduceParticles = value;
            PlayerPrefs.SetInt(PrefReduceParticles, value ? 1 : 0);
            ApplyReduceParticles(value);
            OnAccessibilitySettingsChanged?.Invoke();
        }

        public void SetSlowFallCamera(bool value)
        {
            SlowFallCamera = value;
            PlayerPrefs.SetInt(PrefSlowFallCam, value ? 1 : 0);
            ApplySlowFallCamera(value);
            OnAccessibilitySettingsChanged?.Invoke();
        }

        public void SetNarrationOnly(bool value)
        {
            NarrationOnly = value;
            PlayerPrefs.SetInt(PrefNarrationOnly, value ? 1 : 0);
            ApplyNarrationOnly(value);
            OnAccessibilitySettingsChanged?.Invoke();
        }

        // ──────────────────────────────────────────────────────────────────────
        // Internal application helpers
        // ──────────────────────────────────────────────────────────────────────

        private void ApplyReduceMotion(bool value)
        {
            // Resolve JuiceController lazily if not assigned in Inspector
            if (juiceController == null)
                juiceController = FindObjectOfType<JuiceController>();

            if (juiceController != null)
                juiceController.SetReduceMotionEnabled(value);
        }

        private void ApplyHighContrastAnchors(bool value)
        {
            SurfaceAnchorPoint[] anchors = FindObjectsOfType<SurfaceAnchorPoint>();
            foreach (SurfaceAnchorPoint anchor in anchors)
                anchor.SetHighContrastMode(value);
        }

        private void ApplyLargeSubtitles(bool value)
        {
            if (narrationUI == null)
                narrationUI = FindObjectOfType<NarrationUI>();

            if (narrationUI != null)
                narrationUI.SetFontSize(value ? LargeFontSize : NormalFontSize);
        }

        private void ApplySubtitleBackground(bool value)
        {
            if (narrationUI == null)
                narrationUI = FindObjectOfType<NarrationUI>();

            if (narrationUI != null)
                narrationUI.SetBackgroundVisible(value);
        }

        private void ApplyColorBlindMode(ColorBlindMode mode)
        {
            if (colorBlindFilter == null)
                colorBlindFilter = FindObjectOfType<ColorBlindFilter>();

            if (colorBlindFilter != null)
                colorBlindFilter.ApplyMode(mode);
        }

        private void ApplyReduceParticles(bool value)
        {
            // Find all ParticleSystems and scale their emission rates
            ParticleSystem[] systems = FindObjectsOfType<ParticleSystem>();
            foreach (ParticleSystem ps in systems)
            {
                var emission = ps.emission;
                // Store original rate in a userData workaround via name lookup isn't reliable;
                // instead we use a helper component to track the original value.
                ParticleEmissionRecord record = ps.GetComponent<ParticleEmissionRecord>();
                if (record == null)
                {
                    record = ps.gameObject.AddComponent<ParticleEmissionRecord>();
                    record.OriginalRateOverTime = emission.rateOverTime.constantMax;
                    record.OriginalRateOverDistance = emission.rateOverDistance.constantMax;
                }

                var rateOverTime = emission.rateOverTime;
                rateOverTime.constantMax = value
                    ? record.OriginalRateOverTime * 0.25f
                    : record.OriginalRateOverTime;
                emission.rateOverTime = rateOverTime;

                var rateOverDistance = emission.rateOverDistance;
                rateOverDistance.constantMax = value
                    ? record.OriginalRateOverDistance * 0.25f
                    : record.OriginalRateOverDistance;
                emission.rateOverDistance = rateOverDistance;
            }
        }

        private void ApplySlowFallCamera(bool value)
        {
            // Broadcast to CameraController via static property / event
            CameraAccessibilityBridge.SlowFallCameraEnabled = value;
        }

        private void ApplyNarrationOnly(bool value)
        {
            // Broadcast to NarrationSystem so it shows subtitles for all events
            NarrationAccessibilityBridge.NarrationOnlyEnabled = value;
        }

        private void OnDestroy()
        {
            SaveAll();
        }
    }

    // ── Lightweight bridge classes (avoids hard coupling) ──────────────────────

    /// <summary>
    /// Static bridge so CameraController can query the slow-fall setting without
    /// taking a direct dependency on AccessibilityManager.
    /// </summary>
    public static class CameraAccessibilityBridge
    {
        public static bool SlowFallCameraEnabled { get; set; } = false;
    }

    /// <summary>
    /// Static bridge so NarrationSystem can query the narration-only setting.
    /// </summary>
    public static class NarrationAccessibilityBridge
    {
        public static bool NarrationOnlyEnabled { get; set; } = false;
    }

    /// <summary>
    /// Lightweight component that records the original emission rates of a
    /// ParticleSystem before ReduceParticles modifies them.
    /// </summary>
    [AddComponentMenu("")] // Hide from Add Component menu
    public class ParticleEmissionRecord : MonoBehaviour
    {
        public float OriginalRateOverTime     = 10f;
        public float OriginalRateOverDistance = 0f;
    }
}
