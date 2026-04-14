using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using TitanAscent.Player;

namespace TitanAscent.Systems
{
    /// <summary>
    /// Saves a checkpoint whenever the player surpasses a height milestone
    /// (every <see cref="checkpointInterval"/> metres, default 500 m) and
    /// respawns them there after death.
    ///
    /// Flow:
    ///   1. FallTracker.OnNewHeightRecord fires continuously as the player climbs.
    ///   2. When height crosses the next milestone, CheckpointManager records
    ///      the current position + HP and publishes CheckpointReachedEvent.
    ///   3. PlayerHealth.OnDeath fires → StartRespawn() teleports the player
    ///      back to the checkpoint position after a short fade delay.
    ///   4. SaveManager persists checkpoint data so it survives session restarts.
    /// </summary>
    public class CheckpointManager : MonoBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────────────

        [Header("Checkpoint Spacing")]
        [SerializeField] private float checkpointInterval = 500f; // metres between auto-checkpoints
        [SerializeField] private float firstCheckpointHeight = 500f; // first milestone

        [Header("Respawn")]
        [SerializeField] private float respawnDelay       = 2f;    // seconds before teleport
        [SerializeField] private float respawnHealthFraction = 0.5f;

        [Header("References")]
        [SerializeField] private FallTracker  fallTracker;
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private SaveManager  saveManager;

        [Header("Events")]
        public UnityEvent<float> OnCheckpointSaved;   // checkpoint height
        public UnityEvent        OnRespawnStarted;
        public UnityEvent        OnRespawnComplete;

        // ── State ──────────────────────────────────────────────────────────────

        private Vector3 checkpointPosition;
        private float   checkpointHealth;
        private int     checkpointIndex;
        private float   nextMilestone;

        private bool    isRespawning;
        private Coroutine respawnCoroutine;

        // ── Public API ─────────────────────────────────────────────────────────

        public Vector3 CheckpointPosition => checkpointPosition;
        public float   CheckpointHealth   => checkpointHealth;
        public int     CheckpointIndex    => checkpointIndex;
        public bool    HasCheckpoint      => checkpointIndex > 0;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            // Auto-resolve references if not set in Inspector
            if (fallTracker  == null) fallTracker  = FindFirstObjectByType<FallTracker>();
            if (playerHealth == null) playerHealth  = FindFirstObjectByType<PlayerHealth>();
            if (saveManager  == null) saveManager   = FindFirstObjectByType<SaveManager>();

            // Default spawn is at the player's current position
            checkpointPosition = transform.position;
            checkpointHealth   = playerHealth != null ? playerHealth.MaxHealth : 100f;
            nextMilestone      = firstCheckpointHeight;

            // Restore from saved data if available
            LoadCheckpointFromSave();
        }

        private void OnEnable()
        {
            if (fallTracker  != null) fallTracker.OnNewHeightRecord.AddListener(HandleNewHeightRecord);
            if (playerHealth != null) playerHealth.OnDeath.AddListener(HandleDeath);
        }

        private void OnDisable()
        {
            if (fallTracker  != null) fallTracker.OnNewHeightRecord.RemoveListener(HandleNewHeightRecord);
            if (playerHealth != null) playerHealth.OnDeath.RemoveListener(HandleDeath);
        }

        // ── Event handlers ─────────────────────────────────────────────────────

        private void HandleNewHeightRecord(float height)
        {
            if (height >= nextMilestone)
                SaveCheckpoint(height);
        }

        private void HandleDeath()
        {
            if (isRespawning) return;

            if (HasCheckpoint)
            {
                if (respawnCoroutine != null) StopCoroutine(respawnCoroutine);
                respawnCoroutine = StartCoroutine(RespawnCoroutine());
            }
            // If no checkpoint has been reached yet, GameManager handles game-over
        }

        // ── Checkpoint save ────────────────────────────────────────────────────

        private void SaveCheckpoint(float height)
        {
            // Advance to the next milestone band
            while (nextMilestone <= height)
                nextMilestone += checkpointInterval;

            checkpointIndex++;
            checkpointPosition = new Vector3(
                transform.position.x,
                height,
                transform.position.z);

            checkpointHealth = playerHealth != null ? playerHealth.CurrentHealth : 100f;

            // Persist
            PersistCheckpoint();

            EventBus.Publish(new CheckpointReachedEvent
            {
                checkpointHeight = height,
                checkpointIndex  = checkpointIndex,
            });

            OnCheckpointSaved?.Invoke(height);

            Debug.Log($"[CheckpointManager] Checkpoint #{checkpointIndex} saved at {height:F0}m.");
        }

        // ── Respawn ────────────────────────────────────────────────────────────

        /// <summary>
        /// Forces an immediate respawn at the saved checkpoint.
        /// Can be called by UI buttons (e.g., "Return to Checkpoint").
        /// </summary>
        public void ForceRespawn()
        {
            if (isRespawning || !HasCheckpoint) return;
            if (respawnCoroutine != null) StopCoroutine(respawnCoroutine);
            respawnCoroutine = StartCoroutine(RespawnCoroutine());
        }

        private IEnumerator RespawnCoroutine()
        {
            isRespawning = true;
            OnRespawnStarted?.Invoke();

            yield return new WaitForSeconds(respawnDelay);

            // Teleport player
            transform.position = checkpointPosition;

            // Zero out physics velocity
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb == null) rb = GetComponentInChildren<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity        = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            // Restore health
            if (playerHealth != null)
                playerHealth.Revive(respawnHealthFraction);

            EventBus.Publish(new PlayerRespawnedEvent
            {
                respawnPosition = checkpointPosition,
                healthRestored  = playerHealth != null ? playerHealth.CurrentHealth : 0f,
            });

            OnRespawnComplete?.Invoke();
            isRespawning = false;

            Debug.Log($"[CheckpointManager] Respawned at {checkpointPosition.y:F0}m.");
        }

        // ── Persistence ────────────────────────────────────────────────────────

        private void PersistCheckpoint()
        {
            if (saveManager == null) return;

            SaveData data = saveManager.CurrentData;
            data.checkpointHeight = checkpointPosition.y;
            data.checkpointHealth = checkpointHealth;
            saveManager.Save();
        }

        private void LoadCheckpointFromSave()
        {
            if (saveManager == null) return;

            saveManager.Load();
            SaveData data = saveManager.CurrentData;

            if (data.checkpointHeight > 0f)
            {
                checkpointPosition = new Vector3(0f, data.checkpointHeight, 0f);
                checkpointHealth   = data.checkpointHealth;
                checkpointIndex    = 1; // at least one checkpoint was reached

                // Advance next milestone past the restored checkpoint
                nextMilestone = firstCheckpointHeight;
                while (nextMilestone <= data.checkpointHeight)
                    nextMilestone += checkpointInterval;

                Debug.Log($"[CheckpointManager] Restored checkpoint at {data.checkpointHeight:F0}m.");
            }
        }

        // ── Gizmos ────────────────────────────────────────────────────────────

        private void OnDrawGizmosSelected()
        {
            if (!HasCheckpoint) return;
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(checkpointPosition, 1.5f);
            Gizmos.DrawLine(checkpointPosition, checkpointPosition + Vector3.up * 3f);
        }
    }
}
