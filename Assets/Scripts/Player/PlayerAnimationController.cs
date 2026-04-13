using System.Collections;
using UnityEngine;
using TitanAscent.Grapple;

namespace TitanAscent.Player
{
    /// <summary>
    /// Drives player visual animations using Transform manipulation.
    /// Works without a full Animator — all poses are Lerped/Slerped over time.
    ///
    /// State-based poses:
    ///   Grounded  : neutral upright
    ///   Swinging  : body leans toward swing direction, arms extend toward grapple anchor
    ///   Airborne  : slight forward lean, arms slightly out
    ///   Sliding   : body tilts in slide direction, arms scramble
    ///   Falling   : arms up/out "oh no", body slightly curled
    ///
    /// Special behaviours:
    ///   Rope arm  : rightArm always points toward grapple anchor while attached.
    ///   Thruster  : brief arm-extension burst when thrusters fire.
    ///   Landing   : squash on body scale (y * 0.8 for 0.1 s, recover over 0.15 s).
    /// </summary>
    public class PlayerAnimationController : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        // Inspector
        // -----------------------------------------------------------------------

        [Header("Body Parts")]
        [SerializeField] private Transform bodyRoot;
        [SerializeField] private Transform leftArm;
        [SerializeField] private Transform rightArm;
        [SerializeField] private Transform legs;

        [Header("Transition Speed")]
        [SerializeField] private float poseTransitionSpeed = 6f;  // 1/0.15 ≈ 6.7

        [Header("References (auto-found if null)")]
        [SerializeField] private PlayerController playerController;
        [SerializeField] private GrappleController grappleController;
        [SerializeField] private ThrusterSystem thrusterSystem;

        // -----------------------------------------------------------------------
        // Target poses (local rotations)
        // -----------------------------------------------------------------------

        // Body root
        private static readonly Quaternion BodyNeutral      = Quaternion.identity;
        private static readonly Quaternion BodyAirborne     = Quaternion.Euler(5f, 0f, 0f);
        private static readonly Quaternion BodyFalling      = Quaternion.Euler(15f, 0f, 0f);

        // Arms — resting
        private static readonly Quaternion LeftArmNeutral   = Quaternion.Euler(0f, 0f, 20f);
        private static readonly Quaternion RightArmNeutral  = Quaternion.Euler(0f, 0f, -20f);

        // Arms — falling ("oh no")
        private static readonly Quaternion LeftArmOhNo      = Quaternion.Euler(-150f, 0f, 40f);
        private static readonly Quaternion RightArmOhNo     = Quaternion.Euler(-150f, 0f, -40f);

        // Arms — airborne
        private static readonly Quaternion LeftArmAirborne  = Quaternion.Euler(0f, 0f, 35f);
        private static readonly Quaternion RightArmAirborne = Quaternion.Euler(0f, 0f, -35f);

        // Arms — thruster burst
        private static readonly Quaternion LeftArmThrust    = Quaternion.Euler(-90f, 0f, 60f);
        private static readonly Quaternion RightArmThrust   = Quaternion.Euler(-90f, 0f, -60f);

        // Arms — scramble (sliding)
        private static readonly Quaternion LeftArmScramble  = Quaternion.Euler(-45f, -30f, 50f);
        private static readonly Quaternion RightArmScramble = Quaternion.Euler(-45f, 30f, -50f);

        // Legs
        private static readonly Quaternion LegsNeutral      = Quaternion.identity;
        private static readonly Quaternion LegsAirborne     = Quaternion.Euler(-10f, 0f, 0f);
        private static readonly Quaternion LegsFalling      = Quaternion.Euler(-20f, 0f, 0f);

        // -----------------------------------------------------------------------
        // Private state
        // -----------------------------------------------------------------------

        private PlayerState _lastState = PlayerState.Airborne;
        private bool _thrusterActive;
        private float _thrusterAnimTimer;
        private const float ThrusterAnimDuration = 0.25f;

        private Coroutine _landingSquashCoroutine;
        private Vector3 _bodyRootBaseScale = Vector3.one;

        // -----------------------------------------------------------------------
        // Lifecycle
        // -----------------------------------------------------------------------

        private void Awake()
        {
            if (playerController == null)
                playerController = GetComponentInParent<PlayerController>() ?? FindFirstObjectByType<PlayerController>();

            if (grappleController == null)
                grappleController = GetComponentInParent<GrappleController>() ?? FindFirstObjectByType<GrappleController>();

            if (thrusterSystem == null)
                thrusterSystem = GetComponentInParent<ThrusterSystem>() ?? FindFirstObjectByType<ThrusterSystem>();

            if (bodyRoot != null)
                _bodyRootBaseScale = bodyRoot.localScale;
        }

        private void OnEnable()
        {
            if (playerController != null)
            {
                playerController.OnLanded.AddListener(OnLanded);
                playerController.OnStateChanged.AddListener(OnStateChanged);
            }

            if (thrusterSystem != null)
                thrusterSystem.OnThrust.AddListener(OnThrust);
        }

        private void OnDisable()
        {
            if (playerController != null)
            {
                playerController.OnLanded.RemoveListener(OnLanded);
                playerController.OnStateChanged.RemoveListener(OnStateChanged);
            }

            if (thrusterSystem != null)
                thrusterSystem.OnThrust.RemoveListener(OnThrust);
        }

        private void Update()
        {
            if (playerController == null) return;

            float dt = Time.deltaTime;
            PlayerState state = playerController.CurrentState;

            UpdateBodyPose(state, dt);
            UpdateArmPose(state, dt);
            UpdateLegPose(state, dt);

            // Tick down thruster animation override
            if (_thrusterActive)
            {
                _thrusterAnimTimer -= dt;
                if (_thrusterAnimTimer <= 0f)
                    _thrusterActive = false;
            }
        }

        // -----------------------------------------------------------------------
        // Pose updates
        // -----------------------------------------------------------------------

        private void UpdateBodyPose(PlayerState state, float dt)
        {
            if (bodyRoot == null) return;

            Quaternion targetRot = BodyNeutral;
            float speed = poseTransitionSpeed;

            switch (state)
            {
                case PlayerState.Grounded:
                    targetRot = BodyNeutral;
                    break;

                case PlayerState.Swinging:
                    // Lean toward the grapple anchor
                    if (grappleController != null && grappleController.IsAttached)
                    {
                        Vector3 toAnchor = grappleController.AttachPoint - bodyRoot.position;
                        // Lean in that direction by up to 25 degrees
                        Vector3 leanDir = new Vector3(toAnchor.x, 0f, toAnchor.z).normalized;
                        float leanAngle = 25f;
                        targetRot = Quaternion.LookRotation(Vector3.forward) *
                                    Quaternion.AngleAxis(-leanAngle, Vector3.Cross(Vector3.up, leanDir));
                    }
                    break;

                case PlayerState.Airborne:
                    targetRot = BodyAirborne;
                    break;

                case PlayerState.Sliding:
                    // Tilt in slide direction (downhill)
                    targetRot = Quaternion.Euler(0f, 0f, 20f);
                    break;

                case PlayerState.Falling:
                    targetRot = BodyFalling;
                    speed = poseTransitionSpeed * 0.7f; // slightly slower for dramatic effect
                    break;
            }

            bodyRoot.localRotation = Quaternion.Slerp(
                bodyRoot.localRotation, targetRot, dt * speed);
        }

        private void UpdateArmPose(PlayerState state, float dt)
        {
            // Thruster animation takes priority
            if (_thrusterActive)
            {
                if (leftArm != null)
                    leftArm.localRotation = Quaternion.Slerp(
                        leftArm.localRotation, LeftArmThrust, dt * poseTransitionSpeed * 2f);
                if (rightArm != null)
                    rightArm.localRotation = Quaternion.Slerp(
                        rightArm.localRotation, RightArmThrust, dt * poseTransitionSpeed * 2f);
                return;
            }

            // Right arm tracks grapple anchor when attached
            bool grappleAttached = grappleController != null && grappleController.IsAttached;
            if (grappleAttached && rightArm != null)
            {
                Vector3 toAnchor = grappleController.AttachPoint - rightArm.position;
                if (toAnchor.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(toAnchor.normalized);
                    rightArm.rotation = Quaternion.Slerp(
                        rightArm.rotation, targetRot, dt * poseTransitionSpeed);
                }
            }

            // Determine left arm (and right arm when not grappling) target rotations
            Quaternion leftTarget = LeftArmNeutral;
            Quaternion rightTarget = RightArmNeutral;

            switch (state)
            {
                case PlayerState.Grounded:
                    leftTarget = LeftArmNeutral;
                    rightTarget = RightArmNeutral;
                    break;

                case PlayerState.Swinging:
                    // Left arm extends toward anchor as well for a two-handed feel
                    if (grappleAttached && leftArm != null && grappleController != null)
                    {
                        Vector3 toAnchor = grappleController.AttachPoint - leftArm.position;
                        leftTarget = Quaternion.LookRotation(toAnchor.normalized);
                    }
                    break;

                case PlayerState.Airborne:
                    leftTarget = LeftArmAirborne;
                    rightTarget = RightArmAirborne;
                    break;

                case PlayerState.Sliding:
                    leftTarget = LeftArmScramble;
                    rightTarget = RightArmScramble;
                    break;

                case PlayerState.Falling:
                    leftTarget = LeftArmOhNo;
                    rightTarget = RightArmOhNo;
                    break;
            }

            if (leftArm != null)
                leftArm.localRotation = Quaternion.Slerp(
                    leftArm.localRotation, leftTarget, dt * poseTransitionSpeed);

            if (!grappleAttached && rightArm != null)
                rightArm.localRotation = Quaternion.Slerp(
                    rightArm.localRotation, rightTarget, dt * poseTransitionSpeed);
        }

        private void UpdateLegPose(PlayerState state, float dt)
        {
            if (legs == null) return;

            Quaternion targetRot = LegsNeutral;

            switch (state)
            {
                case PlayerState.Grounded:
                case PlayerState.Sliding:
                    targetRot = LegsNeutral;
                    break;
                case PlayerState.Swinging:
                case PlayerState.Airborne:
                    targetRot = LegsAirborne;
                    break;
                case PlayerState.Falling:
                    targetRot = LegsFalling;
                    break;
            }

            legs.localRotation = Quaternion.Slerp(
                legs.localRotation, targetRot, dt * poseTransitionSpeed);
        }

        // -----------------------------------------------------------------------
        // Event handlers
        // -----------------------------------------------------------------------

        private void OnLanded()
        {
            if (bodyRoot == null) return;

            if (_landingSquashCoroutine != null)
                StopCoroutine(_landingSquashCoroutine);

            _landingSquashCoroutine = StartCoroutine(LandingSquashCoroutine());
        }

        private void OnStateChanged(PlayerState newState)
        {
            _lastState = newState;
        }

        private void OnThrust(Vector3 direction)
        {
            _thrusterActive = true;
            _thrusterAnimTimer = ThrusterAnimDuration;
        }

        // -----------------------------------------------------------------------
        // Landing squash
        // -----------------------------------------------------------------------

        private IEnumerator LandingSquashCoroutine()
        {
            float squashDuration = 0.1f;
            float recoverDuration = 0.15f;

            Vector3 squashedScale = new Vector3(
                _bodyRootBaseScale.x * 1.1f,
                _bodyRootBaseScale.y * 0.8f,
                _bodyRootBaseScale.z * 1.1f);

            // Squash
            float elapsed = 0f;
            while (elapsed < squashDuration)
            {
                bodyRoot.localScale = Vector3.Lerp(
                    _bodyRootBaseScale, squashedScale, elapsed / squashDuration);
                elapsed += Time.deltaTime;
                yield return null;
            }
            bodyRoot.localScale = squashedScale;

            // Recover
            elapsed = 0f;
            while (elapsed < recoverDuration)
            {
                bodyRoot.localScale = Vector3.Lerp(
                    squashedScale, _bodyRootBaseScale, elapsed / recoverDuration);
                elapsed += Time.deltaTime;
                yield return null;
            }
            bodyRoot.localScale = _bodyRootBaseScale;

            _landingSquashCoroutine = null;
        }
    }
}
