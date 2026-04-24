using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using TitanAscent.Environment;

namespace TitanAscent.Player
{
    public enum GripState
    {
        Released,
        Gripping,
        Recovering
    }

    [RequireComponent(typeof(Rigidbody))]
    public class GripSystem : MonoBehaviour
    {
        [Header("Grip Configuration")]
        [SerializeField] private float maxGrip = 100f;
        [SerializeField] private float gripDrainRate = 25f;
        [SerializeField] private float gripRegenDelay = 1.5f;
        [SerializeField] private float gripRegenRate = 30f;
        [SerializeField] private float gripActivationCost = 5f;

        [Header("Friction")]
        [SerializeField] private float baseSlideReduction = 0.7f;
        [SerializeField] private PhysicsMaterial gripMaterial;
        [SerializeField] private PhysicsMaterial normalMaterial;

        [Header("Input")]
        [SerializeField] private KeyCode gripKey = KeyCode.Mouse1;

        [Header("Events")]
        public UnityEvent OnGripActivated;
        public UnityEvent OnGripReleased;
        public UnityEvent OnGripDepleted;

        private Rigidbody rb;
        private PlayerController playerController;
        private Collider playerCollider;

        private GripState currentGripState = GripState.Released;
        private float currentGrip;
        private float gripReleaseTime;

        // Current contact surface information
        private SurfaceProperties currentSurface;
        private bool isOnClimbableSurface;
        private Vector3 surfaceNormal;
        private readonly HashSet<Collider> activeContacts = new HashSet<Collider>();

        public GripState CurrentGripState => currentGripState;
        public float CurrentGrip => currentGrip;
        public float MaxGrip => maxGrip;
        public float GripPercent => maxGrip > 0 ? currentGrip / maxGrip : 0f;
        public bool IsGripping => currentGripState == GripState.Gripping;
        public SurfaceProperties CurrentSurface => currentSurface;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            playerController = GetComponent<PlayerController>();
            playerCollider = GetComponent<Collider>();
            currentGrip = maxGrip;
        }

        private void Update()
        {
            HandleInput();
            UpdateGripState();
        }

        private void FixedUpdate()
        {
            if (IsGripping && isOnClimbableSurface)
                ApplyGripFriction();
        }

        private void HandleInput()
        {
            // Prefer InputHandler (New Input System); fall back to legacy [SerializeField] gripKey.
            TitanAscent.Input.InputHandler ih = TitanAscent.Input.InputHandler.Instance;
            bool gripDown     = ih != null ? ih.GripDown     : Input.GetKeyDown(gripKey);
            bool gripReleased = ih != null ? ih.GripReleased : Input.GetKeyUp(gripKey);

            if (gripDown && isOnClimbableSurface && currentGrip > gripActivationCost)
            {
                ActivateGrip();
            }
            else if (gripReleased && IsGripping)
            {
                ReleaseGrip();
            }
        }

        private void UpdateGripState()
        {
            switch (currentGripState)
            {
                case GripState.Gripping:
                    DrainGrip();
                    break;

                case GripState.Released:
                    // Nothing special; contact detection handles activation readiness
                    break;

                case GripState.Recovering:
                    RegenerateGrip();
                    break;
            }
        }

        private void ActivateGrip()
        {
            if (currentGripState == GripState.Recovering) return;

            currentGripState = GripState.Gripping;
            currentGrip -= gripActivationCost;

            if (playerCollider != null && gripMaterial != null)
                playerCollider.material = gripMaterial;

            OnGripActivated?.Invoke();
        }

        private void ReleaseGrip()
        {
            if (currentGripState != GripState.Gripping) return;

            currentGripState = currentGrip <= 0 ? GripState.Recovering : GripState.Released;
            gripReleaseTime = Time.time;

            if (playerCollider != null && normalMaterial != null)
                playerCollider.material = normalMaterial;

            OnGripReleased?.Invoke();
        }

        private void DrainGrip()
        {
            if (!isOnClimbableSurface)
            {
                ReleaseGrip();
                return;
            }

            float drainMultiplier = currentSurface != null ? currentSurface.GripMultiplier : 1f;
            currentGrip -= gripDrainRate * drainMultiplier * Time.deltaTime;

            if (currentGrip <= 0f)
            {
                currentGrip = 0f;
                currentGripState = GripState.Recovering;
                OnGripDepleted?.Invoke();
            }
        }

        private void RegenerateGrip()
        {
            float timeSinceRelease = Time.time - gripReleaseTime;
            if (timeSinceRelease < gripRegenDelay) return;

            currentGrip = Mathf.Min(maxGrip, currentGrip + gripRegenRate * Time.deltaTime);

            if (currentGrip >= maxGrip)
            {
                currentGrip = maxGrip;
                currentGripState = GripState.Released;
            }
        }

        private void ApplyGripFriction()
        {
            float frictionCoeff = currentSurface != null ? currentSurface.FrictionCoefficient : baseSlideReduction;

            // Project velocity onto the surface normal to find sliding component
            Vector3 normalVelocity = Vector3.Project(rb.linearVelocity, surfaceNormal);
            Vector3 slideVelocity = rb.linearVelocity - normalVelocity;

            // Reduce slide velocity based on grip and surface friction
            float reductionFactor = frictionCoeff * GripPercent * baseSlideReduction;
            Vector3 frictionForce = -slideVelocity * reductionFactor;

            rb.AddForce(frictionForce, ForceMode.Force);
        }

        /// <summary>Sets the grip drain rate at runtime. Used by MovementTuner.</summary>
        public void SetDrainRate(float value) => gripDrainRate = Mathf.Max(0f, value);

        private void OnCollisionEnter(Collision collision)
        {
            activeContacts.Add(collision.collider);
            EvaluateSurface(collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            EvaluateSurface(collision);
        }

        private void OnCollisionExit(Collision collision)
        {
            activeContacts.Remove(collision.collider);
            if (activeContacts.Count == 0)
            {
                isOnClimbableSurface = false;
                currentSurface = null;
                if (IsGripping) ReleaseGrip();
            }
        }

        private void EvaluateSurface(Collision collision)
        {
            if (collision.contactCount == 0) return;

            surfaceNormal = collision.contacts[0].normal;
            SurfaceProperties surfaceProps = collision.gameObject.GetComponent<SurfaceProperties>();

            currentSurface = surfaceProps;
            isOnClimbableSurface = surfaceProps != null; // Any surface with properties is climbable to some degree
        }
    }
}
