using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TitanAscent.Environment
{
    // -----------------------------------------------------------------------
    // DebrisItem — placed on each loose piece of debris in the world
    // -----------------------------------------------------------------------

    public enum DebrisType
    {
        Rock,
        Bone,
        OldGear,
        WeaponFragment,
        Dust
    }

    public class DebrisItem : MonoBehaviour
    {
        [Header("Debris Properties")]
        public DebrisType debrisType = DebrisType.Rock;
        [Range(0.1f, 50f)] public float mass = 1f;
        public bool isAnchored = false;

        [Header("Fall Recovery")]
        [SerializeField] private float deactivationYThreshold = -50f;
        [SerializeField] private float respawnDelay = 30f;

        private Vector3 _spawnPosition;
        private Quaternion _spawnRotation;
        private Rigidbody _rb;
        private bool _isRespawning;

        internal DebrisSystem OwnerSystem { get; set; }

        private void Awake()
        {
            _rb            = GetComponent<Rigidbody>();
            _spawnPosition = transform.position;
            _spawnRotation = transform.rotation;

            if (_rb != null)
                _rb.mass = mass;
        }

        private void OnEnable()
        {
            _isRespawning = false;
        }

        private void Update()
        {
            if (isAnchored || _isRespawning) return;
            if (transform.position.y < deactivationYThreshold)
                StartCoroutine(DeactivateAndRespawn());
        }

        private IEnumerator DeactivateAndRespawn()
        {
            _isRespawning = true;
            gameObject.SetActive(false);

            yield return new WaitForSeconds(respawnDelay);

            // Respawn at a slightly randomised position in the same zone
            Vector3 randomOffset = new Vector3(
                Random.Range(-3f, 3f),
                0f,
                Random.Range(-3f, 3f));

            transform.position = _spawnPosition + randomOffset;
            transform.rotation = _spawnRotation;

            if (_rb != null)
            {
                _rb.velocity              = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }

            gameObject.SetActive(true);
        }

        /// <summary>Apply external wind force this fixed frame.</summary>
        public void ApplyWindForce(Vector3 force)
        {
            if (isAnchored || _rb == null) return;
            _rb.AddForce(force, ForceMode.Force);
        }
    }

    // -----------------------------------------------------------------------
    // DebrisSystem — manages all DebrisItems in the scene
    // -----------------------------------------------------------------------

    public class DebrisSystem : MonoBehaviour
    {
        [Header("Detection Ranges")]
        [SerializeField] private float activeProcessingRadius = 40f;
        [SerializeField] private float windInfluenceRadius    = 20f;

        [Header("Wind Response")]
        [SerializeField] private float windForceScale = 0.8f;

        [Header("Dust Particles")]
        [SerializeField] private ParticleSystem dustParticleSystem;
        [SerializeField] private float maxDustEmissionRate = 80f;

        [Header("Batch Update")]
        [SerializeField] private float distantUpdateInterval = 2f;

        // ------------------------------------------------------------------
        // Statics
        // ------------------------------------------------------------------

        private static readonly List<DebrisItem> AllDebrisItems = new List<DebrisItem>();

        /// <summary>
        /// Returns all DebrisItems within <paramref name="radius"/> of <paramref name="position"/>.
        /// </summary>
        public static List<DebrisItem> GetNearbyDebris(Vector3 position, float radius)
        {
            var result = new List<DebrisItem>();
            float sqrRadius = radius * radius;
            foreach (DebrisItem item in AllDebrisItems)
            {
                if (item == null || !item.gameObject.activeInHierarchy) continue;
                if ((item.transform.position - position).sqrMagnitude <= sqrRadius)
                    result.Add(item);
            }
            return result;
        }

        // ------------------------------------------------------------------
        // Private state
        // ------------------------------------------------------------------

        private WindSystem _windSystem;
        private Player.PlayerController _player;
        private DebrisItem[] _allItems;
        private float _distantTimer;

        // ------------------------------------------------------------------
        // Lifecycle
        // ------------------------------------------------------------------

        private void Awake()
        {
            _windSystem = FindFirstObjectByType<WindSystem>();
            _player     = FindFirstObjectByType<Player.PlayerController>();

            // Collect all DebrisItems in the scene (including children)
            _allItems = FindObjectsByType<DebrisItem>(FindObjectsSortMode.None);
            foreach (DebrisItem item in _allItems)
            {
                item.OwnerSystem = this;
                if (!AllDebrisItems.Contains(item))
                    AllDebrisItems.Add(item);
            }
        }

        private void OnDestroy()
        {
            foreach (DebrisItem item in _allItems)
            {
                if (item != null)
                    AllDebrisItems.Remove(item);
            }
        }

        private void Update()
        {
            if (_player == null)
            {
                _player = FindFirstObjectByType<Player.PlayerController>();
                return;
            }

            UpdateDustParticles();
        }

        private void FixedUpdate()
        {
            if (_player == null) return;

            Vector3 playerPos     = _player.transform.position;
            float   altitude      = _player.CurrentHeight;
            float   windStrength  = _windSystem != null
                ? _windSystem.GetWindStrengthAtAltitude(altitude)
                : 0f;

            Vector3 windDir = _windSystem != null
                ? Vector3.right   // simplified; full impl would expose wind direction
                : Vector3.zero;

            float sqrActive = activeProcessingRadius * activeProcessingRadius;
            float sqrWind   = windInfluenceRadius    * windInfluenceRadius;

            foreach (DebrisItem item in _allItems)
            {
                if (item == null || !item.gameObject.activeInHierarchy || item.isAnchored) continue;

                float sqrDist = (item.transform.position - playerPos).sqrMagnitude;
                if (sqrDist > sqrActive) continue; // handled by batch coroutine

                if (sqrDist <= sqrWind || windStrength > 10f)
                {
                    Vector3 force = windDir * (windStrength * windForceScale / Mathf.Max(item.mass, 0.1f));
                    item.ApplyWindForce(force);
                }
            }

            // Distant batch update timer
            _distantTimer += Time.fixedDeltaTime;
            if (_distantTimer >= distantUpdateInterval)
            {
                _distantTimer = 0f;
                StartCoroutine(BatchUpdateDistant(playerPos, windDir, windStrength));
            }
        }

        // ------------------------------------------------------------------
        // Batch update for distant debris
        // ------------------------------------------------------------------

        private IEnumerator BatchUpdateDistant(Vector3 playerPos, Vector3 windDir, float windStrength)
        {
            float sqrActive = activeProcessingRadius * activeProcessingRadius;

            foreach (DebrisItem item in _allItems)
            {
                if (item == null || !item.gameObject.activeInHierarchy || item.isAnchored) continue;
                if ((item.transform.position - playerPos).sqrMagnitude <= sqrActive) continue;

                // Very light nudge for distant items — purely cosmetic drift
                if (windStrength > 5f)
                {
                    Vector3 nudge = windDir * (windStrength * 0.05f / Mathf.Max(item.mass, 0.1f));
                    item.ApplyWindForce(nudge * distantUpdateInterval); // scaled so it matches per-frame intent
                }

                yield return null; // spread over frames to avoid spike
            }
        }

        // ------------------------------------------------------------------
        // Dust particle emission driven by nearby wind
        // ------------------------------------------------------------------

        private void UpdateDustParticles()
        {
            if (dustParticleSystem == null || _player == null) return;

            float altitude     = _player.CurrentHeight;
            float windStrength = _windSystem != null
                ? _windSystem.GetWindStrengthAtAltitude(altitude)
                : 0f;

            float emissionRate = Mathf.Lerp(0f, maxDustEmissionRate, Mathf.InverseLerp(0f, 30f, windStrength));

            var emission = dustParticleSystem.emission;
            emission.rateOverTime = emissionRate;

            if (emissionRate > 0f && !dustParticleSystem.isPlaying)
                dustParticleSystem.Play();
            else if (emissionRate <= 0f && dustParticleSystem.isPlaying)
                dustParticleSystem.Stop();
        }
    }
}
