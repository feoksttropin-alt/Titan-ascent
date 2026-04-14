using System;
using UnityEngine;
using UnityEngine.Events;

namespace TitanAscent.Systems
{
    public enum FallSeverity
    {
        None,
        Small,
        Medium,
        Large,
        Catastrophic,
        RunEnding
    }

    [Serializable]
    public class FallData
    {
        public float startHeight;
        public float endHeight;
        public float distance;
        public float duration;
        public FallSeverity severity;
    }

    public class FallTracker : MonoBehaviour
    {
        [Header("Fall Thresholds (meters)")]
        [SerializeField] private float smallFallThreshold = 5f;
        [SerializeField] private float mediumFallThreshold = 20f;
        [SerializeField] private float largeFallThreshold = 100f;
        [SerializeField] private float catastrophicFallThreshold = 500f;
        [SerializeField] private float runEndingFallThreshold = 1500f;

        [Header("Detection")]
        [SerializeField] private float fallStartVelocityThreshold = -3f;
        [SerializeField] private float landingVelocityThreshold = -2f;
        [SerializeField] private float groundedCheckDelay = 0.3f;

        [Header("Events")]
        public UnityEvent<FallData> OnFallCompleted;
        public UnityEvent<float> OnFallDistanceUpdate;
        public UnityEvent<float> OnNewHeightRecord;
        public UnityEvent OnEmergencyWindowOpen;

        private bool isFalling = false;
        private float fallStartHeight;
        private float fallStartTime;
        private float bestHeightEver = 0f;
        private float longestFall = 0f;
        private int totalFalls = 0;

        private Rigidbody playerRb;
        private float lastGroundedTime;
        private bool emergencyWindowActive = false;

        public bool IsFalling => isFalling;
        public float CurrentFallDistance => isFalling ? fallStartHeight - transform.position.y : 0f;
        public float BestHeightEver => bestHeightEver;
        public float LongestFall => longestFall;
        public int TotalFalls => totalFalls;

        private void Awake()
        {
            playerRb = GetComponent<Rigidbody>();
        }

        private void Update()
        {
            float currentHeight = transform.position.y;

            // Track best height
            if (currentHeight > bestHeightEver)
            {
                bestHeightEver = currentHeight;
                OnNewHeightRecord?.Invoke(bestHeightEver);
            }

            if (playerRb == null) return;

            float verticalVelocity = playerRb.linearVelocity.y;

            // Detect fall start
            if (!isFalling && verticalVelocity < fallStartVelocityThreshold)
            {
                StartFall(currentHeight);
            }

            // Update active fall
            if (isFalling)
            {
                float currentFallDistance = fallStartHeight - currentHeight;

                if (currentFallDistance > 0f)
                    OnFallDistanceUpdate?.Invoke(currentFallDistance);

                // Open emergency window for large falls
                if (!emergencyWindowActive && currentFallDistance >= largeFallThreshold)
                {
                    emergencyWindowActive = true;
                    OnEmergencyWindowOpen?.Invoke();
                }

                // Detect landing (positive or near-zero velocity while was falling)
                if (verticalVelocity > landingVelocityThreshold && (Time.time - fallStartTime) > groundedCheckDelay)
                {
                    EndFall(currentHeight);
                }
            }
        }

        private void StartFall(float height)
        {
            isFalling = true;
            fallStartHeight = height;
            fallStartTime = Time.time;
            emergencyWindowActive = false;
        }

        private void EndFall(float endHeight)
        {
            isFalling = false;
            float distance = fallStartHeight - endHeight;

            if (distance < smallFallThreshold) return; // Ignore tiny drops

            totalFalls++;
            if (distance > longestFall) longestFall = distance;

            FallData data = new FallData
            {
                startHeight = fallStartHeight,
                endHeight = endHeight,
                distance = distance,
                duration = Time.time - fallStartTime,
                severity = ClassifyFall(distance)
            };

            OnFallCompleted?.Invoke(data);
        }

        private FallSeverity ClassifyFall(float distance)
        {
            if (distance >= runEndingFallThreshold) return FallSeverity.RunEnding;
            if (distance >= catastrophicFallThreshold) return FallSeverity.Catastrophic;
            if (distance >= largeFallThreshold) return FallSeverity.Large;
            if (distance >= mediumFallThreshold) return FallSeverity.Medium;
            if (distance >= smallFallThreshold) return FallSeverity.Small;
            return FallSeverity.None;
        }

        public void ForceEndFall()
        {
            if (isFalling) EndFall(transform.position.y);
        }
    }
}
