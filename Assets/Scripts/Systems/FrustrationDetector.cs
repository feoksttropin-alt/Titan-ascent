using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TitanAscent.Systems
{
    public enum FrustrationPatternType
    {
        RepeatedFallSameArea,
        RapidRestarts,
        LongStuck,
        ThrusterSpam,
        MissStreak
    }

    [Serializable]
    public class FrustrationEvent
    {
        public FrustrationPatternType type;
        public int severity;         // 1–3
        public float height;
        public float sessionTime;
    }

    /// <summary>
    /// Monitors player behaviour for frustration patterns and responds
    /// with narrator cues, temporary difficulty easing, or flagging.
    /// Difficulty ease is temporary and minimal — the game stays punishing.
    /// </summary>
    public class FrustrationDetector : MonoBehaviour
    {
        // ------------------------------------------------------------------
        // Inspector
        // ------------------------------------------------------------------

        [Header("References")]
        [SerializeField] private NarrationSystem narrationSystem;
        [SerializeField] private PlaytestLogger playtestLogger;
        [SerializeField] private Grapple.GrappleAimAssist grappleAimAssist;

        // ------------------------------------------------------------------
        // Thresholds
        // ------------------------------------------------------------------

        // RepeatedFallSameArea
        private const int   RepeatedFallCount       = 3;
        private const float RepeatedFallBand        = 50f;   // metres
        private const float RepeatedFallWindow      = 600f;  // 10 min

        // RapidRestarts
        private const int   RapidRestartCount       = 3;
        private const float RapidRestartWindow      = 300f;  // 5 min
        private const float GrappleForgivenessBonus = 0.20f; // +20 %
        private const float GrappleForgivenessDuration = 300f; // 5 min

        // LongStuck
        private const float LongStuckHeightProgress = 5f;   // metres
        private const float LongStuckWindow         = 480f;  // 8 min

        // ThrusterSpam
        private const int   ThrusterSpamCount       = 5;
        private const float ThrusterSpamWindow      = 120f;  // 2 min

        // MissStreak
        private const int   MissStreakCount         = 5;

        // ------------------------------------------------------------------
        // Events
        // ------------------------------------------------------------------

        public event Action<FrustrationEvent> OnFrustrationDetected;

        // ------------------------------------------------------------------
        // Private state
        // ------------------------------------------------------------------

        // RepeatedFallSameArea: list of (height, time) per fall
        private readonly List<(float height, float time)> recentFalls =
            new List<(float, float)>();

        // RapidRestarts: timestamps of climb-start events
        private readonly List<float> restartTimestamps = new List<float>();

        // LongStuck: track height over time
        private float stuckCheckStartTime;
        private float stuckCheckStartHeight;
        private Coroutine stuckCoroutine;

        // ThrusterSpam: timestamps of thruster-depleted events
        private readonly List<float> thrusterDepletions = new List<float>();

        // MissStreak: consecutive miss count
        private int consecutiveMisses;

        // Grapple forgiveness override
        private bool forgivenessActive;

        // Cached player reference
        private Player.PlayerController _cachedPlayer;

        // ------------------------------------------------------------------
        // Lifecycle
        // ------------------------------------------------------------------

        private void Awake()
        {
            _cachedPlayer = FindFirstObjectByType<Player.PlayerController>();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<FallEndedEvent>(OnFallEnded);
            EventBus.Subscribe<ClimbStartedEvent>(OnClimbStarted);
            EventBus.Subscribe<GrappleAttachedEvent>(OnGrappleAttached);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<FallEndedEvent>(OnFallEnded);
            EventBus.Unsubscribe<ClimbStartedEvent>(OnClimbStarted);
            EventBus.Unsubscribe<GrappleAttachedEvent>(OnGrappleAttached);
        }

        private void OnDestroy()
        {
            if (stuckCoroutine != null)
                StopCoroutine(stuckCoroutine);
        }

        private void Start()
        {
            stuckCheckStartTime   = Time.time;
            stuckCheckStartHeight = GetPlayerHeight();

            stuckCoroutine = StartCoroutine(StuckCheckRoutine());
        }

        // ------------------------------------------------------------------
        // Event handlers
        // ------------------------------------------------------------------

        private void OnFallEnded(FallEndedEvent evt)
        {
            float height = evt.data.startHeight;
            float now    = Time.time;

            // Record fall
            recentFalls.Add((height, now));

            // Prune old entries outside window
            recentFalls.RemoveAll(f => now - f.time > RepeatedFallWindow);

            // Check RepeatedFallSameArea: >= 3 falls within 50 m band
            CheckRepeatedFallSameArea(height, now);
        }

        private void OnClimbStarted(ClimbStartedEvent evt)
        {
            float now = Time.time;
            restartTimestamps.Add(now);

            // Prune old entries
            restartTimestamps.RemoveAll(t => now - t > RapidRestartWindow);

            if (restartTimestamps.Count >= RapidRestartCount)
            {
                CheckRapidRestarts();
            }

            // Reset stuck tracking on new climb
            stuckCheckStartTime   = now;
            stuckCheckStartHeight = GetPlayerHeight();
        }

        private void OnGrappleAttached(GrappleAttachedEvent evt)
        {
            // A successful attach resets the miss streak
            consecutiveMisses = 0;
        }

        // Called externally by GrappleController when a shot misses
        public void RegisterGrappleMiss()
        {
            consecutiveMisses++;
            if (consecutiveMisses >= MissStreakCount)
            {
                FireFrustrationEvent(FrustrationPatternType.MissStreak, 2);
                TriggerNarration("GrappleMissStreak");
                consecutiveMisses = 0; // Reset so we don't spam
            }
        }

        // Called externally by ThrusterSystem when thrusters deplete
        public void RegisterThrusterDepletion()
        {
            float now = Time.time;
            thrusterDepletions.Add(now);
            thrusterDepletions.RemoveAll(t => now - t > ThrusterSpamWindow);

            if (thrusterDepletions.Count >= ThrusterSpamCount)
            {
                FireFrustrationEvent(FrustrationPatternType.ThrusterSpam, 1);
                ShowHintMessage("Thrusters need time to recharge — use them for course corrections, not sustained flight.");
                thrusterDepletions.Clear();
            }
        }

        // ------------------------------------------------------------------
        // Pattern checkers
        // ------------------------------------------------------------------

        private void CheckRepeatedFallSameArea(float latestHeight, float now)
        {
            // Count falls within ±25m of latest fall height within window
            float bandMin = latestHeight - (RepeatedFallBand * 0.5f);
            float bandMax = latestHeight + (RepeatedFallBand * 0.5f);

            int count = 0;
            foreach (var (h, t) in recentFalls)
            {
                if (h >= bandMin && h <= bandMax && now - t <= RepeatedFallWindow)
                    count++;
            }

            if (count >= RepeatedFallCount)
            {
                FireFrustrationEvent(FrustrationPatternType.RepeatedFallSameArea,
                    count >= 5 ? 3 : count >= 4 ? 2 : 1);
                TriggerNarration("RepeatedFailureSameArea");

                // Clear band entries so we don't immediately re-trigger
                recentFalls.RemoveAll(f => f.height >= bandMin && f.height <= bandMax);
            }
        }

        private void CheckRapidRestarts()
        {
            FireFrustrationEvent(FrustrationPatternType.RapidRestarts, 2);

            if (!forgivenessActive)
                StartCoroutine(ApplyTemporaryGrappleForgiveness());

            restartTimestamps.Clear();
        }

        private IEnumerator StuckCheckRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(60f); // check every minute

                float now    = Time.time;
                float height = GetPlayerHeight();
                float elapsed = now - stuckCheckStartTime;
                float progress = height - stuckCheckStartHeight;

                if (elapsed >= LongStuckWindow && Mathf.Abs(progress) < LongStuckHeightProgress)
                {
                    FireFrustrationEvent(FrustrationPatternType.LongStuck, 3);
                    TriggerNarration("LongStuck");

                    if (playtestLogger != null)
                        playtestLogger.LogEvent(LogEventType.PlayerStuck, height, 0, "FrustrationDetector");

                    // Reset baseline
                    stuckCheckStartTime   = now;
                    stuckCheckStartHeight = height;
                }
                else if (Mathf.Abs(progress) >= LongStuckHeightProgress)
                {
                    // Progress made — reset window
                    stuckCheckStartTime   = now;
                    stuckCheckStartHeight = height;
                }
            }
        }

        // ------------------------------------------------------------------
        // Effects
        // ------------------------------------------------------------------

        private IEnumerator ApplyTemporaryGrappleForgiveness()
        {
            forgivenessActive = true;

            // Temporarily widen aim-assist detection radius by GrappleForgivenessBonus fraction
            float originalRadius = 0f;
            if (grappleAimAssist != null)
            {
                originalRadius = grappleAimAssist.DetectionRadius;
                grappleAimAssist.DetectionRadius = originalRadius * (1f + GrappleForgivenessBonus);
            }

            Debug.Log($"[FrustrationDetector] Applying +{GrappleForgivenessBonus * 100}% grapple forgiveness for {GrappleForgivenessDuration}s.");

            yield return new WaitForSeconds(GrappleForgivenessDuration);

            if (grappleAimAssist != null)
                grappleAimAssist.DetectionRadius = originalRadius;

            forgivenessActive = false;
            Debug.Log("[FrustrationDetector] Grapple forgiveness expired.");
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private void FireFrustrationEvent(FrustrationPatternType type, int severity)
        {
            FrustrationEvent fe = new FrustrationEvent
            {
                type        = type,
                severity    = Mathf.Clamp(severity, 1, 3),
                height      = GetPlayerHeight(),
                sessionTime = Time.time
            };

            OnFrustrationDetected?.Invoke(fe);
            Debug.Log($"[FrustrationDetector] Pattern detected: {type}, severity {fe.severity}, height {fe.height:F0}m");
        }

        private void TriggerNarration(string key)
        {
            if (narrationSystem == null) return;

            switch (key)
            {
                case "RepeatedFailureSameArea":
                    narrationSystem.TriggerRepeatedFailureSameArea();
                    break;
                case "GrappleMissStreak":
                    narrationSystem.TriggerGrappleMissStreak();
                    break;
                case "LongStuck":
                    narrationSystem.TriggerLongStuck();
                    break;
            }
        }

        private void ShowHintMessage(string message)
        {
            Debug.Log($"[FrustrationDetector] HINT: {message}");
            // Surface through UI system if available
        }

        private float GetPlayerHeight()
        {
            if (_cachedPlayer == null)
                _cachedPlayer = FindFirstObjectByType<Player.PlayerController>();
            return _cachedPlayer != null ? _cachedPlayer.transform.position.y : 0f;
        }
    }
}
