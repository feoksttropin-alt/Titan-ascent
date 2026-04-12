using UnityEngine;
using TitanAscent.UI;

namespace TitanAscent.Audio
{
    /// <summary>
    /// Singleton that plays UI sound effects. All methods are safe to call
    /// from any script; volume is scaled by SettingsManager values when available.
    /// </summary>
    public class UIAudioController : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        // Singleton
        // -----------------------------------------------------------------------

        public static UIAudioController Instance { get; private set; }

        // -----------------------------------------------------------------------
        // Inspector
        // -----------------------------------------------------------------------

        [Header("UI Clips")]
        [SerializeField] private AudioClip clipButtonClick;
        [SerializeField] private AudioClip clipButtonHover;
        [SerializeField] private AudioClip clipMenuOpen;
        [SerializeField] private AudioClip clipMenuClose;
        [SerializeField] private AudioClip clipAchievementUnlock;
        [SerializeField] private AudioClip clipNewRecord;
        [SerializeField] private AudioClip clipError;
        [SerializeField] private AudioClip clipConfirm;
        [SerializeField] private AudioClip clipZoneName;
        [SerializeField] private AudioClip clipHUDFlash;

        // -----------------------------------------------------------------------
        // Private
        // -----------------------------------------------------------------------

        private AudioSource _audioSource;

        // -----------------------------------------------------------------------
        // Lifecycle
        // -----------------------------------------------------------------------

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            _audioSource              = gameObject.AddComponent<AudioSource>();
            _audioSource.spatialBlend = 0f;
            _audioSource.playOnAwake  = false;
        }

        // -----------------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------------

        public void PlayButtonClick()         => Play(clipButtonClick);
        public void PlayButtonHover()         => Play(clipButtonHover);
        public void PlayMenuOpen()            => Play(clipMenuOpen);
        public void PlayMenuClose()           => Play(clipMenuClose);
        public void PlayAchievementUnlock()   => Play(clipAchievementUnlock);
        public void PlayNewRecord()           => Play(clipNewRecord);
        public void PlayError()               => Play(clipError);
        public void PlayConfirm()             => Play(clipConfirm);
        public void PlayZoneName()            => Play(clipZoneName);
        public void PlayHUDFlash()            => Play(clipHUDFlash);

        // -----------------------------------------------------------------------
        // Internal
        // -----------------------------------------------------------------------

        private void Play(AudioClip clip)
        {
            if (clip == null || _audioSource == null) return;
            _audioSource.PlayOneShot(clip, GetVolume());
        }

        private float GetVolume()
        {
            SettingsManager sm = FindFirstObjectByType<SettingsManager>();
            if (sm == null) return 1f;
            return Mathf.Clamp01(sm.MasterVolume * sm.SFXVolume);
        }
    }
}
