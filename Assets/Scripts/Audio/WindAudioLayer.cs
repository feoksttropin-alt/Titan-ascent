using System.Collections;
using UnityEngine;
using TitanAscent.Environment;

namespace TitanAscent.Audio
{
    public class WindAudioLayer : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────────

        [Header("Base Wind")]
        [SerializeField] private AudioClip baseWindClip;

        [Header("Gust Wind")]
        [SerializeField] private AudioClip[] gustClips;
        [SerializeField] private float gustIntervalMin = 3f;
        [SerializeField] private float gustIntervalMax = 12f;

        [Header("Zone Wind Clips")]
        [Tooltip("One clip per zone index (0-8). Leave null to silence zone wind for that zone.")]
        [SerializeField] private AudioClip[] zoneWindClips = new AudioClip[9];

        [Header("Wind Column Whoosh")]
        [SerializeField] private AudioClip windColumnClip;

        // ── Internal ─────────────────────────────────────────────────────────

        private AudioSource baseWindSource;
        private AudioSource gustWindSource;
        private AudioSource zoneWindSource;
        private AudioSource windColumnSource;

        private float currentAltitude;
        private bool insideWindColumn;

        private ZoneManager zoneManager;
        private int currentZoneIndex = -1;

        private Coroutine gustCoroutine;
        private Coroutine zoneTransitionCoroutine;

        // ── Unity Lifecycle ──────────────────────────────────────────────────

        private void Awake()
        {
            baseWindSource    = CreateSource("Wind_Base",    true,  baseWindClip,    0f);
            gustWindSource    = CreateSource("Wind_Gust",    false, null,            0f);
            zoneWindSource    = CreateSource("Wind_Zone",    true,  null,            0f);
            windColumnSource  = CreateSource("Wind_Column",  true,  windColumnClip,  0f);
        }

        private void Start()
        {
            zoneManager = FindFirstObjectByType<ZoneManager>();
            if (zoneManager != null)
                zoneManager.OnZoneChanged.AddListener(HandleZoneChanged);

            if (baseWindSource.clip != null) baseWindSource.Play();
            gustCoroutine = StartCoroutine(GustRoutine());
        }

        private void OnDestroy()
        {
            if (zoneManager != null)
                zoneManager.OnZoneChanged.RemoveListener(HandleZoneChanged);
        }

        private void Update()
        {
            UpdateBaseWind();
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>Call each frame with the player's current altitude.</summary>
        public void UpdateAltitude(float altitude)
        {
            currentAltitude = altitude;
        }

        /// <summary>Called by WindSystem trigger volumes when the player enters/exits a wind column.</summary>
        public void SetInsideWindColumn(bool inside)
        {
            if (insideWindColumn == inside) return;
            insideWindColumn = inside;

            if (inside)
            {
                if (windColumnSource.clip != null && !windColumnSource.isPlaying)
                    windColumnSource.Play();
                StartCoroutine(FadeSource(windColumnSource, 0.85f, 0.3f));
            }
            else
            {
                StartCoroutine(FadeSourceAndStop(windColumnSource, 0f, 0.4f));
            }
        }

        // ── Private Helpers ──────────────────────────────────────────────────

        private void UpdateBaseWind()
        {
            float targetVol = Mathf.Lerp(0f, 0.6f, currentAltitude / 10000f);
            baseWindSource.volume = Mathf.MoveTowards(baseWindSource.volume, targetVol, Time.deltaTime * 0.5f);
        }

        private void HandleZoneChanged(TitanZone previous, TitanZone newZone)
        {
            if (newZone == null) return;

            int zoneIdx = -1;
            if (zoneManager != null)
                zoneIdx = zoneManager.CurrentZoneIndex;

            if (zoneIdx == currentZoneIndex) return;
            currentZoneIndex = zoneIdx;

            AudioClip targetClip = null;
            if (zoneIdx >= 0 && zoneIdx < zoneWindClips.Length)
                targetClip = zoneWindClips[zoneIdx];

            if (zoneTransitionCoroutine != null) StopCoroutine(zoneTransitionCoroutine);
            zoneTransitionCoroutine = StartCoroutine(CrossFadeZoneWind(targetClip, 1.5f));
        }

        private IEnumerator CrossFadeZoneWind(AudioClip newClip, float duration)
        {
            // Fade out existing
            float startVol = zoneWindSource.volume;
            float elapsed  = 0f;

            while (elapsed < duration * 0.5f)
            {
                elapsed += Time.deltaTime;
                zoneWindSource.volume = Mathf.Lerp(startVol, 0f, elapsed / (duration * 0.5f));
                yield return null;
            }

            zoneWindSource.Stop();
            zoneWindSource.volume = 0f;

            if (newClip == null)
            {
                zoneTransitionCoroutine = null;
                yield break;
            }

            zoneWindSource.clip = newClip;
            zoneWindSource.Play();

            elapsed = 0f;
            while (elapsed < duration * 0.5f)
            {
                elapsed += Time.deltaTime;
                zoneWindSource.volume = Mathf.Lerp(0f, 0.5f, elapsed / (duration * 0.5f));
                yield return null;
            }
            zoneWindSource.volume = 0.5f;
            zoneTransitionCoroutine = null;
        }

        private IEnumerator GustRoutine()
        {
            while (true)
            {
                float interval = Random.Range(gustIntervalMin, gustIntervalMax);
                yield return new WaitForSeconds(interval);

                if (gustClips == null || gustClips.Length == 0) continue;

                AudioClip clip = gustClips[Random.Range(0, gustClips.Length)];
                if (clip == null) continue;

                // Intensity scales with altitude
                float altitudeFactor = Mathf.Clamp01(currentAltitude / 10000f);
                float volume = Mathf.Lerp(0.2f, 0.9f, altitudeFactor);
                float pitch  = Random.Range(0.85f, 1.1f);

                gustWindSource.clip   = clip;
                gustWindSource.volume = volume;
                gustWindSource.pitch  = pitch;
                gustWindSource.loop   = false;
                gustWindSource.Play();
            }
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

        private AudioSource CreateSource(string goName, bool loop, AudioClip clip, float initialVolume)
        {
            GameObject go = new GameObject(goName);
            go.transform.SetParent(transform, false);
            AudioSource src = go.AddComponent<AudioSource>();
            src.loop         = loop;
            src.spatialBlend = 0f;
            src.playOnAwake  = false;
            src.volume       = initialVolume;
            src.clip         = clip;
            return src;
        }
    }
}
