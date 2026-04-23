using UnityEngine;
using UnityEngine.Events;
using TitanAscent.Systems;

namespace TitanAscent.Player
{
    /// <summary>
    /// Manages player HP, fall damage calculation, passive regen, death, and revival.
    /// Damage is sourced from FallTracker.OnFallCompleted; the severity tier plus
    /// how deep into that tier the fall landed determine the exact hit.
    ///
    /// Health thresholds (default 100 HP):
    ///   Small fall  (5–20 m)    →  5 HP base
    ///   Medium fall (20–100 m)  → 20 HP base
    ///   Large fall  (100–500 m) → 45 HP base
    ///   Catastrophic(500–1500m) → 80 HP base
    ///   Run-ending  (1500 m+)   → 100 HP base (instant death)
    /// Each tier scales linearly from 50% to 100% of its base value based on
    /// how far into the tier the fall lands.
    /// </summary>
    public class PlayerHealth : MonoBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────────────

        [Header("Health")]
        [SerializeField] private float maxHealth    = 100f;
        [SerializeField] private float regenRate    = 5f;   // HP/s when regen is active
        [SerializeField] private float regenDelay   = 3f;   // seconds after last damage

        [Header("Fall Damage (HP per tier)")]
        [SerializeField] private float smallFallDamage        = 5f;
        [SerializeField] private float mediumFallDamage       = 20f;
        [SerializeField] private float largeFallDamage        = 45f;
        [SerializeField] private float catastrophicFallDamage = 80f;
        [SerializeField] private float runEndingFallDamage    = 100f;

        [Header("Events")]
        public UnityEvent<float> OnHealthChanged;  // current HP
        public UnityEvent        OnDeath;
        public UnityEvent        OnRevived;

        // ── State ──────────────────────────────────────────────────────────────

        private float currentHealth;
        private float lastDamageTime = float.NegativeInfinity;
        private bool  isDead;

        private FallTracker fallTracker;

        // ── Public API ─────────────────────────────────────────────────────────

        public float CurrentHealth  => currentHealth;
        public float MaxHealth      => maxHealth;
        public bool  IsDead         => isDead;
        public float HealthPercent  => maxHealth > 0f ? currentHealth / maxHealth : 0f;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            currentHealth = maxHealth;

            fallTracker = GetComponent<FallTracker>();
            if (fallTracker == null)
                fallTracker = FindFirstObjectByType<FallTracker>();
        }

        private void OnEnable()
        {
            if (fallTracker != null)
                fallTracker.OnFallCompleted.AddListener(HandleFallCompleted);
        }

        private void OnDisable()
        {
            if (fallTracker != null)
                fallTracker.OnFallCompleted.RemoveListener(HandleFallCompleted);
        }

        private void Update()
        {
            if (isDead) return;
            if (currentHealth >= maxHealth) return;
            if (Time.time - lastDamageTime < regenDelay) return;

            currentHealth = Mathf.Min(maxHealth, currentHealth + regenRate * Time.deltaTime);
            OnHealthChanged?.Invoke(currentHealth);
        }

        // ── Public methods ─────────────────────────────────────────────────────

        /// <summary>Applies damage from an external source (fall, hazard, etc.).</summary>
        public void TakeDamage(float amount, string source = "unknown")
        {
            if (isDead || amount <= 0f) return;

            currentHealth  -= amount;
            lastDamageTime  = Time.time;

            EventBus.Publish(new PlayerDamagedEvent
            {
                damage          = amount,
                healthRemaining = Mathf.Max(0f, currentHealth),
                source          = source,
            });

            OnHealthChanged?.Invoke(Mathf.Max(0f, currentHealth));

            if (currentHealth <= 0f)
                Die();
        }

        /// <summary>Restores HP up to the maximum.</summary>
        public void Heal(float amount)
        {
            if (isDead || amount <= 0f) return;
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            OnHealthChanged?.Invoke(currentHealth);
        }

        /// <summary>Forcibly sets HP (used by CheckpointManager on respawn).</summary>
        public void SetHealth(float value)
        {
            currentHealth = Mathf.Clamp(value, 0f, maxHealth);
            OnHealthChanged?.Invoke(currentHealth);
        }

        /// <summary>Brings the player back after death at a fraction of max HP.</summary>
        public void Revive(float healthFraction = 0.5f)
        {
            isDead        = false;
            currentHealth = maxHealth * Mathf.Clamp01(healthFraction);
            lastDamageTime = float.NegativeInfinity;
            OnHealthChanged?.Invoke(currentHealth);
            OnRevived?.Invoke();
        }

        // ── Internal ───────────────────────────────────────────────────────────

        private void Die()
        {
            isDead        = true;
            currentHealth = 0f;

            EventBus.Publish(new PlayerDeathEvent
            {
                heightAtDeath = transform.position.y,
                cause         = "fall",
            });

            OnDeath?.Invoke();
        }

        private void HandleFallCompleted(FallData data)
        {
            float damage = ComputeFallDamage(data);
            if (damage > 0f)
                TakeDamage(damage, "fall");
        }

        // ── Damage math ────────────────────────────────────────────────────────

        /// <summary>
        /// Maps fall severity → base damage, then scales linearly by how far
        /// into the severity tier the fall distance lands (50 % at floor, 100 % at ceiling).
        /// </summary>
        private float ComputeFallDamage(FallData data)
        {
            float baseDamage = data.severity switch
            {
                FallSeverity.Small        => smallFallDamage,
                FallSeverity.Medium       => mediumFallDamage,
                FallSeverity.Large        => largeFallDamage,
                FallSeverity.Catastrophic => catastrophicFallDamage,
                FallSeverity.RunEnding    => runEndingFallDamage,
                _                         => 0f,
            };

            if (baseDamage <= 0f) return 0f;

            float floor   = TierFloor(data.severity);
            float ceiling = TierCeiling(data.severity);

            float tierFraction = ceiling > floor
                ? Mathf.Clamp01((data.distance - floor) / (ceiling - floor))
                : 1f;

            return baseDamage * Mathf.Lerp(0.5f, 1f, tierFraction);
        }

        private static float TierFloor(FallSeverity s) => s switch
        {
            FallSeverity.Small        =>     5f,
            FallSeverity.Medium       =>    20f,
            FallSeverity.Large        =>   100f,
            FallSeverity.Catastrophic =>   500f,
            FallSeverity.RunEnding    =>  1500f,
            _                         =>     0f,
        };

        private static float TierCeiling(FallSeverity s) => s switch
        {
            FallSeverity.Small        =>    20f,
            FallSeverity.Medium       =>   100f,
            FallSeverity.Large        =>   500f,
            FallSeverity.Catastrophic =>  1500f,
            FallSeverity.RunEnding    => float.MaxValue,
            _                         =>     0f,
        };
    }
}
