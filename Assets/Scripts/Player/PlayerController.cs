using UnityEngine;
using UnityEngine.Events;
using System.Collections;

namespace TitanAscent.Player
{
    public enum PlayerState
    {
        Grounded,
        Airborne,
        Swinging,
        Sliding,
        Falling
    }

    [RequireComponent(typeof(Rigidbody))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Rigidbody Settings")]
        [SerializeField] private float mass = 80f;
        [SerializeField] private float drag = 0.5f;
        [SerializeField] private float angularDrag = 2f;

        [Header("Movement")]
        [SerializeField] private float groundMoveForce = 15f;
        [SerializeField] private float airControlForce = 5f;
        [SerializeField] private float maxGroundSpeed = 10f;
        [SerializeField] private float maxAirSpeed = 20f;

        [Header("Ground Detection")]
        [SerializeField] private float groundCheckDistance = 0.15f;
        [SerializeField] private LayerMask groundLayerMask = ~0;
        [SerializeField] private Transform groundCheckOrigin;

        [Header("State Thresholds")]
        [SerializeField] private float fallSpeedThreshold = -5f;
        [SerializeField] private float slideAngleThreshold = 35f;

        [Header("Events")]
        public UnityEvent OnLanded;
        public UnityEvent OnTookOff;
        public UnityEvent<PlayerState> OnStateChanged;

        // System references
        private Grapple.GrappleController grappleController;
        private ThrusterSystem thrusterSystem;
        private GripSystem gripSystem;

        private Rigidbody rb;
        private PlayerState currentState = PlayerState.Airborne;
        private PlayerState previousState = PlayerState.Airborne;

        // Velocity tracking for momentum conservation
        private Vector3 velocityLastFrame;
        private Vector3 velocityBeforeLanding;
        private Vector3 velocityAtTakeoff;

        // Height tracking
        private float startHeight;
        private float currentHeight;
        private float highestHeight;

        // Ground info
        private bool isGrounded;
        private Vector3 groundNormal = Vector3.up;
        private float groundSlope;

        // State machine timing
        private float timeInCurrentState;
        private float lastGroundedTime;

        public PlayerState CurrentState => currentState;
        public float CurrentHeight => transform.position.y;
        public Vector3 CurrentVelocity => rb.linearVelocity;
        public bool IsGrounded => isGrounded;
        public float HighestHeight => highestHeight;
        public Vector3 VelocityLastFrame => velocityLastFrame;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.mass = mass;
            rb.linearDamping = drag;
            rb.angularDamping = angularDrag;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            grappleController = GetComponent<Grapple.GrappleController>();
            thrusterSystem = GetComponent<ThrusterSystem>();
            gripSystem = GetComponent<GripSystem>();

            startHeight = transform.position.y;
            currentHeight = startHeight;
        }

        private void Update()
        {
            currentHeight = transform.position.y;
            if (currentHeight > highestHeight)
                highestHeight = currentHeight;

            CheckGrounded();
            UpdateState();
            HandleInput();
        }

        private void FixedUpdate()
        {
            ApplyMovementForces();
            velocityLastFrame = rb.linearVelocity;
        }

        private void CheckGrounded()
        {
            Vector3 origin = groundCheckOrigin != null ? groundCheckOrigin.position : transform.position;
            bool wasGrounded = isGrounded;

            if (Physics.SphereCast(origin, 0.25f, Vector3.down, out RaycastHit hit, groundCheckDistance, groundLayerMask))
            {
                isGrounded = true;
                groundNormal = hit.normal;
                groundSlope = Vector3.Angle(groundNormal, Vector3.up);
            }
            else
            {
                isGrounded = false;
                groundNormal = Vector3.up;
                groundSlope = 0f;
            }

            if (!wasGrounded && isGrounded)
            {
                velocityBeforeLanding = velocityLastFrame;
                OnLanded?.Invoke();
                lastGroundedTime = Time.time;
            }
            else if (wasGrounded && !isGrounded)
            {
                velocityAtTakeoff = rb.linearVelocity;
                OnTookOff?.Invoke();
            }
        }

        private void UpdateState()
        {
            PlayerState newState = DetermineState();
            if (newState != currentState)
            {
                previousState = currentState;
                currentState = newState;
                timeInCurrentState = 0f;
                OnStateChanged?.Invoke(currentState);
            }
            else
            {
                timeInCurrentState += Time.deltaTime;
            }
        }

        private PlayerState DetermineState()
        {
            // Swinging takes priority if grapple is attached
            if (grappleController != null && grappleController.IsAttached)
                return PlayerState.Swinging;

            if (isGrounded)
            {
                // Check if sliding on steep surface
                if (groundSlope > slideAngleThreshold)
                    return PlayerState.Sliding;
                return PlayerState.Grounded;
            }

            // Airborne states
            if (rb.linearVelocity.y < fallSpeedThreshold)
                return PlayerState.Falling;

            return PlayerState.Airborne;
        }

        private void HandleInput()
        {
            // Input handling is minimal at PlayerController level;
            // individual systems handle their own input
        }

        private void ApplyMovementForces()
        {
            Vector3 inputDirection = GetInputDirection();

            if (currentState == PlayerState.Grounded)
            {
                // Project move direction onto ground plane
                Vector3 projectedMove = Vector3.ProjectOnPlane(inputDirection, groundNormal).normalized;
                Vector3 currentHorizontalVel = Vector3.ProjectOnPlane(rb.linearVelocity, groundNormal);

                if (currentHorizontalVel.magnitude < maxGroundSpeed)
                    rb.AddForce(projectedMove * groundMoveForce, ForceMode.Force);
            }
            else if (currentState == PlayerState.Airborne || currentState == PlayerState.Falling)
            {
                // Limited air control
                Vector3 horizontalVel = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
                if (horizontalVel.magnitude < maxAirSpeed)
                    rb.AddForce(inputDirection * airControlForce, ForceMode.Force);
            }
            else if (currentState == PlayerState.Sliding)
            {
                // Apply slide acceleration down slope
                Vector3 slideDir = Vector3.ProjectOnPlane(Vector3.down, groundNormal).normalized;
                rb.AddForce(slideDir * 5f, ForceMode.Force);
            }
        }

        private Vector3 GetInputDirection()
        {
            float h = Input.GetAxis("Horizontal");
            float v = Input.GetAxis("Vertical");

            Vector3 camForward = Camera.main != null
                ? Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized
                : transform.forward;
            Vector3 camRight = Camera.main != null
                ? Vector3.ProjectOnPlane(Camera.main.transform.right, Vector3.up).normalized
                : transform.right;

            return (camForward * v + camRight * h).normalized;
        }

        /// <summary>
        /// Applies an external velocity impulse while preserving existing momentum.
        /// Used by systems like GrappleController on release.
        /// </summary>
        public void ApplyMomentumImpulse(Vector3 impulse)
        {
            rb.AddForce(impulse, ForceMode.Impulse);
        }

        /// <summary>
        /// Clamps vertical velocity for safe surface interactions.
        /// </summary>
        public void ClampVerticalVelocity(float minY, float maxY)
        {
            Vector3 v = rb.linearVelocity;
            v.y = Mathf.Clamp(v.y, minY, maxY);
            rb.linearVelocity = v;
        }

        private void OnCollisionEnter(Collision collision)
        {
            // Preserve lateral momentum on surface collision; dampen only the normal component
            Vector3 normal = collision.contacts[0].normal;
            Vector3 normalVel = Vector3.Project(velocityLastFrame, -normal);
            Vector3 tangentVel = velocityLastFrame - normalVel;

            // Allow rigidbody physics to handle the response naturally;
            // grip system handles additional friction modifications
        }
    }
}
