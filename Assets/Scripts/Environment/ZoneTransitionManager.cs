using System.Collections;
using UnityEngine;
using TMPro;
using TitanAscent.Data;
using TitanAscent.Systems;

namespace TitanAscent.Environment
{
    /// <summary>
    /// Handles visual and audio transitions between the 9 zones and manages
    /// continuous altitude-based ambient effects (sky colour, fog, temperature tint).
    /// </summary>
    public class ZoneTransitionManager : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        // Inspector References
        // -----------------------------------------------------------------------

        [Header("Scene References")]
        [SerializeField] private Light ambientLight;
        [SerializeField] private TextMeshProUGUI zoneNameText;
        [SerializeField] private CanvasGroup zoneNameGroup;

        [Header("Zone Data")]
        [SerializeField] private ZoneData[] allZones;

        // -----------------------------------------------------------------------
        // Altitude-based sky colour gradient
        // -----------------------------------------------------------------------

        [Header("Sky Gradient (altitude-based)")]
        [SerializeField] private Gradient skyColorGradient;

        // -----------------------------------------------------------------------
        // Constants
        // -----------------------------------------------------------------------

        private const float AmbientFadeDuration  = 8f;
        private const float FogFadeDuration      = 10f;
        private const float WindFadeDuration     = 5f;
        private const float ZoneNameDisplayTime  = 2f;
        private const float StormFogMinAltitude  = 7000f;
        private const float MaxAltitude          = 10000f;

        // -----------------------------------------------------------------------
        // Private State
        // -----------------------------------------------------------------------

        private ZoneManager   _zoneManager;
        private WindSystem    _windSystem;
        private NarrationSystem _narration;

        private Coroutine _ambientFadeCoroutine;
        private Coroutine _fogFadeCoroutine;
        private Coroutine _windFadeCoroutine;
        private Coroutine _zoneNameCoroutine;

        private Color  _targetAmbientColor;
        private float  _targetFogDensity;
        private float  _targetWindMultiplier;

        private float  _currentAltitude;

        // -----------------------------------------------------------------------
        // Lifecycle
        // -----------------------------------------------------------------------

        private void Awake()
        {
            _zoneManager = FindFirstObjectByType<ZoneManager>();
            _windSystem  = FindFirstObjectByType<WindSystem>();
            _narration   = FindFirstObjectByType<NarrationSystem>();

            if (_zoneManager != null)
                _zoneManager.OnZoneChanged.AddListener(OnZoneChanged);

            // Build a default sky gradient if none assigned
            if (skyColorGradient == null || skyColorGradient.colorKeys.Length == 0)
                skyColorGradient = BuildDefaultSkyGradient();

            if (zoneNameGroup != null)
                zoneNameGroup.alpha = 0f;
        }

        private void OnDestroy()
        {
            if (_zoneManager != null)
                _zoneManager.OnZoneChanged.RemoveListener(OnZoneChanged);
        }

        private void Update()
        {
            TrackAltitude();
            ApplyAltitudeEffects();
        }

        // -----------------------------------------------------------------------
        // Zone Change Handler
        // -----------------------------------------------------------------------

        private void OnZoneChanged(TitanZone previous, TitanZone newZone)
        {
            if (newZone == null) return;

            ZoneData data = FindZoneData(newZone.name);

            // 1. Fade ambient light colour
            Color targetAmbient = data != null ? data.ambientLightColor : newZone.ambientColor;
            StartFade(ref _ambientFadeCoroutine, FadeAmbientLight(targetAmbient, AmbientFadeDuration));

            // 2. Adjust fog density
            float targetFog = data != null ? data.fogDensity : 0.02f;
            StartFade(ref _fogFadeCoroutine, FadeFogDensity(targetFog, FogFadeDuration));

            // 3. Narration
            if (data != null && data.narrationUnlocked && _narration != null)
            {
                if (data.zoneNarrationLines != null && data.zoneNarrationLines.Length > 0)
                {
                    // Entry narration line (pick first for zone entry)
                    _narration.TriggerClimbStart(); // closest generic trigger; extend NarrationSystem for zone-specific lines
                }
            }

            // 4. Zone name display
            if (zoneNameText != null && zoneNameGroup != null)
            {
                if (_zoneNameCoroutine != null) StopCoroutine(_zoneNameCoroutine);
                _zoneNameCoroutine = StartCoroutine(DisplayZoneName(newZone.name, ZoneNameDisplayTime));
            }

            // 5. Wind multiplier
            float targetWind = data != null ? data.windMultiplier : newZone.windStrength;
            StartFade(ref _windFadeCoroutine, FadeWindMultiplier(targetWind, WindFadeDuration));
        }

        // -----------------------------------------------------------------------
        // Altitude-based Continuous Effects
        // -----------------------------------------------------------------------

        private void TrackAltitude()
        {
            var player = FindFirstObjectByType<Player.PlayerController>();
            if (player != null)
                _currentAltitude = player.CurrentHeight;
        }

        private void ApplyAltitudeEffects()
        {
            // Sky colour from gradient
            float altFraction = Mathf.Clamp01(_currentAltitude / MaxAltitude);
            Color skyColor    = skyColorGradient.Evaluate(altFraction);

            if (ambientLight != null)
            {
                // Blend on top of zone ambient fade — only when no zone fade is running
                if (_ambientFadeCoroutine == null)
                    ambientLight.color = Color.Lerp(ambientLight.color, skyColor, Time.deltaTime * 0.5f);
            }

            // Thicken fog above 7000 m
            if (_fogFadeCoroutine == null && _currentAltitude > StormFogMinAltitude)
            {
                float extraFog   = Mathf.InverseLerp(StormFogMinAltitude, MaxAltitude, _currentAltitude);
                float targetDens = Mathf.Lerp(RenderSettings.fogDensity, 0.06f, extraFog * Time.deltaTime * 0.2f);
                RenderSettings.fogDensity = targetDens;
            }
        }

        // -----------------------------------------------------------------------
        // Coroutines — Fades
        // -----------------------------------------------------------------------

        private IEnumerator FadeAmbientLight(Color target, float duration)
        {
            if (ambientLight == null) yield break;

            Color start   = ambientLight.color;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed               += Time.deltaTime;
                ambientLight.color     = Color.Lerp(start, target, elapsed / duration);
                RenderSettings.ambientLight = ambientLight.color;
                yield return null;
            }

            ambientLight.color          = target;
            RenderSettings.ambientLight = target;
            _ambientFadeCoroutine       = null;
        }

        private IEnumerator FadeFogDensity(float target, float duration)
        {
            float start   = RenderSettings.fogDensity;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed                   += Time.deltaTime;
                RenderSettings.fogDensity  = Mathf.Lerp(start, target, elapsed / duration);
                yield return null;
            }

            RenderSettings.fogDensity = target;
            _fogFadeCoroutine         = null;
        }

        private IEnumerator FadeWindMultiplier(float target, float duration)
        {
            if (_windSystem == null) yield break;

            float start   = target; // We don't have a public getter; start from target for now
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t  = elapsed / duration;
                _windSystem.SetGlobalWindMultiplier(Mathf.Lerp(1f, target, t));
                yield return null;
            }

            _windSystem.SetGlobalWindMultiplier(target);
            _windFadeCoroutine = null;
        }

        private IEnumerator DisplayZoneName(string zoneName, float holdTime)
        {
            if (zoneNameText == null || zoneNameGroup == null) yield break;

            zoneNameText.text    = zoneName.ToUpper();
            zoneNameGroup.alpha  = 0f;

            // Fade in
            float fadeInTime = 0.4f;
            float elapsed    = 0f;
            while (elapsed < fadeInTime)
            {
                elapsed           += Time.deltaTime;
                zoneNameGroup.alpha = Mathf.Clamp01(elapsed / fadeInTime);
                yield return null;
            }

            zoneNameGroup.alpha = 1f;

            // Hold
            yield return new WaitForSeconds(holdTime);

            // Fade out
            float fadeOutTime = 0.6f;
            elapsed = 0f;
            while (elapsed < fadeOutTime)
            {
                elapsed            += Time.deltaTime;
                zoneNameGroup.alpha = Mathf.Clamp01(1f - elapsed / fadeOutTime);
                yield return null;
            }

            zoneNameGroup.alpha = 0f;
            _zoneNameCoroutine  = null;
        }

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        private void StartFade(ref Coroutine handle, IEnumerator routine)
        {
            if (handle != null) StopCoroutine(handle);
            handle = StartCoroutine(routine);
        }

        private ZoneData FindZoneData(string zoneName)
        {
            if (allZones == null) return null;
            foreach (var zd in allZones)
            {
                if (zd != null && zd.zoneName == zoneName)
                    return zd;
            }
            return null;
        }

        private static Gradient BuildDefaultSkyGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    // 0 m — dawn orange
                    new GradientColorKey(new Color(1f, 0.55f, 0.2f), 0f),
                    // 2000 m — midday blue
                    new GradientColorKey(new Color(0.4f, 0.6f, 1f), 0.2f),
                    // 6000 m — deep blue
                    new GradientColorKey(new Color(0.2f, 0.3f, 0.6f), 0.6f),
                    // 7000 m+ — dark stormy grey
                    new GradientColorKey(new Color(0.2f, 0.2f, 0.25f), 0.7f),
                    // 10000 m — near-black storm
                    new GradientColorKey(new Color(0.1f, 0.1f, 0.15f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                }
            );
            return gradient;
        }
    }
}
