using System.Collections;
using UnityEngine;

namespace TitanAscent.Optimization
{
    /// <summary>
    /// Pool for AudioSources used for spatial one-shot sounds.
    /// Pre-warms 20 AudioSource GameObjects on Awake.
    ///
    /// Usage
    /// ─────
    ///   AudioSourcePool.Instance.PlayAtPoint(clip, position, volume, pitch);
    ///
    /// Sources auto-return to the pool after clip.length + 0.1 s.
    /// Used by AudioManager, SurfaceAudioMapper, GrappleAudioController.
    /// </summary>
    public class AudioSourcePool : MonoBehaviour
    {
        // ── Singleton ──────────────────────────────────────────────────────────

        public static AudioSourcePool Instance { get; private set; }

        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("Pool Settings")]
        [SerializeField] private int initialPoolSize = 20;

        [Header("Audio Settings")]
        [Range(0f, 500f)]
        [SerializeField] private float defaultMaxDistance = 80f;

        // ── Internal ───────────────────────────────────────────────────────────

        private readonly System.Collections.Generic.Queue<AudioSource> _available =
            new System.Collections.Generic.Queue<AudioSource>();

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

            Prewarm(initialPoolSize);
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Plays <paramref name="clip"/> as a spatial one-shot at <paramref name="position"/>.
        /// The source is automatically returned to the pool when playback ends.
        /// </summary>
        /// <param name="clip">The AudioClip to play. No-op if null.</param>
        /// <param name="position">World-space position for the audio source.</param>
        /// <param name="volume">Volume scalar (0–1). Default 1.</param>
        /// <param name="pitch">Pitch multiplier. Default 1.</param>
        public void PlayAtPoint(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            if (clip == null) return;

            AudioSource source = Get();
            if (source == null) return;

            source.transform.position = position;
            source.clip               = clip;
            source.volume             = Mathf.Clamp01(volume);
            source.pitch              = pitch;
            source.loop               = false;
            source.spatialBlend       = 1f;  // full 3-D
            source.maxDistance        = defaultMaxDistance;
            source.rolloffMode        = AudioRolloffMode.Linear;
            source.Play();

            StartCoroutine(ReturnAfterPlay(source, clip.length + 0.1f));
        }

        /// <summary>
        /// Static convenience wrapper. No-op if no instance is present.
        /// </summary>
        public static void PlayOneShot(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            if (Instance == null)
            {
                Debug.LogWarning("[AudioSourcePool] No instance in scene.");
                return;
            }
            Instance.PlayAtPoint(clip, position, volume, pitch);
        }

        // ── Internal helpers ───────────────────────────────────────────────────

        private void Prewarm(int count)
        {
            for (int i = 0; i < count; i++)
            {
                AudioSource src = CreateSource();
                src.gameObject.SetActive(false);
                _available.Enqueue(src);
            }
        }

        private AudioSource Get()
        {
            AudioSource src = null;

            // Drain any nulls (shouldn't happen, but defensive)
            while (_available.Count > 0 && src == null)
                src = _available.Dequeue();

            if (src == null)
                src = CreateSource();

            src.gameObject.SetActive(true);
            return src;
        }

        private void ReturnToPool(AudioSource source)
        {
            if (source == null) return;
            source.Stop();
            source.clip = null;
            source.gameObject.SetActive(false);
            _available.Enqueue(source);
        }

        private AudioSource CreateSource()
        {
            GameObject go = new GameObject("PooledAudioSource");
            go.transform.SetParent(transform, false);
            AudioSource src = go.AddComponent<AudioSource>();
            src.playOnAwake  = false;
            src.spatialBlend = 1f;
            src.maxDistance  = defaultMaxDistance;
            src.rolloffMode  = AudioRolloffMode.Linear;
            go.SetActive(false);
            return src;
        }

        private IEnumerator ReturnAfterPlay(AudioSource source, float delay)
        {
            yield return new WaitForSeconds(delay);
            ReturnToPool(source);
        }
    }
}
