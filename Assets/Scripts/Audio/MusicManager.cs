using System.Collections;
using UnityEngine;

namespace TitanAscent.Audio
{
    public class MusicManager : MonoBehaviour
    {
        public static MusicManager Instance { get; private set; }

        // ── Layer Sources ────────────────────────────────────────────────────

        [Header("Layer Clips (index matches layer: 0=Base, 1=Rhythm, 2=Melody, 3=Intensity)")]
        [SerializeField] private AudioClip[] layerClips = new AudioClip[4];

        [Header("Victory Sting")]
        [SerializeField] private AudioClip victorySting;

        // ── Volume Curves ────────────────────────────────────────────────────

        [Header("Altitude Volume Curves")]
        [SerializeField] private AnimationCurve rhythmAltitudeCurve  = DefaultRhythmCurve();
        [SerializeField] private AnimationCurve melodyAltitudeCurve  = DefaultMelodyCurve();

        // ── Internal State ───────────────────────────────────────────────────

        private AudioSource[] layers = new AudioSource[4];  // 0=Base,1=Rhythm,2=Melody,3=Intensity
        private float[] targetVolumes  = new float[4];
        private float[] currentVolumes = new float[4];

        private const float BaseVolume      = 0.4f;
        private const float FadeSpeed       = 2f;   // vol/sec normal
        private const float FastFadeSpeed   = 8f;   // vol/sec for quick transitions

        private bool isFalling;
        private bool isPaused;
        private bool isVictory;

        // Coroutine handles so we can cancel mid-flight
        private Coroutine landingBreathCoroutine;
        private Coroutine victoryCoroutine;
        private Coroutine pauseDuckCoroutine;

        // ── Unity Lifecycle ──────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            CreateLayers();
        }

        private void CreateLayers()
        {
            string[] names = { "Music_Base", "Music_Rhythm", "Music_Melody", "Music_Intensity" };
            for (int i = 0; i < 4; i++)
            {
                GameObject go = new GameObject(names[i]);
                go.transform.SetParent(transform, false);
                AudioSource src = go.AddComponent<AudioSource>();
                src.loop         = true;
                src.spatialBlend = 0f;
                src.playOnAwake  = false;
                src.volume       = 0f;

                if (i < layerClips.Length && layerClips[i] != null)
                {
                    src.clip = layerClips[i];
                    src.Play();
                }

                layers[i] = src;
            }

            // Base layer always at full base volume
            targetVolumes[0]  = BaseVolume;
            currentVolumes[0] = BaseVolume;
            if (layers[0] != null) layers[0].volume = BaseVolume;
        }

        private void Update()
        {
            if (isVictory || isPaused) return;

            SmoothVolumes();
        }

        private void SmoothVolumes()
        {
            for (int i = 0; i < 4; i++)
            {
                if (layers[i] == null) continue;

                float speed = (i == 3 && isFalling) ? FastFadeSpeed : FadeSpeed;
                currentVolumes[i] = Mathf.MoveTowards(currentVolumes[i], targetVolumes[i], speed * Time.deltaTime);
                layers[i].volume  = currentVolumes[i];
            }
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>Call each frame from a higher-level controller with the player's altitude.</summary>
        public void UpdateForAltitude(float altitude)
        {
            if (isVictory || isPaused) return;

            // Rhythm: fades in 2000 → 5000 m
            targetVolumes[1] = rhythmAltitudeCurve.Evaluate(altitude);

            // Melody: fades in 5000 → 8000 m
            targetVolumes[2] = melodyAltitudeCurve.Evaluate(altitude);

            // Intensity layer also scales with Zone 7+ altitude (6500+ m)
            // but only if not already driven higher by a fall state
            float altIntensity = Mathf.Clamp01((altitude - 6500f) / 1500f) * 0.7f;
            if (!isFalling)
                targetVolumes[3] = altIntensity;
        }

        /// <summary>Notify the music system about fall state changes.</summary>
        public void SetFallState(bool falling, float fallDistance)
        {
            isFalling = falling;

            if (falling && fallDistance > 100f)
            {
                // Intensity layer ramps to 0.9 quickly (handled in SmoothVolumes with FastFadeSpeed)
                targetVolumes[3] = 0.9f;
            }
            else if (!falling)
            {
                // Landing recovery — only if it was a significant fall
                if (fallDistance > 100f)
                {
                    if (landingBreathCoroutine != null) StopCoroutine(landingBreathCoroutine);
                    landingBreathCoroutine = StartCoroutine(LandingBreathCoroutine());
                }
                else
                {
                    // Small fall — just clear intensity
                    targetVolumes[3] = 0f;
                }
            }
        }

        /// <summary>Called when the player reaches the summit.</summary>
        public void TriggerVictory()
        {
            if (isVictory) return;
            isVictory = true;

            if (victoryCoroutine != null) StopCoroutine(victoryCoroutine);
            victoryCoroutine = StartCoroutine(VictorySequenceCoroutine());
        }

        /// <summary>Called when game is paused.</summary>
        public void SetPaused(bool paused)
        {
            if (isPaused == paused) return;
            isPaused = paused;

            if (pauseDuckCoroutine != null) StopCoroutine(pauseDuckCoroutine);
            pauseDuckCoroutine = StartCoroutine(PauseDuckCoroutine(paused));
        }

        // ── Coroutines ───────────────────────────────────────────────────────

        private IEnumerator LandingBreathCoroutine()
        {
            // Capture the pre-breath targets
            float[] preBreath = new float[4];
            for (int i = 0; i < 4; i++) preBreath[i] = targetVolumes[i];

            // Drop all to 0.1 over 0.5s
            const float dropDuration = 0.5f;
            float elapsed = 0f;
            float[] startVols = (float[])currentVolumes.Clone();

            while (elapsed < dropDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / dropDuration);
                for (int i = 0; i < 4; i++)
                {
                    currentVolumes[i] = Mathf.Lerp(startVols[i], 0.1f, t);
                    if (layers[i] != null) layers[i].volume = currentVolumes[i];
                }
                yield return null;
            }

            // Hold briefly
            yield return new WaitForSeconds(0.15f);

            // Restore — intensity returns to 0 since fall is over
            targetVolumes[0] = BaseVolume;
            targetVolumes[3] = 0f;
            // Rhythm and melody stay at whatever altitude dictates (caller will update)

            landingBreathCoroutine = null;
        }

        private IEnumerator VictorySequenceCoroutine()
        {
            // Fade everything except melody out over 2s; melody goes to full
            const float duration = 2f;
            float elapsed = 0f;
            float[] startVols = (float[])currentVolumes.Clone();

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                for (int i = 0; i < 4; i++)
                {
                    float target = (i == 2) ? 1f : 0f;
                    currentVolumes[i] = Mathf.Lerp(startVols[i], target, t);
                    if (layers[i] != null) layers[i].volume = currentVolumes[i];
                }
                yield return null;
            }

            // Play victory sting as one-shot
            if (victorySting != null)
            {
                AudioSource stingSource = gameObject.AddComponent<AudioSource>();
                stingSource.spatialBlend = 0f;
                stingSource.clip = victorySting;
                stingSource.volume = 1f;
                stingSource.Play();
                Destroy(stingSource, victorySting.length + 0.5f);
            }

            victoryCoroutine = null;
        }

        private IEnumerator PauseDuckCoroutine(bool ducking)
        {
            const float pauseFadeDuration = 0.3f;
            float elapsed = 0f;
            float[] startVols = (float[])currentVolumes.Clone();

            float[] endVols = new float[4];
            if (ducking)
            {
                for (int i = 0; i < 4; i++) endVols[i] = 0.15f;
            }
            else
            {
                // Restore to sensible defaults; proper altitude update will follow
                endVols[0] = BaseVolume;
                endVols[1] = targetVolumes[1];
                endVols[2] = targetVolumes[2];
                endVols[3] = targetVolumes[3];
            }

            while (elapsed < pauseFadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / pauseFadeDuration);
                for (int i = 0; i < 4; i++)
                {
                    currentVolumes[i] = Mathf.Lerp(startVols[i], endVols[i], t);
                    if (layers[i] != null) layers[i].volume = currentVolumes[i];
                }
                yield return null;
            }

            pauseDuckCoroutine = null;
        }

        // ── Default Curve Factories ──────────────────────────────────────────

        private static AnimationCurve DefaultRhythmCurve()
        {
            // 0 at 2000m, ramps to 0.7 at 5000m
            AnimationCurve c = new AnimationCurve();
            c.AddKey(new Keyframe(0f,    0f));
            c.AddKey(new Keyframe(2000f, 0f));
            c.AddKey(new Keyframe(5000f, 0.7f));
            c.AddKey(new Keyframe(10000f, 0.7f));
            return c;
        }

        private static AnimationCurve DefaultMelodyCurve()
        {
            // 0 at 5000m, ramps to 0.8 at 8000m
            AnimationCurve c = new AnimationCurve();
            c.AddKey(new Keyframe(0f,    0f));
            c.AddKey(new Keyframe(5000f, 0f));
            c.AddKey(new Keyframe(8000f, 0.8f));
            c.AddKey(new Keyframe(10000f, 0.8f));
            return c;
        }
    }
}
