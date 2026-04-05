using UnityEngine;
using UnityEngine.Events;

namespace TitanAscent.Player
{
    [RequireComponent(typeof(Rigidbody))]
    public class ThrusterSystem : MonoBehaviour
    {
        [Header("Thruster Configuration")]
        [SerializeField] private float maxEnergy = 100f;
        [SerializeField] private float regenRate = 15f;
        [SerializeField] private float thrustForce = 8f;
        [SerializeField] private float thrustCooldown = 0.2f;
        [SerializeField] private float thrustEnergyCost = 12f;

        [Header("Visual/Audio")]
        [SerializeField] private ParticleSystem thrusterParticles;
        [SerializeField] private AudioClip thrusterBurstClip;

        [Header("Events")]
        public UnityEvent OnEnergyDepleted;
        public UnityEvent OnEnergyRestored;
        public UnityEvent<Vector3> OnThrust;

        private Rigidbody rb;
        private PlayerController playerController;

        private float currentEnergy;
        private float lastThrustTime;
        private bool wasDepleted;

        // Recharge doesn't start immediately after using thrust; there's a brief pause
        [SerializeField] private float regenDelay = 0.5f;
        private float lastThrustUsedTime;

        public float CurrentEnergy => currentEnergy;
        public float MaxEnergy => maxEnergy;
        public float EnergyPercent => maxEnergy > 0 ? currentEnergy / maxEnergy : 0f;
        public bool HasEnergy => currentEnergy > 0f;
        public bool IsOnCooldown => Time.time - lastThrustTime < thrustCooldown;
        public bool CanThrust => HasEnergy && !IsOnCooldown && IsAirborne();

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            playerController = GetComponent<PlayerController>();
            currentEnergy = maxEnergy;
        }

        private void Update()
        {
            HandleInput();
            Recharge();
        }

        private void HandleInput()
        {
            if (!CanThrust) return;

            Vector3 direction = Vector3.zero;

            if (Input.GetKey(KeyCode.Space))
                direction += Vector3.up;
            if (Input.GetKey(KeyCode.LeftShift))
                direction += Vector3.down;

            // Directional thrusters from movement input
            float h = Input.GetAxis("Horizontal");
            float v = Input.GetAxis("Vertical");

            if (Camera.main != null)
            {
                Vector3 camForward = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized;
                Vector3 camRight = Vector3.ProjectOnPlane(Camera.main.transform.right, Vector3.up).normalized;
                direction += camForward * v + camRight * h;
            }

            if (direction.sqrMagnitude > 0.01f)
            {
                ApplyThrust(direction.normalized);
            }
        }

        /// <summary>
        /// Applies a directed thrust impulse when the player is airborne.
        /// Does nothing if the player is grounded, energy is depleted, or on cooldown.
        /// </summary>
        public void ApplyThrust(Vector3 direction)
        {
            if (!IsAirborne())
            {
                Debug.Log("[ThrusterSystem] Cannot thrust: player is not airborne.");
                return;
            }

            if (!HasEnergy)
            {
                Debug.Log("[ThrusterSystem] Cannot thrust: energy depleted.");
                return;
            }

            if (IsOnCooldown)
            {
                Debug.Log("[ThrusterSystem] Cannot thrust: on cooldown.");
                return;
            }

            // Consume energy
            float energyCost = thrustEnergyCost * Mathf.Clamp01(direction.magnitude);
            currentEnergy = Mathf.Max(0f, currentEnergy - energyCost);
            lastThrustTime = Time.time;
            lastThrustUsedTime = Time.time;

            // Apply physics impulse
            rb.AddForce(direction.normalized * thrustForce, ForceMode.Impulse);

            // Fire event
            OnThrust?.Invoke(direction);

            // Play particle burst
            if (thrusterParticles != null)
                thrusterParticles.Emit(20);

            // Check depletion
            if (currentEnergy <= 0f && !wasDepleted)
            {
                wasDepleted = true;
                OnEnergyDepleted?.Invoke();
            }
        }

        /// <summary>
        /// Regenerates thruster energy over time. Called each Update.
        /// Will not regenerate during the regen delay period after last use.
        /// </summary>
        public void Recharge()
        {
            if (currentEnergy >= maxEnergy) return;
            if (Time.time - lastThrustUsedTime < regenDelay) return;

            float previousEnergy = currentEnergy;
            currentEnergy = Mathf.Min(maxEnergy, currentEnergy + regenRate * Time.deltaTime);

            // Fire restored event when going from depleted to having energy again
            if (wasDepleted && currentEnergy > 0f)
            {
                wasDepleted = false;
                OnEnergyRestored?.Invoke();
            }
        }

        /// <summary>
        /// Force-set the max energy value (used by ChallengeManager for modifiers).
        /// </summary>
        public void SetMaxEnergy(float value)
        {
            maxEnergy = Mathf.Max(1f, value);
            currentEnergy = Mathf.Min(currentEnergy, maxEnergy);
        }

        /// <summary>Scales max energy by a multiplier. Used by ChallengeManager.</summary>
        public void SetEnergyMultiplier(float multiplier)
        {
            maxEnergy = Mathf.Max(1f, 100f * multiplier);
            currentEnergy = Mathf.Min(currentEnergy, maxEnergy);
        }

        /// <summary>
        /// Instantly fill energy to its current maximum. Used by the debug menu's infinite-thruster toggle.
        /// </summary>
        public void SetEnergyToMax()
        {
            currentEnergy = maxEnergy;
            wasDepleted = false;
        }

        /// <summary>Sets the thrust force applied per impulse. Used by MovementTuner.</summary>
        public void SetThrustForce(float value) => thrustForce = Mathf.Max(0.1f, value);

        /// <summary>Sets the energy regeneration rate. Used by MovementTuner.</summary>
        public void SetRegenRate(float value) => regenRate = Mathf.Max(0f, value);

        private bool IsAirborne()
        {
            if (playerController == null) return true;
            PlayerState state = playerController.CurrentState;
            return state == PlayerState.Airborne
                || state == PlayerState.Falling
                || state == PlayerState.Swinging;
        }

        private void OnDrawGizmosSelected()
        {
            // Show energy as colored gizmo sphere
            Gizmos.color = Color.Lerp(Color.red, Color.cyan, EnergyPercent);
            Gizmos.DrawWireSphere(transform.position, 0.3f);
        }
    }
}
