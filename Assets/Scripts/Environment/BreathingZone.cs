using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TitanAscent.Environment
{
    // ── Breath phase enum ─────────────────────────────────────────────────────

    public enum BreathPhase
    {
        Inhale,
        HoldIn,
        Exhale,
        HoldOut
    }

    // ── BreathingZone ─────────────────────────────────────────────────────────

    /// <summary>
    /// Manages the titan's breathing expansion in Zone 8 (The Neck).
    /// A trigger volume that activates when the player enters.
    /// Affected objects oscillate on their local Y axis following a smooth
    /// breathing cycle (inhale → hold → exhale → hold).
    /// Fires OnBreathPhaseChanged(BreathPhase) for WindAudioLayer.
    /// Plays a rumble 0.5 s before each major expansion.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class BreathingZone : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("Breathing Objects")]
        [Tooltip("Transforms that expand/contract with the breathing cycle.")]
        [SerializeField] private List<Transform> breathingObjects = new List<Transform>();

        [Tooltip("Maximum local-Y displacement during full inhale (meters).")]
        [SerializeField] private float breathingAmplitude = 0.8f;

        [Header("Cycle Timings (seconds)")]
        [SerializeField] private float inhaleTime  = 2.5f;
        [SerializeField] private float holdTime    = 0.5f;
        [SerializeField] private float exhaleTime  = 3.5f;
        [SerializeField] private float holdTime2   = 1.0f;

        [Header("Animation Curve")]
        [Tooltip("Drives displacement 0→1 over each inhale/exhale phase.  Should go from 0 to 1.")]
        [SerializeField] private AnimationCurve breathCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Audio")]
        [Tooltip("Short rumble clip played 0.5 s before major expansion.")]
        [SerializeField] private AudioSource rumbleSource;

        [Tooltip("Seconds before inhale start when the rumble fires.")]
        [SerializeField] private float rumbleWarningTime = 0.5f;

        // ── Events ────────────────────────────────────────────────────────────

        /// <summary>Fired each time the breathing phase transitions.</summary>
        public event Action<BreathPhase> OnBreathPhaseChanged;

        // ── Runtime state ─────────────────────────────────────────────────────

        private bool _playerInside;
        private Coroutine _cycleCoroutine;

        // Cached rest positions (local Y) for each breathing object
        private float[] _restPositions;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            Collider col = GetComponent<Collider>();
            col.isTrigger = true;

            CacheRestPositions();
        }

        private void CacheRestPositions()
        {
            _restPositions = new float[breathingObjects.Count];
            for (int i = 0; i < breathingObjects.Count; i++)
            {
                if (breathingObjects[i] != null)
                    _restPositions[i] = breathingObjects[i].localPosition.y;
            }
        }

        // ── Trigger callbacks ─────────────────────────────────────────────────

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            _playerInside = true;

            if (_cycleCoroutine == null)
                _cycleCoroutine = StartCoroutine(BreathingCycleRoutine());
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            _playerInside = false;

            // Keep breathing while player is gone — titan doesn't stop for them.
            // To stop: if (_cycleCoroutine != null) { StopCoroutine(_cycleCoroutine); _cycleCoroutine = null; }
        }

        // ── Breathing cycle ───────────────────────────────────────────────────

        private IEnumerator BreathingCycleRoutine()
        {
            while (true)
            {
                // -- Inhale --
                // Warn 0.5 s before the major expansion begins
                if (rumbleWarningTime > 0f && inhaleTime > rumbleWarningTime)
                {
                    // Fire rumble after a short delay into the hold
                    StartCoroutine(PlayRumbleDelayed(rumbleWarningTime));
                    yield return new WaitForSeconds(rumbleWarningTime);
                }

                FirePhaseEvent(BreathPhase.Inhale);
                yield return StartCoroutine(AnimateObjects(0f, 1f, inhaleTime - rumbleWarningTime));

                // -- Hold in --
                FirePhaseEvent(BreathPhase.HoldIn);
                yield return new WaitForSeconds(holdTime);

                // -- Exhale --
                FirePhaseEvent(BreathPhase.Exhale);
                yield return StartCoroutine(AnimateObjects(1f, 0f, exhaleTime));

                // -- Hold out --
                FirePhaseEvent(BreathPhase.HoldOut);
                yield return new WaitForSeconds(holdTime2);
            }
        }

        private IEnumerator PlayRumbleDelayed(float delay)
        {
            // Already at the start of the pre-warning period; just yield the
            // remaining gap between now and the actual inhale.
            // (Called before the warning delay yield, so it fires immediately.)
            if (rumbleSource != null && rumbleSource.clip != null)
                rumbleSource.PlayOneShot(rumbleSource.clip, 0.7f);
            yield break;
        }

        /// <summary>
        /// Moves breathing objects along their local Y axis from <paramref name="fromNorm"/>
        /// to <paramref name="toNorm"/> (0 = rest, 1 = fully expanded) over <paramref name="duration"/> seconds.
        /// </summary>
        private IEnumerator AnimateObjects(float fromNorm, float toNorm, float duration)
        {
            if (duration <= 0f)
            {
                ApplyDisplacement(toNorm);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t          = Mathf.Clamp01(elapsed / duration);
                float curveValue = breathCurve.Evaluate(t);
                float normValue  = Mathf.Lerp(fromNorm, toNorm, curveValue);
                ApplyDisplacement(normValue);
                yield return null;
            }

            ApplyDisplacement(toNorm);
        }

        private void ApplyDisplacement(float normalised)
        {
            for (int i = 0; i < breathingObjects.Count; i++)
            {
                Transform t = breathingObjects[i];
                if (t == null) continue;

                Vector3 pos = t.localPosition;
                pos.y = _restPositions[i] + normalised * breathingAmplitude;
                t.localPosition = pos;
            }
        }

        private void FirePhaseEvent(BreathPhase phase)
        {
            OnBreathPhaseChanged?.Invoke(phase);
        }

        // ── Gizmos ────────────────────────────────────────────────────────────

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.6f, 0.9f, 1f, 0.2f);
            DrawZoneBox();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.6f, 0.9f, 1f, 0.6f);
            DrawZoneBox();

            // Mark breathing objects
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.8f);
            if (breathingObjects != null)
            {
                foreach (Transform t in breathingObjects)
                {
                    if (t == null) continue;
                    Gizmos.DrawWireSphere(t.position, 0.5f);
                    Gizmos.DrawRay(t.position, Vector3.up * breathingAmplitude);
                }
            }
        }

        private void DrawZoneBox()
        {
            Collider col = GetComponent<Collider>();
            if (col is BoxCollider box)
            {
                Gizmos.matrix = Matrix4x4.TRS(transform.position + box.center, transform.rotation, Vector3.one);
                Gizmos.DrawWireCube(Vector3.zero, box.size);
                Gizmos.matrix = Matrix4x4.identity;
            }
            else
            {
                Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
                Gizmos.DrawWireCube(Vector3.zero, new Vector3(10f, 10f, 10f));
                Gizmos.matrix = Matrix4x4.identity;
            }
        }
    }
}
