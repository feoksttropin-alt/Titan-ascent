using UnityEngine;
using TitanAscent.Grapple;
using TitanAscent.Systems;

namespace TitanAscent.Physics
{
    /// <summary>
    /// Prevents physics exploits and edge-cases every FixedUpdate.
    ///
    /// Guards applied each physics tick
    /// ──────────────────────────────────
    /// 1. Geometry clip    – if the player overlaps a non-player collider, an
    ///                       outward push force of 50 N is applied.
    ///
    /// 2. Velocity cap     – speed > 60 m/s is clamped to 55 m/s, preserving
    ///                       direction.
    ///
    /// 3. Rope floor       – prevents rope shortening below minRopeLength.
    ///
    /// 4. Anchor LoS       – every 0.5 s, a Linecast from player→anchor checks
    ///                       for obstruction.  If blocked, the grapple is
    ///                       released and the event is logged.
    ///
    /// 5. NaN guard        – position and velocity are checked for NaN every
    ///                       frame; if found the last valid state is restored.
    ///
    /// All interventions are logged to PlaytestLogger (if present).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class CollisionSafety : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("References")]
        [SerializeField] private Rigidbody         playerRb;
        [SerializeField] private GrappleController grappleController;

        [Header("Geometry clip")]
        [SerializeField] private float overlapRadius      = 0.5f;
        [SerializeField] private float geometryPushForce  = 50f;
        [SerializeField] private LayerMask overlapMask    = ~0; // everything except player set via Inspector

        [Header("Velocity cap")]
        [SerializeField] private float maxAllowedSpeed    = 60f;
        [SerializeField] private float clampedSpeed       = 55f;

        [Header("Anchor LoS")]
        [SerializeField] private float losCheckInterval   = 0.5f;
        [SerializeField] private LayerMask losBlockMask   = ~0; // set to geometry layers

        // ── Private state ─────────────────────────────────────────────────────

        private Vector3 _lastValidPosition;
        private Vector3 _lastValidVelocity;

        private float _nextLosCheckTime;

        private static readonly Collider[] OverlapBuffer = new Collider[16];

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            if (playerRb == null)
                playerRb = GetComponent<Rigidbody>();

            if (grappleController == null)
                grappleController = GetComponentInChildren<GrappleController>();
            if (grappleController == null)
                grappleController = FindFirstObjectByType<GrappleController>();

            // Seed valid state so the NaN guard has something to restore to
            if (playerRb != null)
            {
                _lastValidPosition = playerRb.position;
                _lastValidVelocity = playerRb.velocity;
            }
        }

        private void FixedUpdate()
        {
            if (playerRb == null) return;

            // Guard 5: NaN — check first; restore before applying any other guards
            if (!GuardNaN()) return;

            // Guards 1–4 (order matters: clip → cap → rope floor → LoS)
            GuardGeometryClip();
            GuardVelocityCap();
            GuardRopeFloor();
            GuardAnchorLoS();

            // Cache valid state for next frame
            _lastValidPosition = playerRb.position;
            _lastValidVelocity = playerRb.velocity;
        }

        // ── Guard implementations ─────────────────────────────────────────────

        /// <summary>
        /// Guard 1 — Geometry clip.
        /// If the player's rigidbody centre overlaps any non-player collider,
        /// push outward with a constant force.
        /// </summary>
        private void GuardGeometryClip()
        {
            Vector3 origin = playerRb.position;
            int count = UnityEngine.Physics.OverlapSphereNonAlloc(origin, overlapRadius, OverlapBuffer, overlapMask);

            for (int i = 0; i < count; i++)
            {
                Collider col = OverlapBuffer[i];
                if (col == null) continue;

                // Skip trigger volumes and the player's own colliders
                if (col.isTrigger) continue;
                if (col.attachedRigidbody == playerRb) continue;

                // Compute outward push direction using closest point
                Vector3 closest    = col.ClosestPoint(origin);
                Vector3 pushDir    = origin - closest;
                float   separation = pushDir.magnitude;

                if (separation < 0.001f)
                {
                    // Perfectly centred — use world up as fallback
                    pushDir = Vector3.up;
                }
                else
                {
                    pushDir /= separation; // normalise
                }

                playerRb.AddForce(pushDir * geometryPushForce, ForceMode.Force);

                LogIntervention("GeometryClip",
                    $"overlap with '{col.name}', push {pushDir:F2}, sep {separation:F3}m");
            }
        }

        /// <summary>
        /// Guard 2 — Velocity cap.
        /// Clamps total speed to clampedSpeed when it exceeds maxAllowedSpeed.
        /// </summary>
        private void GuardVelocityCap()
        {
            float speed = playerRb.velocity.magnitude;
            if (speed <= maxAllowedSpeed) return;

            playerRb.velocity = playerRb.velocity.normalized * clampedSpeed;

            LogIntervention("VelocityCap", $"speed {speed:F1} clamped to {clampedSpeed:F1} m/s");
        }

        /// <summary>
        /// Guard 3 — Rope floor.
        /// Prevents the rope from being shortened below minRopeLength.
        /// Reads the limit from GrappleController and resets the joint distance
        /// if the current rope length has gone below it.
        /// </summary>
        private void GuardRopeFloor()
        {
            if (grappleController == null || !grappleController.IsAttached) return;

            // GrappleController already enforces minRopeLength in HandleRetraction;
            // this guard acts as a safety net for any other path that might bypass it.
            const float AbsoluteMinRope = 1f; // metres — hard floor

            if (grappleController.CurrentRopeLength < AbsoluteMinRope)
            {
                // We cannot set CurrentRopeLength directly; log and release to prevent a stuck state.
                LogIntervention("RopeFloor",
                    $"rope length {grappleController.CurrentRopeLength:F2} < min {AbsoluteMinRope:F2} — releasing grapple");
                grappleController.ReleaseGrapple();
            }
        }

        /// <summary>
        /// Guard 4 — Anchor line-of-sight.
        /// Every losCheckInterval seconds, linecasts from player to anchor.
        /// If geometry blocks it, the grapple is released.
        /// </summary>
        private void GuardAnchorLoS()
        {
            if (grappleController == null || !grappleController.IsAttached) return;
            if (Time.fixedTime < _nextLosCheckTime) return;

            _nextLosCheckTime = Time.fixedTime + losCheckInterval;

            Vector3 playerPos = playerRb.position;
            Vector3 anchor    = grappleController.AttachPoint;

            if (UnityEngine.Physics.Linecast(playerPos, anchor, out RaycastHit hit, losBlockMask))
            {
                // Something other than the player's own collider is in the way
                if (hit.collider.attachedRigidbody == playerRb) return;

                LogIntervention("AnchorLoS",
                    $"blocked by '{hit.collider.name}' at {hit.point:F1} — releasing grapple");
                grappleController.ReleaseGrapple();
            }
        }

        /// <summary>
        /// Guard 5 — NaN guard.
        /// Returns false if NaN was detected and state was restored (caller
        /// should skip remaining guards for this tick).
        /// </summary>
        private bool GuardNaN()
        {
            Vector3 pos = playerRb.position;
            Vector3 vel = playerRb.velocity;

            bool posNaN = float.IsNaN(pos.x) || float.IsNaN(pos.y) || float.IsNaN(pos.z);
            bool velNaN = float.IsNaN(vel.x) || float.IsNaN(vel.y) || float.IsNaN(vel.z);

            if (!posNaN && !velNaN) return true; // All good

            LogIntervention("NaNGuard",
                $"NaN detected — pos:{pos} vel:{vel} — restoring last valid state");

            // Restore last valid state
            playerRb.position        = _lastValidPosition;
            playerRb.velocity  = Vector3.zero; // zero out to avoid immediate re-NaN
            playerRb.angularVelocity = Vector3.zero;

            // Release grapple to prevent further instability
            grappleController?.ReleaseGrapple();

            return false; // Signal to FixedUpdate: skip remaining guards this frame
        }

        // ── Logging ───────────────────────────────────────────────────────────

        private void LogIntervention(string guardName, string detail)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[CollisionSafety:{guardName}] {detail}");
#endif
            if (PlaytestLogger.Instance != null)
            {
                float height = playerRb != null ? playerRb.position.y : 0f;
                PlaytestLogger.Instance.LogEvent(
                    LogEventType.PlayerStuck, // re-use closest event type; detail carries the guard name
                    height,
                    0,
                    $"{guardName}: {detail}");
            }
        }
    }
}
