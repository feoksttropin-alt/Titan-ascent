using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TitanAscent.Player;

namespace TitanAscent.VFX
{
    /// <summary>
    /// Visual effects for thruster bursts, streams, energy depletion, and recharge.
    /// Subscribes to ThrusterSystem events and manages particle systems accordingly.
    /// </summary>
    public class ThrusterVFX : MonoBehaviour
    {
        [Header("Particle Systems")]
        [SerializeField] private ParticleSystem thrusterBurstPrefab;
        [SerializeField] private ParticleSystem thrusterStreamPrefab;
        [SerializeField] private ParticleSystem depletedFlashPrefab;
        [SerializeField] private ParticleSystem rechargeGlowPrefab;

        [Header("References")]
        [SerializeField] private Transform playerBody;

        [Header("Vignette (Energy Depleted)")]
        [SerializeField] private CanvasGroup vignetteGroup;
        [SerializeField] private Image       vignetteImage;
        [SerializeField] private Color       depletedVignetteColor = new Color(1f, 0f, 0f, 0.55f);

        [Header("Stream Fade Timing")]
        [SerializeField] private float streamFadeInDuration  = 0.1f;
        [SerializeField] private float streamFadeOutDuration = 0.2f;

        [Header("Force Scaling")]
        [SerializeField] private float minThrustForce = 1f;
        [SerializeField] private float maxThrustForce = 20f;

        // Runtime references
        private ThrusterSystem thrusterSystem;

        // Stream coroutine state
        private Coroutine streamFadeCoroutine;
        private bool      streamActive;

        // Recharge coroutine
        private Coroutine rechargeCoroutine;

        // Vignette coroutine
        private Coroutine vignetteCoroutine;

        // Per-burst direction cache
        private Vector3 lastThrustDirection = Vector3.up;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            thrusterSystem = GetComponent<ThrusterSystem>();
            if (thrusterSystem == null)
                thrusterSystem = GetComponentInParent<ThrusterSystem>();

            if (playerBody == null)
                playerBody = transform;

            EnsureVignetteSetup();
        }

        private void OnEnable()
        {
            if (thrusterSystem != null)
            {
                thrusterSystem.OnThrust.AddListener(HandleThrust);
                thrusterSystem.OnEnergyDepleted.AddListener(HandleEnergyDepleted);
                thrusterSystem.OnEnergyRestored.AddListener(HandleEnergyRestored);
            }
        }

        private void OnDisable()
        {
            if (thrusterSystem != null)
            {
                thrusterSystem.OnThrust.RemoveListener(HandleThrust);
                thrusterSystem.OnEnergyDepleted.RemoveListener(HandleEnergyDepleted);
                thrusterSystem.OnEnergyRestored.RemoveListener(HandleEnergyRestored);
            }

            StopStream();
            if (rechargeCoroutine  != null) { StopCoroutine(rechargeCoroutine);  rechargeCoroutine  = null; }
            if (vignetteCoroutine  != null) { StopCoroutine(vignetteCoroutine);  vignetteCoroutine  = null; }
        }

        private void Update()
        {
            UpdateThrusterStream();
        }

        // ── Event Handlers ────────────────────────────────────────────────────

        private void HandleThrust(Vector3 direction)
        {
            lastThrustDirection = direction;
            SpawnBurst(direction);
        }

        private void HandleEnergyDepleted()
        {
            SpawnDepletedFlash();
            ShowDepletedVignette();
        }

        private void HandleEnergyRestored()
        {
            SpawnRechargeGlow();
        }

        // ── Burst (single impulse) ────────────────────────────────────────────

        private void SpawnBurst(Vector3 thrustDirection)
        {
            if (thrusterBurstPrefab == null) return;

            // Burst emits opposite to thrust direction (exhaust)
            Vector3 exhaustDir = -thrustDirection.normalized;
            Quaternion rot = exhaustDir != Vector3.zero
                ? Quaternion.LookRotation(exhaustDir)
                : Quaternion.identity;

            // Place burst at player body position
            thrusterBurstPrefab.transform.position = playerBody != null
                ? playerBody.position
                : transform.position;
            thrusterBurstPrefab.transform.rotation = rot;

            // Scale emission count with force magnitude (direction magnitude carries no force info
            // from event — we use thrusterSystem.EnergyPercent as a proxy for current intensity)
            float forceFraction = thrusterSystem != null
                ? Mathf.InverseLerp(minThrustForce, maxThrustForce, thrustDirection.magnitude * 8f)
                : 0.5f;
            int burstCount = Mathf.RoundToInt(Mathf.Lerp(8f, 40f, forceFraction));

            var emission = thrusterBurstPrefab.emission;
            thrusterBurstPrefab.Emit(burstCount);
        }

        // ── Continuous Stream ─────────────────────────────────────────────────

        private void UpdateThrusterStream()
        {
            if (thrusterSystem == null || thrusterStreamPrefab == null) return;

            bool thrusting = IsThrusterHeld();

            if (thrusting && !streamActive)
            {
                streamActive = true;
                if (streamFadeCoroutine != null) StopCoroutine(streamFadeCoroutine);
                streamFadeCoroutine = StartCoroutine(FadeStream(0f, 1f, streamFadeInDuration, true));
            }
            else if (!thrusting && streamActive)
            {
                streamActive = false;
                if (streamFadeCoroutine != null) StopCoroutine(streamFadeCoroutine);
                streamFadeCoroutine = StartCoroutine(FadeStream(1f, 0f, streamFadeOutDuration, false));
            }

            // Keep stream aimed opposite to last thrust direction
            if (streamActive && thrusterStreamPrefab != null)
            {
                Vector3 exhaustDir = -lastThrustDirection.normalized;
                if (exhaustDir != Vector3.zero)
                    thrusterStreamPrefab.transform.rotation = Quaternion.LookRotation(exhaustDir);

                thrusterStreamPrefab.transform.position = playerBody != null
                    ? playerBody.position
                    : transform.position;
            }
        }

        private bool IsThrusterHeld()
        {
            // Mirror the input logic from ThrusterSystem without duplicating energy checks.
            if (thrusterSystem == null || !thrusterSystem.HasEnergy) return false;

            TitanAscent.Input.InputHandler ih = TitanAscent.Input.InputHandler.Instance;
            if (ih != null)
                return ih.ThrusterUp || ih.ThrusterDown || ih.ThrusterLeft || ih.ThrusterRight;

            // Fallback for editor without InputHandler
            return Input.GetKey(KeyCode.Space)
                || Input.GetKey(KeyCode.LeftShift)
                || Mathf.Abs(Input.GetAxis("Horizontal")) > 0.1f
                || Mathf.Abs(Input.GetAxis("Vertical")) > 0.1f;
        }

        private IEnumerator FadeStream(float from, float to, float duration, bool startPlaying)
        {
            if (thrusterStreamPrefab == null) yield break;

            if (startPlaying && !thrusterStreamPrefab.isPlaying)
                thrusterStreamPrefab.Play();

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float alpha = Mathf.Lerp(from, to, t);
                SetStreamAlpha(alpha);
                yield return null;
            }

            SetStreamAlpha(to);

            if (!startPlaying && to <= 0f)
                thrusterStreamPrefab.Stop();

            streamFadeCoroutine = null;
        }

        private void SetStreamAlpha(float alpha)
        {
            if (thrusterStreamPrefab == null) return;
            var main = thrusterStreamPrefab.main;
            Color c = main.startColor.color;
            c.a = alpha;
            main.startColor = c;
        }

        private void StopStream()
        {
            if (streamFadeCoroutine != null)
            {
                StopCoroutine(streamFadeCoroutine);
                streamFadeCoroutine = null;
            }
            streamActive = false;
            if (thrusterStreamPrefab != null)
                thrusterStreamPrefab.Stop();
        }

        // ── Depleted Flash + Vignette ─────────────────────────────────────────

        private void SpawnDepletedFlash()
        {
            if (depletedFlashPrefab == null) return;

            depletedFlashPrefab.transform.position = playerBody != null
                ? playerBody.position
                : transform.position;
            depletedFlashPrefab.Emit(25);
        }

        private void ShowDepletedVignette()
        {
            if (vignetteGroup == null) return;

            if (vignetteCoroutine != null) StopCoroutine(vignetteCoroutine);
            vignetteCoroutine = StartCoroutine(VignetteFadeCoroutine(0.7f, 0f, 0.1f));
        }

        private IEnumerator VignetteFadeCoroutine(float peakAlpha, float targetAlpha, float duration)
        {
            if (vignetteGroup == null) yield break;
            if (vignetteImage != null)
                vignetteImage.color = depletedVignetteColor;

            vignetteGroup.alpha = peakAlpha;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                vignetteGroup.alpha = Mathf.Lerp(peakAlpha, targetAlpha, elapsed / duration);
                yield return null;
            }
            vignetteGroup.alpha = targetAlpha;
            vignetteCoroutine = null;
        }

        // ── Recharge Glow ─────────────────────────────────────────────────────

        private void SpawnRechargeGlow()
        {
            if (rechargeGlowPrefab == null) return;

            if (rechargeCoroutine != null) StopCoroutine(rechargeCoroutine);
            rechargeCoroutine = StartCoroutine(RechargeGlowCoroutine());
        }

        private IEnumerator RechargeGlowCoroutine()
        {
            if (rechargeGlowPrefab == null) yield break;

            rechargeGlowPrefab.transform.position = playerBody != null
                ? playerBody.position
                : transform.position;

            if (!rechargeGlowPrefab.isPlaying)
                rechargeGlowPrefab.Play();

            // Pulse: fade in over 0.2s, hold 0.15s, fade out over 0.3s
            float fadeIn   = 0.2f;
            float hold     = 0.15f;
            float fadeOut  = 0.3f;
            float elapsed  = 0f;

            // Fade in
            while (elapsed < fadeIn)
            {
                elapsed += Time.deltaTime;
                SetGlowAlpha(rechargeGlowPrefab, Mathf.Lerp(0f, 1f, elapsed / fadeIn));
                yield return null;
            }

            yield return new WaitForSeconds(hold);

            // Fade out
            elapsed = 0f;
            while (elapsed < fadeOut)
            {
                elapsed += Time.deltaTime;
                SetGlowAlpha(rechargeGlowPrefab, Mathf.Lerp(1f, 0f, elapsed / fadeOut));
                yield return null;
            }

            rechargeGlowPrefab.Stop();
            rechargeCoroutine = null;
        }

        private void SetGlowAlpha(ParticleSystem ps, float alpha)
        {
            if (ps == null) return;
            var main = ps.main;
            Color c = main.startColor.color;
            c.a = alpha;
            main.startColor = c;
        }

        // ── Vignette Setup ────────────────────────────────────────────────────

        private void EnsureVignetteSetup()
        {
            // If no canvas group was assigned, try to find one on a child/parent UI object
            if (vignetteGroup == null)
                vignetteGroup = GetComponentInChildren<CanvasGroup>(includeInactive: true);

            if (vignetteGroup != null)
                vignetteGroup.alpha = 0f;
        }
    }
}
