using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace TitanAscent.Systems
{
    public class JuiceController : MonoBehaviour
    {
        [Header("Camera")]
        [SerializeField] private Camera mainCamera;

        [Header("Screen Overlays")]
        [SerializeField] private CanvasGroup screenFlash;
        [SerializeField] private CanvasGroup goldVignette;

        [Header("Particles")]
        [SerializeField] private ParticleSystem ropeSnapParticles;

        [Header("Shake Settings")]
        [SerializeField] private float grappleImpactShakeMag = 0.15f;
        [SerializeField] private float grappleImpactShakeDuration = 0.08f;

        private float shakeIntensity = 0f;
        private float shakeDuration = 0f;
        private float shakeTimer = 0f;
        private Vector3 cameraOriginOffset;
        private float originalFOV;

        private void Awake()
        {
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera != null) originalFOV = mainCamera.fieldOfView;
        }

        private void LateUpdate()
        {
            if (shakeTimer > 0f)
            {
                shakeTimer -= Time.unscaledDeltaTime;
                float t = shakeTimer / shakeDuration;
                Vector3 shake = Random.insideUnitSphere * shakeIntensity * t;
                shake.z = 0f;
                mainCamera.transform.localPosition = cameraOriginOffset + shake;

                if (shakeTimer <= 0f)
                    mainCamera.transform.localPosition = cameraOriginOffset;
            }
        }

        // ── Public API ─────────────────────────────────────────────────

        public void TriggerGrappleImpact()
        {
            StartShake(grappleImpactShakeMag, grappleImpactShakeDuration);
            StartCoroutine(FreezeForSeconds(0.05f));
        }

        public void TriggerHardLanding(float fallDistance)
        {
            // Intensity scaled by Clamp01(distance/500), duration 0.2–0.4s
            float intensity = Mathf.Clamp01(fallDistance / 500f);
            float mag = Mathf.Lerp(0.05f, 0.8f, intensity);
            float dur = Mathf.Lerp(0.2f, 0.4f, intensity);
            StartShake(mag, dur);
        }

        public void TriggerCatastrophicFall()
        {
            StartCoroutine(FOVPulse(0.85f, 0.3f));
        }

        public void TriggerRopeSnap(Vector3 direction)
        {
            if (ropeSnapParticles != null)
            {
                ropeSnapParticles.transform.rotation = Quaternion.LookRotation(direction);
                ropeSnapParticles.Play();
            }
            StartShake(0.1f, 0.06f);
        }

        public void TriggerNewRecord()
        {
            StartCoroutine(FlashOverlay(goldVignette, 0.1f, 0.5f, 0.55f));
            // Brief positive FOV pulse: FOV - 3 for 0.15s then back
            StartCoroutine(FOVNarrowPulse(3f, 0.15f));
        }

        public void TriggerVictory()
        {
            StartCoroutine(FlashOverlay(goldVignette, 0.2f, 1.5f, 0.8f));
            // Slow-mo: 0.3x for 2s, then lerp back to 1f over 0.5s
            StartCoroutine(SlowMotionRamp(0.3f, 1f, 2f, 0.5f));
        }

        public void TriggerRecovery()
        {
            StartCoroutine(FlashOverlay(screenFlash, 0.05f, 0.3f, 1f));
        }

        // ── Internals ──────────────────────────────────────────────────

        private void StartShake(float magnitude, float duration)
        {
            if (mainCamera == null) return;
            cameraOriginOffset = mainCamera.transform.localPosition;
            shakeIntensity = magnitude;
            shakeDuration = duration;
            shakeTimer = duration;
        }

        private IEnumerator FreezFrame(int frames)
        {
            Time.timeScale = 0f;
            for (int i = 0; i < frames; i++)
                yield return new WaitForEndOfFrame();
            Time.timeScale = 1f;
        }

        private IEnumerator FreezeForSeconds(float seconds)
        {
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(seconds);
            Time.timeScale = 1f;
        }

        private IEnumerator FOVPulse(float targetScale, float halfDuration)
        {
            if (mainCamera == null) yield break;
            float startFOV = mainCamera.fieldOfView;
            float targetFOV = startFOV * targetScale;

            float t = 0f;
            while (t < halfDuration)
            {
                t += Time.unscaledDeltaTime;
                mainCamera.fieldOfView = Mathf.Lerp(startFOV, targetFOV, t / halfDuration);
                yield return null;
            }

            t = 0f;
            while (t < halfDuration)
            {
                t += Time.unscaledDeltaTime;
                mainCamera.fieldOfView = Mathf.Lerp(targetFOV, startFOV, t / halfDuration);
                yield return null;
            }
            mainCamera.fieldOfView = startFOV;
        }

        /// <summary>Narrows FOV by <paramref name="fovReduction"/> for <paramref name="holdDuration"/> then snaps back.</summary>
        private IEnumerator FOVNarrowPulse(float fovReduction, float holdDuration)
        {
            if (mainCamera == null) yield break;
            float startFOV = mainCamera.fieldOfView;
            float narrowFOV = startFOV - fovReduction;

            mainCamera.fieldOfView = narrowFOV;
            yield return new WaitForSecondsRealtime(holdDuration);
            mainCamera.fieldOfView = startFOV;
        }

        private IEnumerator FlashOverlay(CanvasGroup group, float fadeIn, float hold, float fadeOut)
        {
            if (group == null) yield break;
            yield return StartCoroutine(FadeGroup(group, 0f, 1f, fadeIn));
            yield return new WaitForSecondsRealtime(hold);
            yield return StartCoroutine(FadeGroup(group, 1f, 0f, fadeOut));
        }

        private IEnumerator FadeGroup(CanvasGroup group, float from, float to, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, to, t / duration);
                yield return null;
            }
            group.alpha = to;
        }

        private IEnumerator SlowMotionRamp(float targetScale, float fromScale, float holdDuration, float rampOutDuration = 0.5f)
        {
            // Instantly snap to slow-mo scale
            Time.timeScale = targetScale;
            Time.fixedDeltaTime = 0.02f * targetScale;

            yield return new WaitForSecondsRealtime(holdDuration);

            // Lerp back to normal over rampOutDuration
            float t = 0f;
            while (t < rampOutDuration)
            {
                t += Time.unscaledDeltaTime;
                Time.timeScale = Mathf.Lerp(targetScale, 1f, t / rampOutDuration);
                Time.fixedDeltaTime = 0.02f * Time.timeScale;
                yield return null;
            }
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
        }
    }
}
