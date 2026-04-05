using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TitanAscent.Environment;

namespace TitanAscent.Audio
{
    [System.Serializable]
    public struct SurfaceAudioSet
    {
        public SurfaceType surfaceType;
        public AudioClip[] contactClips;
        public AudioClip slideLoopClip;
        public AudioClip[] grappleImpactClips;
        [Range(0f, 2f)] public float slideVolumeMultiplier;
    }

    public class SurfaceAudioMapper : MonoBehaviour
    {
        [Header("Surface Audio Sets")]
        [SerializeField] private List<SurfaceAudioSet> surfaceSets = new List<SurfaceAudioSet>();

        [Header("Spatial Audio")]
        [SerializeField] private float spatialBlend = 1f;
        [SerializeField] private float maxSpatialDistance = 50f;

        private AudioSource slideSource;
        private Coroutine slideFadeCoroutine;

        private void Awake()
        {
            // Dedicated source for slide looping
            GameObject slideGo = new GameObject("SurfaceSlideLoop");
            slideGo.transform.SetParent(transform, false);
            slideSource = slideGo.AddComponent<AudioSource>();
            slideSource.loop         = true;
            slideSource.spatialBlend = 0f;
            slideSource.playOnAwake  = false;
            slideSource.volume       = 0f;
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>Plays a random contact sound for the given surface at a world position.</summary>
        public void PlayContactSound(SurfaceType surface, Vector3 position)
        {
            SurfaceAudioSet set = GetSet(surface);
            if (set.contactClips == null || set.contactClips.Length == 0) return;

            AudioClip clip = PickRandom(set.contactClips);
            if (clip == null) return;

            PlayAtPosition(clip, position, 1f);
        }

        /// <summary>Starts the slide loop for the given surface type.</summary>
        public void StartSlideSound(SurfaceType surface)
        {
            SurfaceAudioSet set = GetSet(surface);
            if (set.slideLoopClip == null) return;

            if (slideFadeCoroutine != null) StopCoroutine(slideFadeCoroutine);

            if (slideSource.clip != set.slideLoopClip)
            {
                slideSource.clip = set.slideLoopClip;
                slideSource.Stop();
            }

            if (!slideSource.isPlaying) slideSource.Play();

            float targetVolume = 0.6f * set.slideVolumeMultiplier;
            ApplyAudioManagerScale(ref targetVolume);
            slideFadeCoroutine = StartCoroutine(FadeSourceVolume(slideSource, targetVolume, 0.1f));
        }

        /// <summary>Stops the slide loop with a short fade.</summary>
        public void StopSlideSound()
        {
            if (!slideSource.isPlaying) return;

            if (slideFadeCoroutine != null) StopCoroutine(slideFadeCoroutine);
            slideFadeCoroutine = StartCoroutine(FadeSourceAndStop(slideSource, 0f, 0.1f));
        }

        /// <summary>Plays a random grapple impact sound for the given surface at a world position.</summary>
        public void PlayGrappleImpact(SurfaceType surface, Vector3 position)
        {
            SurfaceAudioSet set = GetSet(surface);
            if (set.grappleImpactClips == null || set.grappleImpactClips.Length == 0) return;

            AudioClip clip = PickRandom(set.grappleImpactClips);
            if (clip == null) return;

            PlayAtPosition(clip, position, 0.9f);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private SurfaceAudioSet GetSet(SurfaceType surface)
        {
            foreach (SurfaceAudioSet s in surfaceSets)
            {
                if (s.surfaceType == surface)
                    return s;
            }

            // Fallback: ScaleArmor
            foreach (SurfaceAudioSet s in surfaceSets)
            {
                if (s.surfaceType == SurfaceType.ScaleArmor)
                    return s;
            }

            return default;
        }

        private AudioClip PickRandom(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0) return null;
            return clips[Random.Range(0, clips.Length)];
        }

        private void PlayAtPosition(AudioClip clip, Vector3 position, float volume)
        {
            float finalVolume = volume;
            ApplyAudioManagerScale(ref finalVolume);
            AudioSource.PlayClipAtPoint(clip, position, finalVolume);
        }

        private void ApplyAudioManagerScale(ref float volume)
        {
            // If AudioManager is present, respect any global volume scaling (future-proof hook)
            // Currently AudioManager does not expose a master volume scalar, so we just leave it.
            volume = Mathf.Clamp01(volume);
        }

        private IEnumerator FadeSourceVolume(AudioSource src, float target, float duration)
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
            yield return FadeSourceVolume(src, target, duration);
            src.Stop();
        }
    }
}
