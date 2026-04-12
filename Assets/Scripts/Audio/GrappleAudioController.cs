using System.Collections;
using UnityEngine;
using TitanAscent.Grapple;
using TitanAscent.Environment;

namespace TitanAscent.Audio
{
    [RequireComponent(typeof(GrappleController))]
    public class GrappleAudioController : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────────

        [Header("Grapple Fire")]
        [SerializeField] private AudioClip fireClip;

        [Header("Grapple Attach")]
        [SerializeField] private AudioClip ropeSnapClip;

        [Header("Grapple Release")]
        [SerializeField] private AudioClip releaseClip;

        [Header("Rope Tension")]
        [SerializeField] private AudioClip ropeTensionLoopClip;

        [Header("Rope Max Length")]
        [SerializeField] private AudioClip ropeStrainPingClip;

        [Header("Grapple Miss")]
        [SerializeField] private AudioClip missWhooshClip;

        [Header("Tension Thresholds")]
        [SerializeField] private float tensionFadeInThreshold  = 0.3f;
        [SerializeField] private float tensionFadeOutThreshold = 0.1f;

        // ── Internal ─────────────────────────────────────────────────────────

        private GrappleController grappleController;
        private SurfaceAudioMapper surfaceAudioMapper;

        private AudioSource fireSource;
        private AudioSource attachSource;
        private AudioSource releaseSource;
        private AudioSource tensionSource;
        private AudioSource oneShotSource;

        private float currentTension;
        private bool tensionAudible;
        private Coroutine tensionFadeCoroutine;

        private float prevRopeLength;
        private bool prevAtMaxLength;

        // ── Unity Lifecycle ──────────────────────────────────────────────────

        private void Awake()
        {
            grappleController = GetComponent<GrappleController>();

            fireSource    = CreateSource("Grapple_Fire",    false, null, 1f);
            attachSource  = CreateSource("Grapple_Attach",  false, null, 1f);
            releaseSource = CreateSource("Grapple_Release", false, null, 1f);
            tensionSource = CreateSource("Grapple_Tension", true,  ropeTensionLoopClip, 0f);
            oneShotSource = CreateSource("Grapple_OneShot", false, null, 1f);

            if (tensionSource.clip != null) tensionSource.Play();
        }

        private void Start()
        {
            surfaceAudioMapper = FindFirstObjectByType<SurfaceAudioMapper>();

            // Subscribe to grapple events
            grappleController.OnGrappleAttached.AddListener(HandleGrappleAttached);
            grappleController.OnGrappleReleased.AddListener(HandleGrappleReleased);
            grappleController.OnRopeTensionChanged.AddListener(HandleRopeTension);
        }

        private void OnDestroy()
        {
            if (grappleController == null) return;
            grappleController.OnGrappleAttached.RemoveListener(HandleGrappleAttached);
            grappleController.OnGrappleReleased.RemoveListener(HandleGrappleReleased);
            grappleController.OnRopeTensionChanged.RemoveListener(HandleRopeTension);
        }

        private void Update()
        {
            TrackRopeLength();
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>Call when the player fires the grapple (before it hits anything).</summary>
        public void OnGrappleFired()
        {
            if (fireClip == null) return;

            // Pitch varies slightly with current rope length (shorter rope = higher pitch)
            float ropeLength = grappleController.CurrentRopeLength;
            float maxLen     = 50f; // mirrors GrappleController default
            float pitch      = Mathf.Lerp(1.2f, 0.85f, ropeLength / maxLen);

            fireSource.clip   = fireClip;
            fireSource.pitch  = pitch;
            fireSource.volume = 0.85f;
            fireSource.Play();
        }

        /// <summary>Call when the grapple misses all surfaces.</summary>
        public void OnGrappleMiss()
        {
            PlayOneShot(missWhooshClip, 0.5f, Random.Range(0.9f, 1.1f));
        }

        // ── Event Handlers ───────────────────────────────────────────────────

        private void HandleGrappleAttached()
        {
            // Surface-appropriate impact sound
            if (surfaceAudioMapper != null)
            {
                // Determine the surface type of the attach point (best-effort)
                SurfaceType surface = GetAttachedSurfaceType();
                surfaceAudioMapper.PlayGrappleImpact(surface, grappleController.AttachPoint);
            }

            // Rope snap
            PlayOneShot(ropeSnapClip, 0.9f, Random.Range(0.95f, 1.05f));
        }

        private void HandleGrappleReleased()
        {
            // Pitch based on velocity magnitude at release
            Rigidbody rb = GetComponentInParent<Rigidbody>();
            float speed  = rb != null ? rb.velocity.magnitude : 0f;
            float pitch  = Mathf.Lerp(0.7f, 1.3f, Mathf.Clamp01(speed / 30f));

            releaseSource.clip   = releaseClip;
            releaseSource.pitch  = pitch;
            releaseSource.volume = 0.8f;
            if (releaseClip != null) releaseSource.Play();

            // Stop tension sound
            SetTensionAudible(false, immediate: true);
        }

        private void HandleRopeTension(float tension)
        {
            currentTension = tension;

            // Pitch and volume on the tension loop
            if (tensionSource.clip != null)
            {
                tensionSource.pitch  = Mathf.Lerp(0.7f, 1.4f, tension);
                tensionSource.volume = Mathf.Lerp(0f, 0.7f, tension) * (tensionAudible ? 1f : 0f);
            }

            if (tension > tensionFadeInThreshold && !tensionAudible)
                SetTensionAudible(true, immediate: false);
            else if (tension < tensionFadeOutThreshold && tensionAudible)
                SetTensionAudible(false, immediate: false);
        }

        // ── Max Length Tracking ──────────────────────────────────────────────

        private void TrackRopeLength()
        {
            if (!grappleController.IsAttached)
            {
                prevAtMaxLength = false;
                return;
            }

            float ropeLen = grappleController.CurrentRopeLength;
            float maxLen  = 50f;
            bool atMax    = ropeLen >= maxLen - 0.1f;

            if (atMax && !prevAtMaxLength)
                PlayOneShot(ropeStrainPingClip, 0.7f, Random.Range(0.95f, 1.05f));

            prevAtMaxLength = atMax;
            prevRopeLength  = ropeLen;
        }

        // ── Tension Fade ─────────────────────────────────────────────────────

        private void SetTensionAudible(bool audible, bool immediate)
        {
            tensionAudible = audible;

            if (tensionFadeCoroutine != null) StopCoroutine(tensionFadeCoroutine);

            if (immediate)
            {
                if (tensionSource != null) tensionSource.volume = 0f;
                return;
            }

            tensionFadeCoroutine = StartCoroutine(FadeTensionSource(audible ? currentTension * 0.7f : 0f, 0.1f));
        }

        private IEnumerator FadeTensionSource(float target, float duration)
        {
            if (tensionSource == null) yield break;

            float start   = tensionSource.volume;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                tensionSource.volume = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            tensionSource.volume = target;
            tensionFadeCoroutine = null;
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private SurfaceType GetAttachedSurfaceType()
        {
            // Raycast to the attach point to find the surface
            Vector3 origin    = grappleController.AttachPoint + Vector3.up * 0.2f;
            Vector3 direction = (grappleController.AttachPoint - transform.position).normalized;

            if (Physics.Raycast(origin, -Vector3.up, out RaycastHit hit, 1f))
            {
                SurfaceAnchorPoint anchor = hit.collider.GetComponent<SurfaceAnchorPoint>();
                if (anchor != null) return anchor.AnchorSurfaceType;

                SurfaceProperties props = hit.collider.GetComponent<SurfaceProperties>();
                if (props != null) return props.Type;
            }

            return SurfaceType.ScaleArmor;
        }

        private void PlayOneShot(AudioClip clip, float volume, float pitch)
        {
            if (clip == null || oneShotSource == null) return;
            oneShotSource.clip   = clip;
            oneShotSource.volume = volume;
            oneShotSource.pitch  = pitch;
            oneShotSource.Play();
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
    }
}
