using UnityEngine;
using UnityEngine.Events;
using TitanAscent.Grapple;
using TitanAscent.Systems;

namespace TitanAscent.Physics
{
    /// <summary>
    /// Detects and rewards swing mastery techniques.
    ///
    /// Monitored techniques
    /// ────────────────────
    /// • PerfectRelease  – grapple released within 0.1 s of the swing apex
    ///                     (highest y point while attached).  Event includes
    ///                     height gained from the most-recent attach point.
    ///
    /// • SlingshotDetected – rope was retracted rapidly during a swing AND
    ///                       release speed exceeds 80 % of max swing speed.
    ///                       Applies a 10 % velocity bonus on detection.
    ///
    /// • ChainSwing        – two attaches within 2 s where the second attach
    ///                       height exceeds the first.  Fires with the running
    ///                       chain count; resets if the 2 s window lapses.
    ///
    /// • MomentumSurge     – speed gain of more than 5 m/s through a single
    ///                       swing arc.  Fires with the peak speed reached.
    ///
    /// Hooks into JuiceController: PerfectRelease triggers TriggerRecovery().
    /// Calls AnalyticsStub for each detected technique.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class SwingAnalyzer : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("References")]
        [SerializeField] private GrappleController grappleController;
        [SerializeField] private JuiceController   juiceController;

        [Header("Thresholds")]
        [SerializeField] private float perfectReleaseWindow    = 0.1f;  // s after apex
        [SerializeField] private float slingshotReleaseRatio   = 0.8f;  // fraction of max swing speed
        [SerializeField] private float slingshotVelocityBonus  = 0.10f; // 10%
        [SerializeField] private float chainSwingMaxGap        = 2f;    // s
        [SerializeField] private float momentumSurgeThreshold  = 5f;    // m/s gain

        [Header("Retraction detection")]
        [SerializeField] private float rapidRetractThreshold   = 6f;    // m/s rope shortening rate

        [Header("Events")]
        public UnityEvent<float> OnPerfectRelease;      // height gain (m)
        public UnityEvent        OnSlingshotDetected;
        public UnityEvent<int>   OnChainSwing;          // chain count
        public UnityEvent<float> OnMomentumSurge;       // peak speed (m/s)

        // ── Event subscription guard ──────────────────────────────────────────

        private bool _eventsSubscribed;

        // ── Tracked state ─────────────────────────────────────────────────────

        private Rigidbody _rb;

        // Per-swing tracking
        private bool  _isAttached;
        private float _attachHeight;
        private float _swingStartSpeed;
        private float _currentSwingApex;     // highest y while attached
        private float _apexTime;             // time when apex was last updated
        private float _maxSwingSpeed;        // max speed reached in current swing

        // Retraction slingshot tracking
        private float _lastRopeLength;
        private bool  _rapidRetractDetected;

        // Release tracking
        private float _lastReleaseTime;

        // Chain swing tracking
        private float _lastAttachTime;
        private float _lastAttachHeight;
        private int   _chainLength;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();

            if (grappleController == null)
                grappleController = GetComponentInChildren<GrappleController>();
            if (grappleController == null)
                grappleController = FindFirstObjectByType<GrappleController>();

            if (juiceController == null)
                juiceController = FindFirstObjectByType<JuiceController>();
        }

        private void OnEnable()
        {
            if (_eventsSubscribed) return;
            if (grappleController != null)
            {
                grappleController.OnGrappleAttached.AddListener(HandleAttach);
                grappleController.OnGrappleReleased.AddListener(HandleRelease);
                _eventsSubscribed = true;
            }
        }

        private void OnDisable()
        {
            if (!_eventsSubscribed) return;
            if (grappleController != null)
            {
                grappleController.OnGrappleAttached.RemoveListener(HandleAttach);
                grappleController.OnGrappleReleased.RemoveListener(HandleRelease);
            }
            _eventsSubscribed = false;
        }

        private void Update()
        {
            if (!_isAttached) return;

            float currentY = transform.position.y;
            float speed    = _rb.linearVelocity.magnitude;

            // Track apex (highest y point while attached)
            if (currentY > _currentSwingApex)
            {
                _currentSwingApex = currentY;
                _apexTime         = Time.time;
            }

            // Track max swing speed
            if (speed > _maxSwingSpeed)
                _maxSwingSpeed = speed;

            // Detect rapid retraction (slingshot build-up)
            float currentRopeLength = grappleController.CurrentRopeLength;
            float ropeDelta         = _lastRopeLength - currentRopeLength; // positive when shortening
            float retractRate       = ropeDelta / Time.deltaTime;
            if (retractRate >= rapidRetractThreshold)
                _rapidRetractDetected = true;

            _lastRopeLength = currentRopeLength;
        }

        // ── Event handlers ────────────────────────────────────────────────────

        private void HandleAttach()
        {
            float now          = Time.time;
            float currentY     = transform.position.y;

            // ── Chain swing check ─────────────────────────────────────────────
            bool withinWindow  = (now - _lastAttachTime) <= chainSwingMaxGap && _lastAttachTime > 0f;
            bool higherAttach  = currentY > _lastAttachHeight;

            if (withinWindow && higherAttach)
            {
                _chainLength++;
                OnChainSwing?.Invoke(_chainLength);

                AnalyticsStub.TrackClimbStart("chain_swing", $"chain:{_chainLength}");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[SwingAnalyzer] ChainSwing x{_chainLength} at height {currentY:F1}m");
#endif
            }
            else
            {
                // Broken chain or first attach
                _chainLength = 1;
            }

            // Reset per-swing state
            _isAttached          = true;
            _attachHeight        = currentY;
            _currentSwingApex    = currentY;
            _apexTime            = now;
            _swingStartSpeed     = _rb.linearVelocity.magnitude;
            _maxSwingSpeed       = _swingStartSpeed;
            _rapidRetractDetected = false;
            _lastRopeLength      = grappleController.CurrentRopeLength;

            _lastAttachTime      = now;
            _lastAttachHeight    = currentY;
        }

        private void HandleRelease()
        {
            if (!_isAttached) return;

            float now        = Time.time;
            float releaseY   = transform.position.y;
            float speed      = _rb.linearVelocity.magnitude;

            _isAttached      = false;
            _lastReleaseTime = now;

            // ── PerfectRelease ────────────────────────────────────────────────
            // Released within perfectReleaseWindow seconds after the apex was last updated.
            float timeSinceApex = now - _apexTime;
            if (timeSinceApex <= perfectReleaseWindow)
            {
                float heightGain = _currentSwingApex - _attachHeight;

                OnPerfectRelease?.Invoke(heightGain);
                juiceController?.TriggerRecovery();

                AnalyticsStub.TrackHeightRecord(
                    _currentSwingApex,
                    Time.time,
                    0);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[SwingAnalyzer] PerfectRelease — height gain {heightGain:F2}m, timeSinceApex {timeSinceApex:F3}s");
#endif
            }

            // ── MomentumSurge ─────────────────────────────────────────────────
            float speedGain = _maxSwingSpeed - _swingStartSpeed;
            if (speedGain >= momentumSurgeThreshold)
            {
                OnMomentumSurge?.Invoke(_maxSwingSpeed);

                AnalyticsStub.TrackClimbStart("momentum_surge", $"peakSpeed:{_maxSwingSpeed:F1}");

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[SwingAnalyzer] MomentumSurge — peak speed {_maxSwingSpeed:F1} m/s, gain {speedGain:F1} m/s");
#endif
            }

            // ── SlingshotDetected ─────────────────────────────────────────────
            float slingshotSpeedThreshold = _maxSwingSpeed * slingshotReleaseRatio;
            if (_rapidRetractDetected && speed >= slingshotSpeedThreshold)
            {
                // Apply 10 % velocity bonus
                _rb.linearVelocity = _rb.linearVelocity * (1f + slingshotVelocityBonus);

                OnSlingshotDetected?.Invoke();

                AnalyticsStub.TrackClimbStart("slingshot", $"speed:{speed:F1},maxSwing:{_maxSwingSpeed:F1}");

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[SwingAnalyzer] SlingshotDetected — release speed {speed:F1} m/s (>= {slingshotSpeedThreshold:F1} threshold)");
#endif
            }
        }

        // ── Public read-only state ────────────────────────────────────────────

        /// <summary>Time of the most recent grapple release, in Time.time seconds.</summary>
        public float LastReleaseTime => _lastReleaseTime;

        /// <summary>Height at which the last grapple attached.</summary>
        public float LastAttachHeight => _lastAttachHeight;

        /// <summary>Running chain-swing counter.  Resets when the gap exceeds chainSwingMaxGap.</summary>
        public int ChainLength => _chainLength;

        /// <summary>Highest y position reached during the current (or most recent) swing.</summary>
        public float CurrentSwingApex => _currentSwingApex;
    }
}
