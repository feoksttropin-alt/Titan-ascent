using System.Collections.Generic;
using System.Text;
using UnityEngine;
using TitanAscent.Grapple;
using TitanAscent.Environment;

namespace TitanAscent.Systems
{
    /// <summary>
    /// Tracks per-session statistics.  Subscribes to GrappleController and
    /// FallTracker events during Start().  Call StartSession() to reset all
    /// counters at the beginning of a run.
    /// </summary>
    public class SessionStatsTracker : MonoBehaviour
    {
        [Header("Event Sources")]
        [SerializeField] private GrappleController grappleController;
        [SerializeField] private FallTracker        fallTracker;

        // ── Per-session stats ─────────────────────────────────────────────────────
        private List<float>              heightBandPeaks      = new List<float>();
        private int                      grapplesFired;
        private int                      grapplesAttached;
        private int                      grappleMisses;
        private int                      thrusterBurstsUsed;
        private float                    totalSwingDistance;
        private int                      recoveryAttempts;
        private int                      recoveriesSucceeded;
        private Dictionary<int, float>   timePerZone          = new Dictionary<int, float>();
        private HashSet<SurfaceType>     surfacesContactedThisSession = new HashSet<SurfaceType>();
        private float                    longestSingleSwing;
        private float                    highestFallSpeed;

        // Intermediate swing tracking
        private float currentSwingFrameDistance;

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void Start()
        {
            if (grappleController == null)
                grappleController = FindFirstObjectByType<GrappleController>();

            if (fallTracker == null)
                fallTracker = FindFirstObjectByType<FallTracker>();

            SubscribeToEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        // ── Event wiring ──────────────────────────────────────────────────────────

        private void SubscribeToEvents()
        {
            if (grappleController != null)
            {
                grappleController.OnGrappleAttached.AddListener(OnGrappleAttachedEvent);
                grappleController.OnGrappleReleased.AddListener(OnGrappleReleasedEvent);
            }

            if (fallTracker != null)
            {
                fallTracker.OnFallCompleted.AddListener(OnFallCompleted);
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (grappleController != null)
            {
                grappleController.OnGrappleAttached.RemoveListener(OnGrappleAttachedEvent);
                grappleController.OnGrappleReleased.RemoveListener(OnGrappleReleasedEvent);
            }

            if (fallTracker != null)
            {
                fallTracker.OnFallCompleted.RemoveListener(OnFallCompleted);
            }
        }

        private void OnGrappleAttachedEvent()
        {
            RecordGrappleAttach();
            currentSwingFrameDistance = 0f;
        }

        private void OnGrappleReleasedEvent()
        {
            if (currentSwingFrameDistance > longestSingleSwing)
                longestSingleSwing = currentSwingFrameDistance;

            currentSwingFrameDistance = 0f;
        }

        private void OnFallCompleted(FallData data)
        {
            // Track highest fall speed (approximated from distance / duration)
            if (data.duration > 0f)
            {
                float speed = data.distance / data.duration;
                if (speed > highestFallSpeed)
                    highestFallSpeed = speed;
            }
        }

        // ── Public: session control ───────────────────────────────────────────────

        /// <summary>Resets all per-session statistics. Call at the start of each run.</summary>
        public void StartSession()
        {
            heightBandPeaks.Clear();
            grapplesFired             = 0;
            grapplesAttached          = 0;
            grappleMisses             = 0;
            thrusterBurstsUsed        = 0;
            totalSwingDistance        = 0f;
            recoveryAttempts          = 0;
            recoveriesSucceeded       = 0;
            timePerZone.Clear();
            surfacesContactedThisSession.Clear();
            longestSingleSwing        = 0f;
            highestFallSpeed          = 0f;
            currentSwingFrameDistance = 0f;
        }

        // ── Public: record events ─────────────────────────────────────────────────

        public void RecordGrappleFire()
        {
            grapplesFired++;
        }

        public void RecordGrappleAttach()
        {
            grapplesAttached++;
        }

        public void RecordGrappleMiss()
        {
            grappleMisses++;
            grapplesFired++; // A miss also counts as a fire
        }

        public void RecordThrusterBurst()
        {
            thrusterBurstsUsed++;
        }

        /// <summary>
        /// Accumulates swing arc distance.  Call every frame while the grapple is
        /// attached, passing the distance travelled by the player this frame.
        /// </summary>
        public void RecordSwingDistance(float dist)
        {
            if (dist <= 0f) return;
            totalSwingDistance        += dist;
            currentSwingFrameDistance += dist;
        }

        /// <summary>Call when an emergency recovery is attempted.</summary>
        public void RecordRecoveryAttempt(bool succeeded)
        {
            recoveryAttempts++;
            if (succeeded)
                recoveriesSucceeded++;
        }

        public void UpdateZoneTime(int zoneIndex, float deltaTime)
        {
            if (!timePerZone.ContainsKey(zoneIndex))
                timePerZone[zoneIndex] = 0f;
            timePerZone[zoneIndex] += deltaTime;
        }

        public void RecordSurfaceContact(SurfaceType surface)
        {
            surfacesContactedThisSession.Add(surface);
        }

        public void RecordHeightVisited(float height)
        {
            int band = Mathf.FloorToInt(height / 100f);
            while (heightBandPeaks.Count <= band)
                heightBandPeaks.Add(0f);

            if (height > heightBandPeaks[band])
                heightBandPeaks[band] = height;
        }

        // ── Public: queries ───────────────────────────────────────────────────────

        /// <summary>Returns grapple accuracy as a percentage (0–100).</summary>
        public float GetAccuracyPercent()
        {
            if (grapplesFired == 0) return 0f;
            return (float)grapplesAttached / grapplesFired * 100f;
        }

        /// <summary>Returns a formatted multi-line summary of all session statistics.</summary>
        public string GetSessionSummaryString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("── Session Summary ──────────────────");
            sb.AppendLine($"  Grapples Fired    : {grapplesFired}");
            sb.AppendLine($"  Grapples Attached : {grapplesAttached}");
            sb.AppendLine($"  Grapple Misses    : {grappleMisses}");
            sb.AppendLine($"  Accuracy          : {GetAccuracyPercent():F1}%");
            sb.AppendLine($"  Thruster Bursts   : {thrusterBurstsUsed}");
            sb.AppendLine($"  Total Swing Dist  : {totalSwingDistance:F1}m");
            sb.AppendLine($"  Longest Swing     : {longestSingleSwing:F1}m");
            sb.AppendLine($"  Recovery Attempts : {recoveryAttempts}");
            sb.AppendLine($"  Recoveries OK     : {recoveriesSucceeded}");
            sb.AppendLine($"  Highest Fall Speed: {highestFallSpeed:F1}m/s");

            if (timePerZone.Count > 0)
            {
                sb.AppendLine("  Zone Breakdown    :");
                foreach (KeyValuePair<int, float> kvp in timePerZone)
                    sb.AppendLine($"    Zone {kvp.Key}: {kvp.Value:F0}s");
            }

            if (surfacesContactedThisSession.Count > 0)
            {
                sb.Append("  Surfaces Grappled : ");
                foreach (SurfaceType st in surfacesContactedThisSession)
                    sb.Append($"{st} ");
                sb.AppendLine();
            }

            sb.AppendLine("─────────────────────────────────────");
            return sb.ToString();
        }

        // ── Read-only accessors ───────────────────────────────────────────────────

        public int   GrapplesFired       => grapplesFired;
        public int   GrapplesAttached    => grapplesAttached;
        public int   GrappleMisses       => grappleMisses;
        public int   ThrusterBurstsUsed  => thrusterBurstsUsed;
        public float TotalSwingDistance  => totalSwingDistance;
        public int   RecoveryAttempts    => recoveryAttempts;
        public int   RecoveriesSucceeded => recoveriesSucceeded;
        public float LongestSingleSwing  => longestSingleSwing;
        public float HighestFallSpeed    => highestFallSpeed;
        public IReadOnlyDictionary<int, float> TimePerZone => timePerZone;
        public IReadOnlyCollection<SurfaceType> SurfacesContacted => surfacesContactedThisSession;
    }
}
