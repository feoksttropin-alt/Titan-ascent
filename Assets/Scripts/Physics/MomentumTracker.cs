using UnityEngine;
using System.Collections.Generic;

namespace TitanAscent.Physics
{
    [RequireComponent(typeof(Rigidbody))]
    public class MomentumTracker : MonoBehaviour
    {
        [Header("Tracking Configuration")]
        [SerializeField] private float velocityRecordInterval = 0.1f;
        [SerializeField] private int velocityHistorySize = 20;
        [SerializeField] private float hardImpactThreshold = 8f;
        [SerializeField] private float momentumPreservationFactor = 0.95f;

        [Header("Swing Momentum")]
        [SerializeField] private float swingMomentumMultiplier = 1.2f;
        [SerializeField] private float swingExitBoostWindow = 0.3f;

        private Rigidbody rb;
        private Player.PlayerController playerController;

        private Queue<Vector3> velocityHistory = new Queue<Vector3>();
        private float lastRecordTime;
        private float maxFallSpeed;
        private float maxFallSpeedEver;

        // Track velocity at swing exit for momentum preservation
        private Vector3 velocityAtSwingStart;
        private Vector3 velocityAtSwingEnd;
        private float swingEndTime;
        private bool wasSwinging;

        public float MaxFallSpeed => maxFallSpeed;
        public float MaxFallSpeedEver => maxFallSpeedEver;
        public Vector3 AverageVelocity => GetAverageVelocity();
        public float CurrentSpeed => rb != null ? rb.linearVelocity.magnitude : 0f;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            playerController = GetComponent<Player.PlayerController>();
        }

        private void FixedUpdate()
        {
            if (rb == null) return;

            RecordVelocity();
            TrackFallSpeed();
            PreserveSwingMomentum();
        }

        private void RecordVelocity()
        {
            if (Time.time - lastRecordTime < velocityRecordInterval) return;
            lastRecordTime = Time.time;

            velocityHistory.Enqueue(rb.linearVelocity);
            if (velocityHistory.Count > velocityHistorySize)
                velocityHistory.Dequeue();
        }

        private void TrackFallSpeed()
        {
            float downwardSpeed = -rb.linearVelocity.y;
            if (downwardSpeed > maxFallSpeed)
            {
                maxFallSpeed = downwardSpeed;
                if (downwardSpeed > maxFallSpeedEver)
                    maxFallSpeedEver = downwardSpeed;
            }
        }

        private void PreserveSwingMomentum()
        {
            if (playerController == null) return;

            bool isSwinging = playerController.CurrentState == Player.PlayerState.Swinging;

            if (isSwinging && !wasSwinging)
            {
                // Started swinging
                velocityAtSwingStart = rb.linearVelocity;
            }
            else if (!isSwinging && wasSwinging)
            {
                // Just released swing — record velocity and time
                velocityAtSwingEnd = rb.linearVelocity;
                swingEndTime = Time.time;
            }

            // Apply momentum boost if we recently exited a swing
            if (!isSwinging && Time.time - swingEndTime < swingExitBoostWindow && swingEndTime > 0f)
            {
                float t = 1f - (Time.time - swingEndTime) / swingExitBoostWindow;
                Vector3 boostedVelocity = velocityAtSwingEnd * swingMomentumMultiplier;
                rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, boostedVelocity, t * 0.05f);
            }

            wasSwinging = isSwinging;
        }

        private Vector3 GetAverageVelocity()
        {
            if (velocityHistory.Count == 0) return Vector3.zero;

            Vector3 sum = Vector3.zero;
            foreach (Vector3 v in velocityHistory)
                sum += v;
            return sum / velocityHistory.Count;
        }

        /// <summary>
        /// Resets the tracked max fall speed. Called after landing.
        /// </summary>
        public void ResetFallSpeed()
        {
            maxFallSpeed = 0f;
        }

        /// <summary>
        /// Returns the highest-magnitude velocity recorded in the history window.
        /// Useful for calculating swing arc peak.
        /// </summary>
        public float GetPeakSpeed()
        {
            float peak = 0f;
            foreach (Vector3 v in velocityHistory)
            {
                if (v.magnitude > peak)
                    peak = v.magnitude;
            }
            return peak;
        }

        /// <summary>
        /// Returns the velocity vector recorded at a specific time offset (approximate).
        /// </summary>
        public Vector3 GetVelocityAtOffset(int framesBack)
        {
            if (velocityHistory.Count == 0) return Vector3.zero;

            Vector3[] arr = new Vector3[velocityHistory.Count];
            velocityHistory.CopyTo(arr, 0);
            int index = Mathf.Max(0, arr.Length - 1 - framesBack);
            return arr[index];
        }

        private void OnCollisionEnter(Collision collision)
        {
            float impactMagnitude = collision.relativeVelocity.magnitude;
            if (impactMagnitude >= hardImpactThreshold)
            {
                // Hard impact detected — reset fall speed tracking
                ResetFallSpeed();
            }
        }
    }
}
