using UnityEngine;
using TitanAscent.Grapple;
using TitanAscent.Player;

namespace TitanAscent.Physics
{
    /// <summary>
    /// Preserves momentum through key player transitions.
    ///
    /// Behaviours
    /// ──────────
    /// • Velocity cache    – Stores the exact velocity on the frame the grapple
    ///                       releases so it is available to downstream systems.
    ///
    /// • Surface bounce    – On surface contact, converts a fraction
    ///                       (surfaceBounceFactor = 0.3) of the incoming normal
    ///                       momentum into an upward impulse instead of zeroing
    ///                       it out.
    ///
    /// • Slingshot effect  – Accumulates elastic tension during rapid rope
    ///                       retraction; releases it as a velocity boost at the
    ///                       end of retraction.
    ///
    /// • Air momentum      – While airborne, applies a counter-force that
    ///                       resists external drags killing horizontal momentum
    ///                       (up to airMomentumPreservation = 0.85 of the
    ///                       horizontal speed at the point of going airborne).
    ///
    /// Properties exposed for other systems:
    ///   LastReleaseVelocity      (Vector3)
    ///   CurrentMomentumPreservation (float 0–1)
    ///   IsInSlingshot            (bool)
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class MomentumConservationSystem : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("References")]
        [SerializeField] private GrappleController grappleController;
        [SerializeField] private PlayerController  playerController;

        [Header("Surface bounce")]
        [SerializeField] [Range(0f, 1f)]
        private float surfaceBounceFactor = 0.3f;

        [Header("Slingshot")]
        [SerializeField] private float rapidRetractThreshold  = 5f;   // m/s rope shortening rate to count as rapid
        [SerializeField] private float maxSlingshotAccumulation = 8f; // m/s — cap on accumulated boost
        [SerializeField] private float slingshotDecayRate     = 3f;   // accumulated boost lost per second while NOT retracting

        [Header("Air momentum")]
        [SerializeField] [Range(0f, 1f)]
        private float airMomentumPreservation = 0.85f;

        // ── Private state ─────────────────────────────────────────────────────

        private Rigidbody _rb;

        // Velocity cache
        private Vector3 _lastReleaseVelocity;

        // Airborne tracking
        private bool    _wasAirborne;
        private Vector3 _horizontalVelocityAtLiftoff;

        // Slingshot
        private float   _slingshotAccumulation;   // accumulated tension (m/s)
        private float   _lastRopeLength;
        private bool    _wasRetracting;

        // Momentum preservation factor (exposed as property)
        private float   _currentMomentumPreservation = 1f;

        // ── Public properties ─────────────────────────────────────────────────

        /// <summary>Exact player velocity captured on the grapple-release frame.</summary>
        public Vector3 LastReleaseVelocity => _lastReleaseVelocity;

        /// <summary>
        /// A 0–1 value indicating how well horizontal momentum is currently being
        /// preserved.  1 = full preservation; 0 = fully dissipated.
        /// </summary>
        public float CurrentMomentumPreservation => _currentMomentumPreservation;

        /// <summary>True while the slingshot tension is being actively accumulated.</summary>
        public bool IsInSlingshot => _wasRetracting && _slingshotAccumulation > 0f;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();

            if (grappleController == null)
                grappleController = GetComponentInChildren<GrappleController>();
            if (grappleController == null)
                grappleController = FindFirstObjectByType<GrappleController>();

            if (playerController == null)
                playerController = GetComponent<PlayerController>();
            if (playerController == null)
                playerController = FindFirstObjectByType<PlayerController>();
        }

        private void OnEnable()
        {
            if (grappleController != null)
                grappleController.OnGrappleReleased.AddListener(HandleGrappleReleased);
        }

        private void OnDisable()
        {
            if (grappleController != null)
                grappleController.OnGrappleReleased.RemoveListener(HandleGrappleReleased);
        }

        private void FixedUpdate()
        {
            UpdateSlingshotAccumulation();
            UpdateAirMomentum();
            UpdateMomentumPreservationFactor();
        }

        // ── Event handlers ────────────────────────────────────────────────────

        private void HandleGrappleReleased()
        {
            // Cache velocity on the exact release frame
            _lastReleaseVelocity = _rb.velocity;

            // Release accumulated slingshot tension as an impulse
            if (_slingshotAccumulation > 0.1f)
            {
                Vector3 releaseDir = _rb.velocity.sqrMagnitude > 0.01f
                    ? _rb.velocity.normalized
                    : transform.forward;

                _rb.AddForce(releaseDir * _slingshotAccumulation, ForceMode.VelocityChange);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[MomentumConservation] Slingshot release — boost {_slingshotAccumulation:F2} m/s");
#endif
            }

            _slingshotAccumulation = 0f;
            _wasRetracting         = false;
        }

        // ── Slingshot ─────────────────────────────────────────────────────────

        private void UpdateSlingshotAccumulation()
        {
            if (grappleController == null || !grappleController.IsAttached)
            {
                // Decay any residual accumulation while not attached
                _slingshotAccumulation = Mathf.MoveTowards(
                    _slingshotAccumulation, 0f, slingshotDecayRate * Time.fixedDeltaTime);
                _wasRetracting = false;
                return;
            }

            float currentRope = grappleController.CurrentRopeLength;
            float ropeDelta   = _lastRopeLength - currentRope; // positive when shortening
            float retractRate = ropeDelta / Time.fixedDeltaTime;

            bool isRetractingRapidly = retractRate >= rapidRetractThreshold;

            if (isRetractingRapidly)
            {
                // Accumulate elastic tension proportional to retraction rate
                float gain = (retractRate - rapidRetractThreshold) * Time.fixedDeltaTime;
                _slingshotAccumulation = Mathf.Min(
                    _slingshotAccumulation + gain,
                    maxSlingshotAccumulation);
                _wasRetracting = true;
            }
            else
            {
                // Not retracting rapidly — slowly decay tension
                _slingshotAccumulation = Mathf.MoveTowards(
                    _slingshotAccumulation, 0f, slingshotDecayRate * Time.fixedDeltaTime);
                _wasRetracting = false;
            }

            _lastRopeLength = currentRope;
        }

        // ── Air momentum ──────────────────────────────────────────────────────

        private void UpdateAirMomentum()
        {
            if (playerController == null) return;

            bool isAirborne = playerController.CurrentState == PlayerState.Airborne
                           || playerController.CurrentState == PlayerState.Falling;

            if (!_wasAirborne && isAirborne)
            {
                // Just left the ground (or grapple) — cache horizontal velocity
                Vector3 vel = _rb.velocity;
                _horizontalVelocityAtLiftoff = new Vector3(vel.x, 0f, vel.z);
            }

            if (isAirborne && _horizontalVelocityAtLiftoff.sqrMagnitude > 0.01f)
            {
                // Desired preserved horizontal speed
                float preservedSpeed = _horizontalVelocityAtLiftoff.magnitude * airMomentumPreservation;

                Vector3 currentHorizontal = new Vector3(_rb.velocity.x, 0f, _rb.velocity.z);
                float   currentSpeed      = currentHorizontal.magnitude;

                // Only apply if current speed has dropped below the preserved target
                if (currentSpeed < preservedSpeed)
                {
                    Vector3 preserveDir  = _horizontalVelocityAtLiftoff.normalized;
                    float   speedDeficit = preservedSpeed - currentSpeed;

                    // Apply gentle restoring force — scale by deficit and rb mass
                    float forceMagnitude = speedDeficit * _rb.mass * 2f;
                    _rb.AddForce(preserveDir * forceMagnitude, ForceMode.Force);
                }
            }

            if (!isAirborne)
            {
                // Reset liftoff snapshot when grounded / swinging
                _horizontalVelocityAtLiftoff = Vector3.zero;
            }

            _wasAirborne = isAirborne;
        }

        // ── Momentum preservation factor ──────────────────────────────────────

        private void UpdateMomentumPreservationFactor()
        {
            // Compute 0–1 factor: how close current speed is to the last-release speed
            float releaseSpeed = _lastReleaseVelocity.magnitude;
            if (releaseSpeed < 0.01f)
            {
                _currentMomentumPreservation = 1f;
                return;
            }

            float currentSpeed = _rb.velocity.magnitude;
            _currentMomentumPreservation = Mathf.Clamp01(currentSpeed / releaseSpeed);
        }

        // ── Surface bounce ────────────────────────────────────────────────────

        private void OnCollisionEnter(Collision collision)
        {
            if (_rb == null) return;

            // Find the dominant contact normal
            Vector3 normal = collision.contacts[0].normal;

            // Velocity along the collision normal (inward component)
            Vector3 inwardVelocity = Vector3.Project(collision.relativeVelocity, normal);
            float   impactSpeed    = inwardVelocity.magnitude;

            if (impactSpeed < 0.5f) return; // Too gentle to warrant a bounce

            // Convert a fraction of normal-axis momentum to upward force
            float   bounceImpulse = impactSpeed * surfaceBounceFactor;
            Vector3 bounceDir     = Vector3.up;

            _rb.AddForce(bounceDir * bounceImpulse, ForceMode.VelocityChange);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[MomentumConservation] SurfaceBounce — impact {impactSpeed:F2} m/s → upward boost {bounceImpulse:F2} m/s");
#endif
        }
    }
}
