using System;
using System.Collections;
using UnityEngine;
using TitanAscent.Environment;

namespace TitanAscent.Audio
{
    // -----------------------------------------------------------------------
    // Zone config struct
    // -----------------------------------------------------------------------

    [Serializable]
    public struct ZoneAmbientConfig
    {
        public int    zoneIndex;
        [Tooltip("Human-readable description of this zone's ambient character.")]
        public string description;
        [Range(0f, 1f)] public float volume;
        [Range(0f, 0.5f)] public float pitchVariation;
    }

    // -----------------------------------------------------------------------
    // AmbientSoundscape
    // -----------------------------------------------------------------------

    /// <summary>
    /// Per-zone ambient audio with A/B crossfade and subtle Perlin noise
    /// modulation to prevent loop fatigue.
    /// </summary>
    public class AmbientSoundscape : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        // Inspector
        // -----------------------------------------------------------------------

        [Header("Audio Clips (9 zones, index 0–8)")]
        [SerializeField] private AudioClip[] zoneAmbientClips = new AudioClip[9];

        [Header("Audio Sources (A/B crossfade)")]
        [SerializeField] private AudioSource sourceA;
        [SerializeField] private AudioSource sourceB;

        [Header("Crossfade")]
        [SerializeField] private float crossfadeDuration = 3f;

        [Header("Perlin Variation")]
        [SerializeField] private float variationCycle    = 8f;   // seconds per Perlin cycle
        [SerializeField] private float maxVolumeVariation = 0.05f;
        [SerializeField] private float maxPitchVariation  = 0.05f;

        // -----------------------------------------------------------------------
        // Pre-populated zone configs
        // -----------------------------------------------------------------------

        private static readonly ZoneAmbientConfig[] DefaultConfigs =
        {
            new ZoneAmbientConfig { zoneIndex = 0, description = "Deep titan heartbeat",        volume = 0.30f, pitchVariation = 0.02f },
            new ZoneAmbientConfig { zoneIndex = 1, description = "Hollow bone wind",             volume = 0.25f, pitchVariation = 0.03f },
            new ZoneAmbientConfig { zoneIndex = 2, description = "Organic muscle sounds",        volume = 0.20f, pitchVariation = 0.02f },
            new ZoneAmbientConfig { zoneIndex = 3, description = "Leathery membrane creaks",     volume = 0.20f, pitchVariation = 0.04f },
            new ZoneAmbientConfig { zoneIndex = 4, description = "Sharp whistling wind",         volume = 0.35f, pitchVariation = 0.05f },
            new ZoneAmbientConfig { zoneIndex = 5, description = "Metallic weapon creaks",       volume = 0.15f, pitchVariation = 0.03f },
            new ZoneAmbientConfig { zoneIndex = 6, description = "Storm roar",                   volume = 0.50f, pitchVariation = 0.05f },
            new ZoneAmbientConfig { zoneIndex = 7, description = "Breathing pulse",              volume = 0.40f, pitchVariation = 0.04f },
            new ZoneAmbientConfig { zoneIndex = 8, description = "Crystal ringing",              volume = 0.30f, pitchVariation = 0.02f },
        };

        // -----------------------------------------------------------------------
        // Private state
        // -----------------------------------------------------------------------

        private int    _currentZoneIndex = -1;
        private bool   _sourceAActive    = true;  // A is current, B is incoming
        private Coroutine _crossfadeRoutine;

        private ZoneAmbientConfig[] _configs;
        private ZoneManager _zm;

        private float _perlinOffsetVolume;
        private float _perlinOffsetPitch;

        // -----------------------------------------------------------------------
        // Lifecycle
        // -----------------------------------------------------------------------

        private void Awake()
        {
            _configs = DefaultConfigs;

            _perlinOffsetVolume = UnityEngine.Random.Range(0f, 100f);
            _perlinOffsetPitch  = UnityEngine.Random.Range(0f, 100f);

            EnsureAudioSources();
        }

        private void Start()
        {
            _zm = FindFirstObjectByType<ZoneManager>();
            if (_zm != null)
                _zm.OnZoneChanged.AddListener(OnZoneChanged);
        }

        private void OnDestroy()
        {
            if (_crossfadeRoutine != null) StopCoroutine(_crossfadeRoutine);
            if (_zm != null)
                _zm.OnZoneChanged.RemoveListener(OnZoneChanged);
        }

        private void Update()
        {
            ApplyPerlinVariation();
        }

        // -----------------------------------------------------------------------
        // Zone change handler
        // -----------------------------------------------------------------------

        private void OnZoneChanged(TitanZone previous, TitanZone newZone)
        {
            int newIndex = _zm != null ? _zm.CurrentZoneIndex : 0;
            newIndex = Mathf.Clamp(newIndex, 0, _configs.Length - 1);

            if (newIndex == _currentZoneIndex) return;
            _currentZoneIndex = newIndex;

            if (_crossfadeRoutine != null)
                StopCoroutine(_crossfadeRoutine);

            _crossfadeRoutine = StartCoroutine(CrossfadeTo(newIndex));
        }

        // -----------------------------------------------------------------------
        // Crossfade coroutine
        // -----------------------------------------------------------------------

        private IEnumerator CrossfadeTo(int zoneIndex)
        {
            AudioSource outgoing = _sourceAActive ? sourceA : sourceB;
            AudioSource incoming = _sourceAActive ? sourceB : sourceA;

            ZoneAmbientConfig cfg = _configs[Mathf.Clamp(zoneIndex, 0, _configs.Length - 1)];
            AudioClip clip = zoneIndex < zoneAmbientClips.Length ? zoneAmbientClips[zoneIndex] : null;

            if (clip != null)
            {
                incoming.clip   = clip;
                incoming.volume = 0f;
                incoming.pitch  = 1f;
                incoming.loop   = true;
                incoming.Play();
            }

            float outStartVol = outgoing.volume;
            float inTargetVol = cfg.volume;
            float elapsed     = 0f;

            while (elapsed < crossfadeDuration)
            {
                elapsed += Time.deltaTime;
                float t  = Mathf.Clamp01(elapsed / crossfadeDuration);

                outgoing.volume = Mathf.Lerp(outStartVol, 0f, t);
                if (clip != null)
                    incoming.volume = Mathf.Lerp(0f, inTargetVol, t);

                yield return null;
            }

            outgoing.Stop();
            outgoing.volume = 0f;

            _sourceAActive = !_sourceAActive;
        }

        // -----------------------------------------------------------------------
        // Perlin noise variation
        // -----------------------------------------------------------------------

        private void ApplyPerlinVariation()
        {
            if (_currentZoneIndex < 0 || _currentZoneIndex >= _configs.Length) return;

            ZoneAmbientConfig cfg = _configs[_currentZoneIndex];
            AudioSource active    = _sourceAActive ? sourceA : sourceB;

            if (active == null || !active.isPlaying) return;

            float timeBase    = Time.time / variationCycle;
            float volNoise    = Mathf.PerlinNoise(timeBase + _perlinOffsetVolume, 0f);   // 0..1
            float pitchNoise  = Mathf.PerlinNoise(0f, timeBase + _perlinOffsetPitch);    // 0..1

            float volMod   = Mathf.Lerp(-maxVolumeVariation, maxVolumeVariation, volNoise);
            float pitchMod = Mathf.Lerp(-maxPitchVariation,  maxPitchVariation,  pitchNoise);

            active.volume = Mathf.Clamp01(cfg.volume + volMod);
            active.pitch  = Mathf.Clamp(1f + pitchMod * cfg.pitchVariation, 0.8f, 1.2f);
        }

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        private void EnsureAudioSources()
        {
            if (sourceA == null)
            {
                GameObject goA = new GameObject("AmbientSource_A");
                goA.transform.SetParent(transform, false);
                sourceA = goA.AddComponent<AudioSource>();
                sourceA.spatialBlend  = 0f;
                sourceA.playOnAwake   = false;
                sourceA.loop          = true;
            }

            if (sourceB == null)
            {
                GameObject goB = new GameObject("AmbientSource_B");
                goB.transform.SetParent(transform, false);
                sourceB = goB.AddComponent<AudioSource>();
                sourceB.spatialBlend  = 0f;
                sourceB.playOnAwake   = false;
                sourceB.loop          = true;
            }
        }
    }
}
