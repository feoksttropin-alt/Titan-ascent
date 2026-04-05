using System.Collections;
using UnityEngine;
using TitanAscent.Systems;

namespace TitanAscent.Audio
{
    public class FallAudioController : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────────

        [Header("Fall Wind Whoosh")]
        [SerializeField] private AudioClip whooshLoopClip;
        [SerializeField] private float terminalVelocity = 50f;

        [Header("Impact Sounds")]
        [SerializeField] private AudioClip smallMediumThudClip;
        [SerializeField] private AudioClip largeImpactClip;
        [SerializeField] private AudioClip catastrophicImpactClip;
        [SerializeField] private AudioClip runEndingImpactClip;

        [Header("Debris Settle")]
        [SerializeField] private AudioClip[] debrisSettleClips;

        [Header("Emergency Grapple Recovery")]
        [SerializeField] private AudioClip emergencyRopeSnapClip;

        [Header("Post-RunEnding Heartbeat")]
        [SerializeField] private AudioClip heartbeatClip;

        [Header("Audio Duck Settings")]
        [SerializeField] private float largeDuckAmount    = 0.6f;   // fraction to reduce
        [SerializeField] private float largeDuckDuration  = 0.3f;
        [SerializeField] private float cataDuckDuration   = 0.6f;
        [SerializeField] private float runEndDuckDuration = 1.0f;

        // ── Internal ─────────────────────────────────────────────────────────

        private AudioSource whooshSource;
        private AudioSource impactSource;
        private AudioSource emergencySource;
        private AudioSource heartbeatSource;
        private AudioSource oneShotSource;

        private Coroutine whooshFadeCoroutine;
        private Coroutine duckCoroutine;
        private Coroutine heartbeatCoroutine;

        // ── Unity Lifecycle ──────────────────────────────────────────────────

        private void Awake()
        {
            whooshSource    = CreateSource("Fall_Whoosh",    true,  whooshLoopClip, 0f);
            impactSource    = CreateSource("Fall_Impact",    false, null,           1f);
            emergencySource = CreateSource("Fall_Emergency", false, emergencyRopeSnapClip, 1f);
            heartbeatSource = CreateSource("Fall_Heartbeat", true,  heartbeatClip,  0f);
            oneShotSource   = CreateSource("Fall_OneShot",   false, null,           1f);

            if (whooshSource.clip != null) whooshSource.Play();
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>Called when a fall is detected by FallTracker.</summary>
        public void OnFallStarted()
        {
            if (whooshFadeCoroutine != null) StopCoroutine(whooshFadeCoroutine);
            whooshFadeCoroutine = StartCoroutine(FadeSource(whooshSource, 0.3f, 0.5f));
        }

        /// <summary>Called each frame during a fall with the current speed (positive).</summary>
        public void OnFallAccelerating(float speed)
        {
            float t      = Mathf.Clamp01(speed / terminalVelocity);
            float volume = Mathf.Lerp(0.1f, 1.0f, t);
            float pitch  = Mathf.Lerp(0.7f, 1.3f, t);

            whooshSource.volume = volume;
            whooshSource.pitch  = pitch;
        }

        /// <summary>Called on landing. Severity determines which impact and duck to play.</summary>
        public void OnLanding(FallData data)
        {
            // Stop whoosh
            if (whooshFadeCoroutine != null) StopCoroutine(whooshFadeCoroutine);
            whooshFadeCoroutine = StartCoroutine(FadeSourceAndStop(whooshSource, 0f, 0.15f));

            switch (data.severity)
            {
                case FallSeverity.Small:
                case FallSeverity.Medium:
                    PlayImpact(smallMediumThudClip, 0.7f, RandomPitch(0.95f, 1.05f));
                    break;

                case FallSeverity.Large:
                    PlayImpact(largeImpactClip, 1f, RandomPitch(0.9f, 1.0f));
                    TriggerDuck(largeDuckAmount, largeDuckDuration);
                    break;

                case FallSeverity.Catastrophic:
                    PlayImpact(catastrophicImpactClip, 1f, RandomPitch(0.85f, 0.95f));
                    TriggerDuck(largeDuckAmount, cataDuckDuration);
                    PlayDebrisSettle();
                    break;

                case FallSeverity.RunEnding:
                    PlayImpact(runEndingImpactClip, 1f, RandomPitch(0.8f, 0.9f));
                    TriggerDuck(0.9f, runEndDuckDuration);
                    PlayDebrisSettle();
                    if (heartbeatCoroutine != null) StopCoroutine(heartbeatCoroutine);
                    heartbeatCoroutine = StartCoroutine(HeartbeatSequence());
                    break;
            }
        }

        /// <summary>Called when the player triggers emergency grapple recovery mid-fall.</summary>
        public void OnEmergencyGrapple()
        {
            // Stop the whoosh
            if (whooshFadeCoroutine != null) StopCoroutine(whooshFadeCoroutine);
            whooshFadeCoroutine = StartCoroutine(FadeSourceAndStop(whooshSource, 0f, 0.2f));

            // Triumphant rope-snap
            if (emergencyRopeSnapClip != null)
            {
                emergencySource.clip   = emergencyRopeSnapClip;
                emergencySource.volume = 1f;
                emergencySource.pitch  = 1.1f;
                emergencySource.Play();
            }

            // Brief music intensity spike via MusicManager
            if (MusicManager.Instance != null)
                MusicManager.Instance.SetFallState(false, 0f);
        }

        // ── Private ──────────────────────────────────────────────────────────

        private void PlayImpact(AudioClip clip, float volume, float pitch)
        {
            if (clip == null) return;
            impactSource.clip   = clip;
            impactSource.volume = volume;
            impactSource.pitch  = pitch;
            impactSource.Play();
        }

        private void PlayDebrisSettle()
        {
            if (debrisSettleClips == null || debrisSettleClips.Length == 0) return;
            StartCoroutine(DelayedDebris());
        }

        private IEnumerator DelayedDebris()
        {
            yield return new WaitForSeconds(Random.Range(0.2f, 0.5f));
            AudioClip clip = debrisSettleClips[Random.Range(0, debrisSettleClips.Length)];
            if (clip == null) yield break;

            oneShotSource.clip   = clip;
            oneShotSource.volume = Random.Range(0.3f, 0.6f);
            oneShotSource.pitch  = RandomPitch(0.9f, 1.1f);
            oneShotSource.Play();
        }

        private void TriggerDuck(float amount, float duration)
        {
            if (duckCoroutine != null) StopCoroutine(duckCoroutine);
            duckCoroutine = StartCoroutine(AudioDuckCoroutine(amount, duration));
        }

        private IEnumerator AudioDuckCoroutine(float duckFraction, float duration)
        {
            // Duck all AudioListener volume by the fraction
            float startVol  = AudioListener.volume;
            float targetVol = startVol * (1f - duckFraction);

            AudioListener.volume = targetVol;
            yield return new WaitForSeconds(duration);

            // Recover
            float elapsed = 0f;
            float recoverTime = duration * 0.5f;
            while (elapsed < recoverTime)
            {
                elapsed += Time.deltaTime;
                AudioListener.volume = Mathf.Lerp(targetVol, startVol, elapsed / recoverTime);
                yield return null;
            }
            AudioListener.volume = startVol;
            duckCoroutine = null;
        }

        private IEnumerator HeartbeatSequence()
        {
            if (heartbeatClip == null)
            {
                heartbeatCoroutine = null;
                yield break;
            }

            heartbeatSource.volume = 0.6f;
            if (!heartbeatSource.isPlaying) heartbeatSource.Play();

            float elapsed = 0f;
            float duration = 2f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                // Fade out as the 2s passes
                heartbeatSource.volume = Mathf.Lerp(0.6f, 0f, elapsed / duration);
                yield return null;
            }

            heartbeatSource.Stop();
            heartbeatSource.volume = 0f;
            heartbeatCoroutine = null;
        }

        private IEnumerator FadeSource(AudioSource src, float target, float duration)
        {
            float start   = src.volume;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                src.volume = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            src.volume = target;
        }

        private IEnumerator FadeSourceAndStop(AudioSource src, float target, float duration)
        {
            yield return FadeSource(src, target, duration);
            src.Stop();
        }

        private AudioSource CreateSource(string goName, bool loop, AudioClip clip, float volume)
        {
            GameObject go = new GameObject(goName);
            go.transform.SetParent(transform, false);
            AudioSource src = go.AddComponent<AudioSource>();
            src.loop         = loop;
            src.spatialBlend = 0f;
            src.playOnAwake  = false;
            src.volume       = volume;
            src.clip         = clip;
            return src;
        }

        private float RandomPitch(float min, float max) => Random.Range(min, max);
    }
}
