using UnityEngine;
using UnityEngine.InputSystem;

namespace TitanAscent.Input
{
    /// <summary>
    /// Haptic feedback singleton for gamepad rumble.
    /// Call GamepadRumble.Instance.Play(profile) from any system.
    /// Rumble automatically stops after the profile duration.
    /// </summary>
    public class GamepadRumble : MonoBehaviour
    {
        // ── Rumble profiles ────────────────────────────────────────────────────
        public enum Profile
        {
            GrappleAttach,   // Short precise click
            GrappleRelease,  // Quick decay
            HardLanding,     // Heavy thud
            ThrustBurst,     // Light continuous while active
            SecondaryGrapple // Gentler click for secondary hook
        }

        // ── Singleton ──────────────────────────────────────────────────────────
        public static GamepadRumble Instance { get; private set; }

        // ── Inspector ──────────────────────────────────────────────────────────
        [Header("Master")]
        [SerializeField, Range(0f, 1f)] private float masterIntensity = 1f;
        [SerializeField] private bool rumbleEnabled = true;

        // ── Private state ──────────────────────────────────────────────────────
        private float _stopTime = 0f;
        private float _lowFreq  = 0f;
        private float _highFreq = 0f;

        // ── Profile data ───────────────────────────────────────────────────────
        // (lowFreq, highFreq, duration)
        private static readonly (float low, float high, float duration)[] ProfileData =
        {
            (0.20f, 0.60f, 0.12f),  // GrappleAttach
            (0.15f, 0.30f, 0.10f),  // GrappleRelease
            (0.85f, 0.40f, 0.35f),  // HardLanding
            (0.05f, 0.20f, 0.08f),  // ThrustBurst
            (0.10f, 0.50f, 0.10f),  // SecondaryGrapple
        };

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            if (!rumbleEnabled) return;

            if (_stopTime > 0f && Time.time >= _stopTime)
                StopRumble();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) StopRumble();
        }

        private void OnDestroy()
        {
            StopRumble();
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>Triggers a predefined haptic profile on the current gamepad.</summary>
        public void Play(Profile profile)
        {
            if (!rumbleEnabled) return;

            Gamepad gamepad = Gamepad.current;
            if (gamepad == null) return;

            var (low, high, duration) = ProfileData[(int)profile];
            _lowFreq  = low  * masterIntensity;
            _highFreq = high * masterIntensity;
            _stopTime = Time.time + duration;

            gamepad.SetMotorSpeeds(_lowFreq, _highFreq);
        }

        /// <summary>Sets rumble speeds directly (0–1). Caller must call StopRumble() when done.</summary>
        public void PlayRaw(float lowFreq, float highFreq)
        {
            if (!rumbleEnabled) return;

            Gamepad gamepad = Gamepad.current;
            if (gamepad == null) return;

            _lowFreq  = Mathf.Clamp01(lowFreq  * masterIntensity);
            _highFreq = Mathf.Clamp01(highFreq * masterIntensity);
            _stopTime = 0f; // caller manages lifetime

            gamepad.SetMotorSpeeds(_lowFreq, _highFreq);
        }

        /// <summary>Immediately stops all rumble on the current gamepad.</summary>
        public void StopRumble()
        {
            _stopTime = 0f;
            _lowFreq  = 0f;
            _highFreq = 0f;

            Gamepad gamepad = Gamepad.current;
            gamepad?.SetMotorSpeeds(0f, 0f);
        }

        /// <summary>Whether haptic feedback is currently enabled.</summary>
        public bool RumbleEnabled
        {
            get => rumbleEnabled;
            set
            {
                rumbleEnabled = value;
                if (!rumbleEnabled) StopRumble();
            }
        }
    }
}
