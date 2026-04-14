using UnityEngine;
using System.Collections.Generic;

namespace TitanAscent.Audio
{
    public enum AudioChannel
    {
        RopeTension,
        Wind,
        TitanBreathing,
        SurfaceScrape,
        GrappleFire,
        GrappleImpact,
        ThrusterBurst,
        FallWhoosh
    }

    [System.Serializable]
    public class AudioChannelConfig
    {
        public AudioChannel channel;
        public AudioSource source;
        public AudioClip defaultClip;
        [Range(0f, 1f)] public float baseVolume = 1f;
        public bool loops = false;
    }

    public class AudioManager : MonoBehaviour
    {
        // ── Singleton ─────────────────────────────────────────────────────────────
        private static AudioManager _instance;

        /// <summary>
        /// Returns the AudioManager singleton. Logs a warning (dev builds) if not yet in scene —
        /// audio calls made before the AudioManager is instantiated are silently ignored.
        /// </summary>
        public static AudioManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<AudioManager>();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    if (_instance == null)
                        Debug.LogWarning("[AudioManager] No AudioManager found in scene. Audio will be silent.");
#endif
                }
                return _instance;
            }
        }

        [Header("Channel Configurations")]
        [SerializeField] private List<AudioChannelConfig> channelConfigs = new List<AudioChannelConfig>();

        [Header("One-Shot Pool")]
        [SerializeField] private int oneShotPoolSize = 8;

        [Header("Ambient")]
        [SerializeField] private AnimationCurve windVolumeCurve = AnimationCurve.Linear(0f, 0f, 10000f, 1f);
        [SerializeField] private AnimationCurve windPitchCurve  = AnimationCurve.Linear(0f, 0.8f, 10000f, 1.4f);

        private Dictionary<AudioChannel, AudioChannelConfig> channelMap = new Dictionary<AudioChannel, AudioChannelConfig>();
        private List<AudioSource> oneShotPool = new List<AudioSource>();
        private int oneShotPoolIndex = 0;

        private Player.PlayerController player;
        private Grapple.RopeSimulator   ropeSimulator;
        private float _nextReferenceSearchTime;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            BuildChannelMap();
            BuildOneShotPool();

            player = FindFirstObjectByType<Player.PlayerController>();
            ropeSimulator = FindFirstObjectByType<Grapple.RopeSimulator>();

            // Start looping channels
            StartChannel(AudioChannel.TitanBreathing);
            StartChannel(AudioChannel.Wind);
        }

        private void BuildChannelMap()
        {
            foreach (AudioChannelConfig config in channelConfigs)
            {
                if (!channelMap.ContainsKey(config.channel))
                    channelMap[config.channel] = config;

                if (config.source == null)
                {
                    GameObject go = new GameObject($"Audio_{config.channel}");
                    go.transform.SetParent(transform, false);
                    config.source = go.AddComponent<AudioSource>();
                    config.source.loop = config.loops;
                    config.source.volume = config.baseVolume;
                    config.source.spatialBlend = 0f;
                }
            }
        }

        private void BuildOneShotPool()
        {
            if (oneShotPoolSize <= 0) oneShotPoolSize = 8;
            for (int i = 0; i < oneShotPoolSize; i++)
            {
                GameObject go = new GameObject($"OneShotSource_{i}");
                go.transform.SetParent(transform, false);
                AudioSource src = go.AddComponent<AudioSource>();
                src.spatialBlend = 0f;
                src.playOnAwake = false;
                oneShotPool.Add(src);
            }
        }

        private void Update()
        {
            // Reacquire references lost after scene changes — throttled to once per second
            // to avoid paying FindFirstObjectByType cost every frame.
            if ((player == null || ropeSimulator == null) && Time.time >= _nextReferenceSearchTime)
            {
                _nextReferenceSearchTime = Time.time + 1f;
                if (player == null)
                    player = FindFirstObjectByType<Player.PlayerController>();
                if (ropeSimulator == null)
                    ropeSimulator = FindFirstObjectByType<Grapple.RopeSimulator>();
            }

            if (player != null)
                UpdateAmbient(player.CurrentHeight);

            UpdateRopeTension();
            UpdateFallWhoosh();
            UpdateSurfaceScrape();
        }

        private void UpdateAmbient(float altitude)
        {
            SetChannelVolume(AudioChannel.Wind, windVolumeCurve.Evaluate(altitude));
            SetChannelPitch(AudioChannel.Wind, windPitchCurve.Evaluate(altitude));

            // Titan breathing is constant but fades slightly with altitude
            float breathVol = Mathf.Lerp(0.6f, 0.15f, Mathf.Clamp01(altitude / 10000f));
            SetChannelVolume(AudioChannel.TitanBreathing, breathVol);
        }

        private void UpdateRopeTension()
        {
            if (ropeSimulator == null || !ropeSimulator.IsAttached)
            {
                SetChannelVolume(AudioChannel.RopeTension, 0f);
                return;
            }

            float tension = ropeSimulator.GetTension();
            SetChannelVolume(AudioChannel.RopeTension, tension * 0.8f);
            SetChannelPitch(AudioChannel.RopeTension, 0.8f + tension * 0.6f);

            StartChannel(AudioChannel.RopeTension);
        }

        private void UpdateFallWhoosh()
        {
            if (player == null) return;

            float fallSpeed = -player.CurrentVelocity.y;
            if (fallSpeed > 5f)
            {
                float volume = Mathf.Clamp01((fallSpeed - 5f) / 40f);
                SetChannelVolume(AudioChannel.FallWhoosh, volume);
                SetChannelPitch(AudioChannel.FallWhoosh, 0.7f + volume * 0.6f);
                StartChannel(AudioChannel.FallWhoosh);
            }
            else
            {
                SetChannelVolume(AudioChannel.FallWhoosh, 0f);
            }
        }

        private void UpdateSurfaceScrape()
        {
            if (player == null) return;

            bool sliding = player.CurrentState == Player.PlayerState.Sliding;
            float volume = sliding ? Mathf.Clamp01(player.CurrentVelocity.magnitude / 10f) * 0.6f : 0f;
            SetChannelVolume(AudioChannel.SurfaceScrape, volume);

            if (sliding) StartChannel(AudioChannel.SurfaceScrape);
        }

        public void PlayOneShot(AudioClip clip, float volume = 1f)
        {
            if (clip == null) return;

            AudioSource src = oneShotPool[oneShotPoolIndex % oneShotPool.Count];
            oneShotPoolIndex++;
            src.clip = clip;
            src.volume = volume;
            src.pitch = Random.Range(0.95f, 1.05f);
            src.Play();
        }

        public void SetChannelVolume(AudioChannel channel, float volume)
        {
            if (!channelMap.TryGetValue(channel, out AudioChannelConfig config)) return;
            if (config.source == null) return;
            config.source.volume = Mathf.Clamp01(volume * config.baseVolume);
        }

        public void SetChannelPitch(AudioChannel channel, float pitch)
        {
            if (!channelMap.TryGetValue(channel, out AudioChannelConfig config)) return;
            if (config.source == null) return;
            config.source.pitch = pitch;
        }

        private void StartChannel(AudioChannel channel)
        {
            if (!channelMap.TryGetValue(channel, out AudioChannelConfig config)) return;
            if (config.source == null || config.source.isPlaying) return;
            if (config.defaultClip != null) config.source.clip = config.defaultClip;
            config.source.loop = config.loops;
            if (config.source.clip != null) config.source.Play();
        }

        public void StopChannel(AudioChannel channel)
        {
            if (!channelMap.TryGetValue(channel, out AudioChannelConfig config)) return;
            config.source?.Stop();
        }
    }
}
