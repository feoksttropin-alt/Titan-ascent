using UnityEngine;
using TitanAscent.Grapple;

namespace TitanAscent.Player
{
    /// <summary>
    /// Implements coyote time and input buffering for grapple mechanics.
    ///
    /// Coyote window (0.15 s after leaving a surface):
    ///   - GrappleController checks IsInCoyoteWindow for increased attach forgiveness.
    ///   - No momentum penalty on first grapple fired during this window.
    ///
    /// Input buffer (0.2 s before landing):
    ///   - If the player fires the grapple within 0.2 s before grounding, the grapple
    ///     fires automatically on contact.
    ///
    /// Chain combo (re-fire within 0.1 s of releasing grapple):
    ///   - Re-fired grapple gets a 20 % force bonus for a short window.
    ///
    /// Integrates with GrappleController via events (OnGrappleAttached / OnGrappleReleased)
    /// and with PlayerController (OnLanded / OnTookOff).
    /// </summary>
    public class CoyoteTimeSystem : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        // Tuning constants
        // -----------------------------------------------------------------------

        private const float CoyoteWindowDuration = 0.15f;
        private const float InputBufferDuration = 0.2f;
        private const float ChainComboDuration = 0.1f;

        /// <summary>Force multiplier applied during the chain combo window.</summary>
        public const float ChainComboForceBonus = 1.20f;

        // -----------------------------------------------------------------------
        // Inspector
        // -----------------------------------------------------------------------

        [Header("References (auto-found if null)")]
        [SerializeField] private PlayerController playerController;
        [SerializeField] private GrappleController grappleController;

        [Header("Tuning")]
        [SerializeField] private float coyoteWindowDuration = CoyoteWindowDuration;
        [SerializeField] private float inputBufferDuration = InputBufferDuration;
        [SerializeField] private float chainComboDuration = ChainComboDuration;

        // -----------------------------------------------------------------------
        // Private state
        // -----------------------------------------------------------------------

        private float _coyoteTimer;
        private float _inputBufferTimer;
        private float _chainComboTimer;

        private bool _coyoteWindowActive;
        private bool _inputBufferPending;
        private bool _chainComboActive;

        // -----------------------------------------------------------------------
        // Public properties
        // -----------------------------------------------------------------------

        /// <summary>True while the player is in the post-takeoff coyote time window.</summary>
        public bool IsInCoyoteWindow => _coyoteWindowActive;

        /// <summary>True while a grapple fire input is buffered and waiting for landing.</summary>
        public bool IsInInputBuffer => _inputBufferPending;

        /// <summary>True while the chain combo bonus window is active.</summary>
        public bool ChainComboActive => _chainComboActive;

        // -----------------------------------------------------------------------
        // Lifecycle
        // -----------------------------------------------------------------------

        private void Awake()
        {
            if (playerController == null)
                playerController = GetComponentInParent<PlayerController>()
                    ?? FindFirstObjectByType<PlayerController>();

            if (grappleController == null)
                grappleController = GetComponentInParent<GrappleController>()
                    ?? FindFirstObjectByType<GrappleController>();
        }

        private void OnEnable()
        {
            if (playerController != null)
            {
                playerController.OnLanded.AddListener(OnLanded);
                playerController.OnTookOff.AddListener(OnTookOff);
            }

            if (grappleController != null)
            {
                grappleController.OnGrappleAttached.AddListener(OnGrappleAttached);
                grappleController.OnGrappleReleased.AddListener(OnGrappleReleased);
            }
        }

        private void OnDisable()
        {
            if (playerController != null)
            {
                playerController.OnLanded.RemoveListener(OnLanded);
                playerController.OnTookOff.RemoveListener(OnTookOff);
            }

            if (grappleController != null)
            {
                grappleController.OnGrappleAttached.RemoveListener(OnGrappleAttached);
                grappleController.OnGrappleReleased.RemoveListener(OnGrappleReleased);
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            // Coyote window countdown
            if (_coyoteWindowActive)
            {
                _coyoteTimer -= dt;
                if (_coyoteTimer <= 0f)
                {
                    _coyoteWindowActive = false;
                    _coyoteTimer = 0f;
                }
            }

            // Input buffer countdown
            if (_inputBufferPending)
            {
                _inputBufferTimer -= dt;
                if (_inputBufferTimer <= 0f)
                {
                    _inputBufferPending = false;
                    _inputBufferTimer = 0f;
                }
            }

            // Chain combo countdown
            if (_chainComboActive)
            {
                _chainComboTimer -= dt;
                if (_chainComboTimer <= 0f)
                {
                    _chainComboActive = false;
                    _chainComboTimer = 0f;
                }
            }
        }

        // -----------------------------------------------------------------------
        // Public API — called by GrappleController or InputHandler
        // -----------------------------------------------------------------------

        /// <summary>
        /// Records a grapple fire input for buffering.
        /// If called while the player is in the air, the fire will trigger automatically
        /// on the next landing contact.
        /// </summary>
        public void RegisterGrappleFireInput()
        {
            if (playerController == null) return;

            bool airborne = playerController.CurrentState == PlayerState.Airborne
                         || playerController.CurrentState == PlayerState.Falling
                         || playerController.CurrentState == PlayerState.Swinging;

            if (airborne)
            {
                _inputBufferPending = true;
                _inputBufferTimer = inputBufferDuration;
            }
        }

        /// <summary>
        /// Returns the forgiveness radius multiplier that should be applied to this grapple fire.
        /// Returns a value > 1 during the coyote window.
        /// </summary>
        public float GetForgivenessMultiplier()
        {
            return _coyoteWindowActive ? 1.4f : 1f;
        }

        /// <summary>
        /// Returns the force multiplier that should be applied to this grapple fire.
        /// Returns ChainComboForceBonus during the chain combo window, otherwise 1.
        /// </summary>
        public float GetForceMultiplier()
        {
            return _chainComboActive ? ChainComboForceBonus : 1f;
        }

        // -----------------------------------------------------------------------
        // Event handlers
        // -----------------------------------------------------------------------

        private void OnTookOff()
        {
            // Start coyote window immediately when player leaves surface
            _coyoteWindowActive = true;
            _coyoteTimer = coyoteWindowDuration;
        }

        private void OnLanded()
        {
            // Cancel coyote window on landing
            _coyoteWindowActive = false;
            _coyoteTimer = 0f;

            // If a grapple input was buffered, fire it now
            if (_inputBufferPending)
            {
                _inputBufferPending = false;
                _inputBufferTimer = 0f;
                ExecuteBufferedGrappleFire();
            }
        }

        private void OnGrappleAttached()
        {
            // Grapple attached — cancel coyote window (forgiveness consumed)
            _coyoteWindowActive = false;

            // Chain combo fires when re-attaching, so cancel input buffer
            _inputBufferPending = false;
        }

        private void OnGrappleReleased()
        {
            // Start chain combo window
            _chainComboActive = true;
            _chainComboTimer = chainComboDuration;
        }

        // -----------------------------------------------------------------------
        // Buffered grapple execution
        // -----------------------------------------------------------------------

        private void ExecuteBufferedGrappleFire()
        {
            if (grappleController == null) return;
            grappleController.FireGrapple();
        }
    }
}
