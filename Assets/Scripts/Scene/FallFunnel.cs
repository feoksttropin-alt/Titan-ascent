using UnityEngine;
using TitanAscent.Systems;

namespace TitanAscent.Scene
{
    /// <summary>
    /// Invisible trigger volume that catches falling players and applies a gentle
    /// horizontal physics nudge toward a target landing zone center.
    ///
    /// Activation conditions:
    ///   - Player enters the collider trigger.
    ///   - Player's vertical velocity is downward and exceeds <see cref="downwardVelocityThreshold"/>.
    ///   - Player does NOT have upward velocity.
    ///
    /// The funnel never teleports the player — all correction is force-based.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class FallFunnel : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        // Inspector
        // -----------------------------------------------------------------------

        [Header("Funnel Settings")]
        [Tooltip("Horizontal force (N/s) applied toward the target center.")]
        [SerializeField] private float funnelStrength = 3f;

        [Tooltip("Maximum horizontal velocity correction capped at this speed (m/s).")]
        [SerializeField] private float maxHorizontalCorrection = 8f;

        [Tooltip("Downward velocity (positive magnitude) required to activate the funnel.")]
        [SerializeField] private float downwardVelocityThreshold = 5f;

        [Header("Target")]
        [Tooltip("Transform at the center of the desired landing zone.")]
        [SerializeField] private Transform targetCenter;

        // -----------------------------------------------------------------------
        // Private State
        // -----------------------------------------------------------------------

        private Rigidbody _playerRb;
        private bool      _isActive;
        private PlaytestLogger _logger;

        // -----------------------------------------------------------------------
        // Lifecycle
        // -----------------------------------------------------------------------

        private void Awake()
        {
            // Ensure the collider on this object is a trigger
            var col = GetComponent<Collider>();
            col.isTrigger = true;

            _logger = FindObjectOfType<PlaytestLogger>();
        }

        // -----------------------------------------------------------------------
        // Trigger Callbacks
        // -----------------------------------------------------------------------

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            var rb = other.GetComponent<Rigidbody>();
            if (rb == null) rb = other.GetComponentInParent<Rigidbody>();
            if (rb == null) return;

            // Only activate for downward falls above the threshold
            if (rb.velocity.y < -downwardVelocityThreshold)
            {
                _playerRb = rb;
                _isActive = true;
            }
        }

        private void OnTriggerStay(Collider other)
        {
            if (!_isActive || _playerRb == null) return;
            if (!other.CompareTag("Player")) return;

            // Deactivate if player gains upward velocity (recovered via grapple etc.)
            if (_playerRb.velocity.y > 0f)
            {
                _isActive = false;
                _playerRb = null;
                return;
            }

            ApplyHorizontalNudge();
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            if (_isActive)
            {
                // Log the nudge event to PlaytestLogger
                float height = _playerRb != null ? _playerRb.position.y : transform.position.y;
                _logger?.LogEvent(LogEventType.RecoverySuccess, height, 0, "FallFunnel nudge applied");
            }

            _isActive = false;
            _playerRb = null;
        }

        // -----------------------------------------------------------------------
        // Core Logic
        // -----------------------------------------------------------------------

        private void FixedUpdate()
        {
            if (!_isActive || _playerRb == null) return;

            // Sanity: stop if player is rising
            if (_playerRb.velocity.y > 0f)
            {
                _isActive = false;
                _playerRb = null;
                return;
            }

            ApplyHorizontalNudge();
        }

        private void ApplyHorizontalNudge()
        {
            if (_playerRb == null) return;

            Vector3 center = targetCenter != null ? targetCenter.position : transform.position;

            // Horizontal direction toward target center
            Vector3 toCenter    = center - _playerRb.position;
            toCenter.y          = 0f;

            if (toCenter.sqrMagnitude < 0.001f) return;

            // Current horizontal velocity
            Vector3 hVel = new Vector3(_playerRb.velocity.x, 0f, _playerRb.velocity.z);

            // Only nudge if we're not already moving fast enough horizontally in the right direction
            float speed = hVel.magnitude;
            if (speed >= maxHorizontalCorrection) return;

            // Apply force toward center
            Vector3 force = toCenter.normalized * funnelStrength;

            // Clamp so we never push beyond maxHorizontalCorrection
            Vector3 projected = hVel + force * Time.fixedDeltaTime;
            if (projected.magnitude > maxHorizontalCorrection)
                projected = projected.normalized * maxHorizontalCorrection;

            // Remove existing horizontal velocity contribution and apply clamped result
            Vector3 correction = projected - hVel;
            _playerRb.AddForce(new Vector3(correction.x, 0f, correction.z), ForceMode.VelocityChange);
        }

        // -----------------------------------------------------------------------
        // Gizmos
        // -----------------------------------------------------------------------

        private void OnDrawGizmos()
        {
            // Draw funnel volume
            var col = GetComponent<Collider>();
            Gizmos.color = new Color(0f, 0.8f, 1f, 0.2f);

            if (col is CapsuleCollider cap)
            {
                // Draw a cylinder approximation
                Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
                Gizmos.DrawWireMesh(
                    CreateCylinderMeshApprox(),
                    Vector3.zero,
                    Quaternion.identity,
                    new Vector3(cap.radius * 2f, cap.height, cap.radius * 2f));
                Gizmos.matrix = Matrix4x4.identity;
            }
            else if (col is BoxCollider box)
            {
                Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
                Gizmos.DrawWireCube(box.center, box.size);
                Gizmos.matrix = Matrix4x4.identity;
            }
            else
            {
                Gizmos.DrawWireSphere(transform.position, 5f);
            }

            // Draw arrow toward target center
            if (targetCenter != null)
            {
                Gizmos.color = new Color(0f, 1f, 0.5f, 0.8f);
                Gizmos.DrawLine(transform.position, targetCenter.position);
                Gizmos.DrawSphere(targetCenter.position, 0.5f);

                // Cone-like lines from funnel top to target
                Gizmos.color = new Color(0f, 0.8f, 1f, 0.5f);
                float radius = 4f;
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * Mathf.PI * 2f / 8f;
                    Vector3 rim = transform.position + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                    Gizmos.DrawLine(rim, targetCenter.position);
                }
            }
        }

        // Minimal mesh for gizmo purposes (avoids a dependency on a mesh asset)
        private static Mesh _cylinderGizmoMesh;
        private static Mesh CreateCylinderMeshApprox()
        {
            if (_cylinderGizmoMesh != null) return _cylinderGizmoMesh;

            // Return a cube mesh as a stand-in for the cylinder gizmo
            _cylinderGizmoMesh = new Mesh();
            _cylinderGizmoMesh.name = "FallFunnel_GizmoProxy";
            return _cylinderGizmoMesh;
        }
    }
}
