using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using TitanAscent.Systems;

namespace TitanAscent.Player
{
    public class EmergencyRecovery : MonoBehaviour
    {
        [Header("Emergency Window Settings")]
        [SerializeField] private float baseWindowDuration = 0.5f;
        [SerializeField] private float extendedWindowDuration = 2.0f;
        [SerializeField] private float grappleRangeBonus = 20f;
        [SerializeField] private float grappleForceBonus = 300f;
        [SerializeField] private float activationFallThreshold = 100f; // FallTracker.LargeFall threshold

        [Header("Visual Feedback")]
        [SerializeField] private GameObject emergencyIndicatorObject;
        [SerializeField] private Color emergencyColor = new Color(1f, 0.3f, 0f, 0.8f);

        [Header("Events")]
        public UnityEvent OnEmergencyActivated;
        public UnityEvent OnEmergencyUsed;
        public UnityEvent OnEmergencyExpired;

        private FallTracker fallTracker;
        private Grapple.GrappleController grappleController;

        private bool isEmergencyActive = false;
        private bool hasBeenUsedThisActivation = false;
        private float emergencyStartTime;
        private float currentWindowDuration;
        private Coroutine emergencyCoroutine;

        // Bonus values for external systems to query
        public bool IsActive => isEmergencyActive;
        public float GrappleRangeBonus => isEmergencyActive ? grappleRangeBonus : 0f;
        public float GrappleForceBonus => isEmergencyActive ? grappleForceBonus : 0f;
        public float WindowTimeRemaining => isEmergencyActive
            ? Mathf.Max(0f, currentWindowDuration - (Time.time - emergencyStartTime))
            : 0f;

        private void Awake()
        {
            fallTracker = GetComponent<FallTracker>();
            if (fallTracker == null)
                fallTracker = FindFirstObjectByType<FallTracker>();

            grappleController = GetComponent<Grapple.GrappleController>();
        }

        private void OnEnable()
        {
            if (fallTracker != null)
                fallTracker.OnFallThresholdCrossed += HandleFallThreshold;

            if (grappleController != null)
                grappleController.OnGrappleAttached.AddListener(HandleGrappleUsed);
        }

        private void OnDisable()
        {
            if (fallTracker != null)
                fallTracker.OnFallThresholdCrossed -= HandleFallThreshold;

            if (grappleController != null)
                grappleController.OnGrappleAttached.RemoveListener(HandleGrappleUsed);
        }

        private void HandleFallThreshold(float fallDistance)
        {
            if (fallDistance >= activationFallThreshold && !isEmergencyActive)
            {
                // Extend window for truly catastrophic falls
                float windowDuration = fallDistance >= 500f ? extendedWindowDuration : baseWindowDuration;
                ActivateEmergencyWindow(windowDuration);
            }
        }

        private void ActivateEmergencyWindow(float duration)
        {
            if (isEmergencyActive)
            {
                // Extend the window if already active
                currentWindowDuration = Mathf.Max(currentWindowDuration, duration);
                return;
            }

            isEmergencyActive = true;
            hasBeenUsedThisActivation = false;
            emergencyStartTime = Time.time;
            currentWindowDuration = duration;

            if (emergencyCoroutine != null)
                StopCoroutine(emergencyCoroutine);
            emergencyCoroutine = StartCoroutine(EmergencyWindowCoroutine());

            // Show visual indicator
            if (emergencyIndicatorObject != null)
                emergencyIndicatorObject.SetActive(true);

            OnEmergencyActivated?.Invoke();
        }

        private IEnumerator EmergencyWindowCoroutine()
        {
            float elapsed = 0f;

            while (elapsed < currentWindowDuration && isEmergencyActive)
            {
                elapsed = Time.time - emergencyStartTime;

                // Pulse the visual indicator
                if (emergencyIndicatorObject != null)
                {
                    float pulse = Mathf.PingPong(elapsed * 4f, 1f);
                    Color indicatorColor = Color.Lerp(emergencyColor, Color.clear, pulse * 0.3f);
                    // In a real implementation, set the indicator's material color here
                }

                yield return null;
            }

            ExpireEmergency();
        }

        private void HandleGrappleUsed()
        {
            if (!isEmergencyActive) return;
            if (hasBeenUsedThisActivation) return;

            hasBeenUsedThisActivation = true;
            OnEmergencyUsed?.Invoke();

            // End the emergency window after use
            isEmergencyActive = false;

            if (emergencyCoroutine != null)
            {
                StopCoroutine(emergencyCoroutine);
                emergencyCoroutine = null;
            }

            HideIndicator();
        }

        private void ExpireEmergency()
        {
            if (!isEmergencyActive) return;

            isEmergencyActive = false;
            emergencyCoroutine = null;

            HideIndicator();
            OnEmergencyExpired?.Invoke();
        }

        private void HideIndicator()
        {
            if (emergencyIndicatorObject != null)
                emergencyIndicatorObject.SetActive(false);
        }

        /// <summary>
        /// Public activation entry point called by SceneBootstrapper when FallTracker.OnEmergencyWindowOpen fires.
        /// Uses the extended window duration for a generous recovery window.
        /// </summary>
        public void ActivateWindow() => ActivateEmergencyWindow(extendedWindowDuration);

        /// <summary>Sets the extended emergency window duration at runtime. Used by MovementTuner.</summary>
        public void SetWindowDuration(float seconds) => extendedWindowDuration = Mathf.Max(0.1f, seconds);

        private void OnDrawGizmosSelected()
        {
            if (isEmergencyActive)
            {
                Gizmos.color = emergencyColor;
                Gizmos.DrawWireSphere(transform.position, 1.5f);
            }
        }
    }
}
