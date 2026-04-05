using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TitanAscent.Audio;

namespace TitanAscent.UI
{
    public class SettingsManager : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        // PlayerPrefs Keys
        // -----------------------------------------------------------------------

        private const string KeyMasterVolume      = "Settings_MasterVolume";
        private const string KeyMusicVolume       = "Settings_MusicVolume";
        private const string KeySFXVolume         = "Settings_SFXVolume";
        private const string KeyNarrationVolume   = "Settings_NarrationVolume";
        private const string KeyNarrationSubs     = "Settings_NarrationSubtitles";
        private const string KeyMouseSensitivity  = "Settings_MouseSensitivity";
        private const string KeyInvertY           = "Settings_InvertY";
        private const string KeyQualityLevel      = "Settings_QualityLevel";
        private const string KeyFullScreen        = "Settings_FullScreen";
        private const string KeyVSync             = "Settings_VSync";
        private const string KeyTargetFrameRate   = "Settings_TargetFrameRate";

        // -----------------------------------------------------------------------
        // Default Values
        // -----------------------------------------------------------------------

        private const float DefaultMasterVolume     = 0.9f;
        private const float DefaultMusicVolume      = 0.7f;
        private const float DefaultSFXVolume        = 1.0f;
        private const float DefaultNarrationVolume  = 1.0f;
        private const bool  DefaultNarrationSubs    = true;
        private const float DefaultMouseSensitivity = 1.0f;
        private const bool  DefaultInvertY          = false;
        private const int   DefaultQualityLevel     = 3;
        private const bool  DefaultFullScreen       = true;
        private const bool  DefaultVSync            = true;
        private const int   DefaultTargetFrameRate  = 60;

        // -----------------------------------------------------------------------
        // Runtime Settings
        // -----------------------------------------------------------------------

        public float MasterVolume     { get; private set; }
        public float MusicVolume      { get; private set; }
        public float SFXVolume        { get; private set; }
        public float NarrationVolume  { get; private set; }
        public bool  NarrationSubtitles { get; private set; }
        public float MouseSensitivity { get; private set; }
        public bool  InvertY          { get; private set; }
        public int   QualityLevel     { get; private set; }
        public bool  FullScreen       { get; private set; }
        public bool  VSync            { get; private set; }
        public int   TargetFrameRate  { get; private set; }

        // -----------------------------------------------------------------------
        // UI References
        // -----------------------------------------------------------------------

        [Header("Volume Sliders")]
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private Slider narrationVolumeSlider;

        [Header("Audio Toggles")]
        [SerializeField] private Toggle narrationSubtitlesToggle;

        [Header("Input")]
        [SerializeField] private Slider mouseSensitivitySlider;
        [SerializeField] private Toggle invertYToggle;

        [Header("Graphics")]
        [SerializeField] private Slider qualityLevelSlider;
        [SerializeField] private Toggle fullScreenToggle;
        [SerializeField] private Toggle vSyncToggle;
        [SerializeField] private TMP_Dropdown targetFrameRateDropdown;

        [Header("Buttons")]
        [SerializeField] private Button closeButton;
        [SerializeField] private Button resetDefaultsButton;

        [Header("Panel")]
        [SerializeField] private GameObject settingsPanel;

        // Valid frame-rate options matching the dropdown order
        private static readonly int[] FrameRateOptions = { 30, 60, 120, 144, -1 };

        // -----------------------------------------------------------------------
        // Lifecycle
        // -----------------------------------------------------------------------

        private void Awake()
        {
            Load();
        }

        private void Start()
        {
            PushValuesToUI();
            RegisterUICallbacks();

            if (closeButton        != null) closeButton.onClick.AddListener(OnCloseClicked);
            if (resetDefaultsButton!= null) resetDefaultsButton.onClick.AddListener(OnResetDefaultsClicked);
        }

        // -----------------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------------

        public void Save()
        {
            PlayerPrefs.SetFloat(KeyMasterVolume,     MasterVolume);
            PlayerPrefs.SetFloat(KeyMusicVolume,      MusicVolume);
            PlayerPrefs.SetFloat(KeySFXVolume,        SFXVolume);
            PlayerPrefs.SetFloat(KeyNarrationVolume,  NarrationVolume);
            PlayerPrefs.SetInt  (KeyNarrationSubs,    NarrationSubtitles ? 1 : 0);
            PlayerPrefs.SetFloat(KeyMouseSensitivity, MouseSensitivity);
            PlayerPrefs.SetInt  (KeyInvertY,          InvertY ? 1 : 0);
            PlayerPrefs.SetInt  (KeyQualityLevel,     QualityLevel);
            PlayerPrefs.SetInt  (KeyFullScreen,       FullScreen ? 1 : 0);
            PlayerPrefs.SetInt  (KeyVSync,            VSync ? 1 : 0);
            PlayerPrefs.SetInt  (KeyTargetFrameRate,  TargetFrameRate);
            PlayerPrefs.Save();
        }

        public void Load()
        {
            MasterVolume      = PlayerPrefs.GetFloat(KeyMasterVolume,     DefaultMasterVolume);
            MusicVolume       = PlayerPrefs.GetFloat(KeyMusicVolume,      DefaultMusicVolume);
            SFXVolume         = PlayerPrefs.GetFloat(KeySFXVolume,        DefaultSFXVolume);
            NarrationVolume   = PlayerPrefs.GetFloat(KeyNarrationVolume,  DefaultNarrationVolume);
            NarrationSubtitles= PlayerPrefs.GetInt  (KeyNarrationSubs,    DefaultNarrationSubs ? 1 : 0) == 1;
            MouseSensitivity  = PlayerPrefs.GetFloat(KeyMouseSensitivity, DefaultMouseSensitivity);
            InvertY           = PlayerPrefs.GetInt  (KeyInvertY,          DefaultInvertY ? 1 : 0) == 1;
            QualityLevel      = PlayerPrefs.GetInt  (KeyQualityLevel,     DefaultQualityLevel);
            FullScreen        = PlayerPrefs.GetInt  (KeyFullScreen,       DefaultFullScreen ? 1 : 0) == 1;
            VSync             = PlayerPrefs.GetInt  (KeyVSync,            DefaultVSync ? 1 : 0) == 1;
            TargetFrameRate   = PlayerPrefs.GetInt  (KeyTargetFrameRate,  DefaultTargetFrameRate);

            ApplyAll();
        }

        public void ApplyAll()
        {
            // Audio
            AudioListener.volume = MasterVolume;

            AudioManager audioMgr = AudioManager.Instance;
            if (audioMgr != null)
            {
                // Scale individual channel groups via master; specific music/SFX/narration
                // channels are driven by their base volume multiplied by the category volume.
                // AudioManager exposes SetChannelVolume per-channel; we apply a global scale
                // through AudioListener.volume and channel multipliers for sub-categories.
                audioMgr.SetChannelVolume(AudioChannel.Wind,          SFXVolume);
                audioMgr.SetChannelVolume(AudioChannel.RopeTension,   SFXVolume);
                audioMgr.SetChannelVolume(AudioChannel.GrappleFire,   SFXVolume);
                audioMgr.SetChannelVolume(AudioChannel.GrappleImpact, SFXVolume);
                audioMgr.SetChannelVolume(AudioChannel.ThrusterBurst, SFXVolume);
                audioMgr.SetChannelVolume(AudioChannel.FallWhoosh,    SFXVolume);
                audioMgr.SetChannelVolume(AudioChannel.SurfaceScrape, SFXVolume);
                audioMgr.SetChannelVolume(AudioChannel.TitanBreathing,NarrationVolume);
            }

            // Graphics
            int clampedQuality = Mathf.Clamp(QualityLevel, 0, QualitySettings.names.Length - 1);
            QualitySettings.SetQualityLevel(clampedQuality, true);

            QualitySettings.vSyncCount = VSync ? 1 : 0;

            Application.targetFrameRate = TargetFrameRate;

            Screen.fullScreen = FullScreen;
        }

        public void ResetToDefaults()
        {
            MasterVolume      = DefaultMasterVolume;
            MusicVolume       = DefaultMusicVolume;
            SFXVolume         = DefaultSFXVolume;
            NarrationVolume   = DefaultNarrationVolume;
            NarrationSubtitles= DefaultNarrationSubs;
            MouseSensitivity  = DefaultMouseSensitivity;
            InvertY           = DefaultInvertY;
            QualityLevel      = DefaultQualityLevel;
            FullScreen        = DefaultFullScreen;
            VSync             = DefaultVSync;
            TargetFrameRate   = DefaultTargetFrameRate;

            PushValuesToUI();
            ApplyAll();
            Save();
        }

        // -----------------------------------------------------------------------
        // UI Sync
        // -----------------------------------------------------------------------

        private void PushValuesToUI()
        {
            SetSliderSilent(masterVolumeSlider,    MasterVolume);
            SetSliderSilent(musicVolumeSlider,     MusicVolume);
            SetSliderSilent(sfxVolumeSlider,       SFXVolume);
            SetSliderSilent(narrationVolumeSlider, NarrationVolume);

            SetToggleSilent(narrationSubtitlesToggle, NarrationSubtitles);

            SetSliderSilent(mouseSensitivitySlider, MouseSensitivity);
            SetToggleSilent(invertYToggle, InvertY);

            if (qualityLevelSlider != null)
                SetSliderSilent(qualityLevelSlider, QualityLevel);

            SetToggleSilent(fullScreenToggle, FullScreen);
            SetToggleSilent(vSyncToggle, VSync);

            if (targetFrameRateDropdown != null)
            {
                int idx = System.Array.IndexOf(FrameRateOptions, TargetFrameRate);
                if (idx < 0) idx = 1; // default to 60fps slot
                targetFrameRateDropdown.SetValueWithoutNotify(idx);
            }
        }

        private void RegisterUICallbacks()
        {
            if (masterVolumeSlider    != null) masterVolumeSlider.onValueChanged.AddListener(v  => { MasterVolume     = v;  ApplyAll(); });
            if (musicVolumeSlider     != null) musicVolumeSlider.onValueChanged.AddListener(v   => { MusicVolume      = v;  ApplyAll(); });
            if (sfxVolumeSlider       != null) sfxVolumeSlider.onValueChanged.AddListener(v     => { SFXVolume        = v;  ApplyAll(); });
            if (narrationVolumeSlider != null) narrationVolumeSlider.onValueChanged.AddListener(v=> { NarrationVolume  = v;  ApplyAll(); });

            if (narrationSubtitlesToggle != null)
                narrationSubtitlesToggle.onValueChanged.AddListener(v => { NarrationSubtitles = v; });

            if (mouseSensitivitySlider != null)
                mouseSensitivitySlider.onValueChanged.AddListener(v => MouseSensitivity = v);

            if (invertYToggle != null)
                invertYToggle.onValueChanged.AddListener(v => InvertY = v);

            if (qualityLevelSlider != null)
                qualityLevelSlider.onValueChanged.AddListener(v => { QualityLevel = Mathf.RoundToInt(v); ApplyAll(); });

            if (fullScreenToggle != null)
                fullScreenToggle.onValueChanged.AddListener(v => { FullScreen = v; ApplyAll(); });

            if (vSyncToggle != null)
                vSyncToggle.onValueChanged.AddListener(v => { VSync = v; ApplyAll(); });

            if (targetFrameRateDropdown != null)
                targetFrameRateDropdown.onValueChanged.AddListener(idx =>
                {
                    TargetFrameRate = (idx >= 0 && idx < FrameRateOptions.Length)
                        ? FrameRateOptions[idx]
                        : DefaultTargetFrameRate;
                    ApplyAll();
                });
        }

        // -----------------------------------------------------------------------
        // Button Handlers
        // -----------------------------------------------------------------------

        private void OnCloseClicked()
        {
            Save();
            if (settingsPanel != null)
                settingsPanel.SetActive(false);
        }

        private void OnResetDefaultsClicked()
        {
            ResetToDefaults();
        }

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        private static void SetSliderSilent(Slider slider, float value)
        {
            if (slider != null) slider.SetValueWithoutNotify(value);
        }

        private static void SetToggleSilent(Toggle toggle, bool value)
        {
            if (toggle != null) toggle.SetIsOnWithoutNotify(value);
        }
    }
}
