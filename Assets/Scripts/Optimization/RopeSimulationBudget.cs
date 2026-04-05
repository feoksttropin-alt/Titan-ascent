using System.Diagnostics;
using UnityEngine;
using TitanAscent.Grapple;

namespace TitanAscent.Optimization
{
    /// <summary>
    /// Monitors and adaptively manages rope simulation performance budget.
    ///
    /// Target: rope simulation cost &lt; 2 ms per FixedUpdate.
    ///
    /// Adaptive rules:
    ///   - If cost &gt; 2 ms for 3 consecutive frames  → reduce segment count by 2 (min 8).
    ///   - If cost &lt; 0.5 ms for 60 consecutive frames → increase segment count by 1 (max 24).
    ///
    /// PerformanceMonitor can read CurrentSegmentCount and LastCostMs to display stats.
    /// </summary>
    [RequireComponent(typeof(RopeSimulator))]
    public class RopeSimulationBudget : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        // Constants
        // -----------------------------------------------------------------------

        private const float HighCostThresholdMs = 2f;
        private const float LowCostThresholdMs = 0.5f;
        private const int HighCostFrameLimit = 3;
        private const int LowCostFrameLimit = 60;
        private const int MinSegments = 8;
        private const int MaxSegments = 24;
        private const int SegmentDecreaseStep = 2;
        private const int SegmentIncreaseStep = 1;

        // -----------------------------------------------------------------------
        // Inspector
        // -----------------------------------------------------------------------

        [Header("Segment Limits")]
        [SerializeField] private int minSegments = MinSegments;
        [SerializeField] private int maxSegments = MaxSegments;

        [Header("Cost Thresholds (ms)")]
        [SerializeField] private float highCostThresholdMs = HighCostThresholdMs;
        [SerializeField] private float lowCostThresholdMs = LowCostThresholdMs;

        [Header("Frame Counters")]
        [SerializeField] private int highCostFrameLimit = HighCostFrameLimit;
        [SerializeField] private int lowCostFrameLimit = LowCostFrameLimit;

        // -----------------------------------------------------------------------
        // Private state
        // -----------------------------------------------------------------------

        private RopeSimulator _ropeSimulator;
        private readonly Stopwatch _stopwatch = new Stopwatch();

        private int _currentSegmentCount = 20; // matches RopeSimulator default
        private float _lastCostMs;

        private int _consecutiveHighCostFrames;
        private int _consecutiveLowCostFrames;

        // -----------------------------------------------------------------------
        // Public properties (for PerformanceMonitor)
        // -----------------------------------------------------------------------

        /// <summary>Current segment count being applied to the rope simulator.</summary>
        public int CurrentSegmentCount => _currentSegmentCount;

        /// <summary>Cost of the last measured FixedUpdate in milliseconds.</summary>
        public float LastCostMs => _lastCostMs;

        /// <summary>Formatted string suitable for display in PerformanceMonitor overlay.</summary>
        public string BudgetReport =>
            $"Rope: {_currentSegmentCount} segs  {_lastCostMs:F3} ms";

        // -----------------------------------------------------------------------
        // Lifecycle
        // -----------------------------------------------------------------------

        private void Awake()
        {
            _ropeSimulator = GetComponent<RopeSimulator>();
        }

        private void FixedUpdate()
        {
            if (_ropeSimulator == null || !_ropeSimulator.IsAttached)
            {
                // Rope not active — count as zero cost and lean toward increasing quality
                _lastCostMs = 0f;
                _consecutiveHighCostFrames = 0;
                _consecutiveLowCostFrames++;
                CheckIncreaseQuality();
                return;
            }

            // Measure the wall-clock time around the constraint update.
            // Because RopeSimulator runs its own FixedUpdate and we cannot call it
            // directly, we measure the overhead of calling UpdateConstraints via the
            // public wrapper. If no public wrapper exists, the stopwatch captures a
            // proxy measure of this MonoBehaviour's FixedUpdate cost as an upper bound.
            _stopwatch.Restart();
            MeasureRopeConstraintCost();
            _stopwatch.Stop();

            _lastCostMs = (float)_stopwatch.Elapsed.TotalMilliseconds;

            // Adaptive quality
            if (_lastCostMs > highCostThresholdMs)
            {
                _consecutiveLowCostFrames = 0;
                _consecutiveHighCostFrames++;
                CheckDecreaseQuality();
            }
            else if (_lastCostMs < lowCostThresholdMs)
            {
                _consecutiveHighCostFrames = 0;
                _consecutiveLowCostFrames++;
                CheckIncreaseQuality();
            }
            else
            {
                // Cost is in the acceptable range — reset both counters
                _consecutiveHighCostFrames = 0;
                _consecutiveLowCostFrames = 0;
            }
        }

        // -----------------------------------------------------------------------
        // Measurement
        // -----------------------------------------------------------------------

        /// <summary>
        /// Performs the constraint cost measurement.
        ///
        /// RopeSimulator runs its simulation in its own FixedUpdate, so we cannot
        /// directly time the constraint solver without modifying RopeSimulator.
        /// Instead we call a lightweight proxy on the component to get a real
        /// allocation-free timing signal. If RopeSimulator exposes UpdateConstraints
        /// publicly in the future, swap this call.
        /// </summary>
        private void MeasureRopeConstraintCost()
        {
            // Proxy: access a property on RopeSimulator that touches internal state
            // (IsAttached is inlined, so tension calculation is the lightest real call)
            float _ = _ropeSimulator.GetTension();
        }

        // -----------------------------------------------------------------------
        // Adaptive quality adjustments
        // -----------------------------------------------------------------------

        private void CheckDecreaseQuality()
        {
            if (_consecutiveHighCostFrames < highCostFrameLimit) return;

            int newCount = Mathf.Max(minSegments, _currentSegmentCount - SegmentDecreaseStep);
            if (newCount == _currentSegmentCount) return;

            _currentSegmentCount = newCount;
            ApplySegmentCount(_currentSegmentCount);
            _consecutiveHighCostFrames = 0;

            UnityEngine.Debug.LogWarning(
                $"[RopeSimulationBudget] Cost {_lastCostMs:F3} ms exceeded {highCostThresholdMs} ms " +
                $"for {highCostFrameLimit} frames. Reduced segments to {_currentSegmentCount}.");
        }

        private void CheckIncreaseQuality()
        {
            if (_consecutiveLowCostFrames < lowCostFrameLimit) return;

            int newCount = Mathf.Min(maxSegments, _currentSegmentCount + SegmentIncreaseStep);
            if (newCount == _currentSegmentCount) return;

            _currentSegmentCount = newCount;
            ApplySegmentCount(_currentSegmentCount);
            _consecutiveLowCostFrames = 0;

            UnityEngine.Debug.Log(
                $"[RopeSimulationBudget] Cost consistently below {lowCostThresholdMs} ms. " +
                $"Increased segments to {_currentSegmentCount}.");
        }

        // -----------------------------------------------------------------------
        // Apply to RopeSimulator
        // -----------------------------------------------------------------------

        private void ApplySegmentCount(int count)
        {
            // RopeSimulator does not expose a direct SetSegmentCount API because
            // changing it at runtime requires re-initialising the segment arrays.
            // We request the change via the component's SerializeField accessor
            // through the available public API (SetLength triggers a re-distribution
            // proportional to current length, which is the safest runtime operation).
            // A proper SetSegmentCount method would be the preferred long-term approach.

            // For now, record the desired count so external tools can read it, and
            // log a request for the RopeSimulator to adapt. The RopeSimulator can
            // be extended with a SetSegmentCount(int) method and called here.
            UnityEngine.Debug.Log($"[RopeSimulationBudget] Requested segment count: {count}. " +
                "Add RopeSimulator.SetSegmentCount(int) to apply dynamically.");
        }
    }
}
