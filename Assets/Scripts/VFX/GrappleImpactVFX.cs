using System.Collections;
using UnityEngine;
using TitanAscent.Grapple;
using TitanAscent.Environment;
using TitanAscent.Optimization;

namespace TitanAscent.VFX
{
    /// <summary>
    /// Visual effects for grapple attachment, release, and miss events.
    /// Subscribes to GrappleController events and uses ObjectPooler for all particles.
    /// </summary>
    public class GrappleImpactVFX : MonoBehaviour
    {
        [Header("Impact Particles")]
        [SerializeField] private GameObject impactParticlePrefab;
        [SerializeField] private GameObject crystalFlashPrefab;
        [SerializeField] private GameObject boneDebrisPrefab;
        [SerializeField] private GameObject missPuffPrefab;
        [SerializeField] private GameObject ropeSnapTrailPrefab;

        [Header("Timing")]
        [SerializeField] private float impactParticleLifetime = 0.8f;
        [SerializeField] private float crystalFlashLifetime   = 0.12f;
        [SerializeField] private float boneDebrisLifetime     = 1.0f;
        [SerializeField] private float missPuffLifetime       = 0.5f;
        [SerializeField] private float snapTrailLifetime      = 0.4f;

        [Header("Scaling")]
        [SerializeField] private float minImpactVelocity = 2f;
        [SerializeField] private float maxImpactVelocity = 25f;
        [SerializeField] private int   minParticleCount  = 5;
        [SerializeField] private int   maxParticleCount  = 30;

        // References resolved at runtime
        private GrappleController grappleController;
        private Rigidbody         playerRb;

        // Cached data set just before Attach fires so the handler knows velocity and surface
        private Vector3     pendingAttachPoint;
        private Vector3     pendingImpactVelocity;
        private SurfaceType pendingAttachSurface = SurfaceType.ScaleArmor;

        // Stored so we can compute retraction direction on release
        private Vector3 lastAttachPoint;
        private Vector3 playerFireOrigin;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            grappleController = GetComponent<GrappleController>();
            if (grappleController == null)
                grappleController = GetComponentInParent<GrappleController>();

            playerRb = GetComponentInParent<Rigidbody>();
            if (playerRb == null)
                playerRb = GetComponent<Rigidbody>();
        }

        private void OnEnable()
        {
            if (grappleController != null)
            {
                grappleController.OnGrappleAttached.AddListener(HandleGrappleAttached);
                grappleController.OnGrappleReleased.AddListener(HandleGrappleReleased);
            }
        }

        private void OnDisable()
        {
            if (grappleController != null)
            {
                grappleController.OnGrappleAttached.RemoveListener(HandleGrappleAttached);
                grappleController.OnGrappleReleased.RemoveListener(HandleGrappleReleased);
            }
        }

        private void Update()
        {
            // Track the player's fire-point origin each frame so release knows the direction
            playerFireOrigin = transform.position;

            // Keep pending data fresh so the moment Attach fires we have current velocity
            if (playerRb != null)
                pendingImpactVelocity = playerRb.linearVelocity;

            // If the grapple is attached, record the attach point for the release handler
            if (grappleController != null && grappleController.IsAttached)
                lastAttachPoint = grappleController.AttachPoint;
        }

        // ── Event Handlers ────────────────────────────────────────────────────

        private void HandleGrappleAttached()
        {
            if (grappleController == null) return;

            Vector3 attachPoint = grappleController.AttachPoint;
            float   impactSpeed = pendingImpactVelocity.magnitude;

            // Determine surface type from any SurfaceAnchorPoint at the attach location
            SurfaceType surface = ResolveSurfaceType(attachPoint);

            // Main impact burst
            SpawnImpactBurst(attachPoint, impactSpeed);

            // Surface-specific extras
            switch (surface)
            {
                case SurfaceType.CrystalSurface:
                    SpawnCrystalFlash(attachPoint);
                    break;
                case SurfaceType.BoneRidge:
                    SpawnBoneDebris(attachPoint, impactSpeed);
                    break;
            }

            lastAttachPoint = attachPoint;
        }

        private void HandleGrappleReleased()
        {
            // Compute the rope retraction direction: from attach point toward player origin
            Vector3 snapDirection = (playerFireOrigin - lastAttachPoint).normalized;
            if (snapDirection == Vector3.zero)
                snapDirection = Vector3.up;

            SpawnRopeSnapTrail(lastAttachPoint, snapDirection);
        }

        // ── Miss (called externally by GrappleController or other systems) ────

        /// <summary>
        /// Spawns a miss-indicator puff. Can be called directly when the grapple hits a
        /// non-attachable surface (GrappleController's FireAndMissCoroutine destination point).
        /// </summary>
        public void SpawnMissPuff(Vector3 worldPosition)
        {
            if (missPuffPrefab == null || ObjectPooler.Instance == null) return;

            GameObject puff = ObjectPooler.Instance.Get(missPuffPrefab, worldPosition, Quaternion.identity);

            if (puff != null)
            {
                ParticleSystem ps = puff.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    var main = ps.main;
                    main.startColor = new Color(0.6f, 0.6f, 0.6f, 0.7f);
                    ps.Emit(8);
                }
                ObjectPooler.Instance.ReturnAfter(puff, missPuffLifetime);
            }
        }

        // ── Spawn Helpers ─────────────────────────────────────────────────────

        private void SpawnImpactBurst(Vector3 position, float speed)
        {
            if (impactParticlePrefab == null || ObjectPooler.Instance == null) return;

            int count = ComputeParticleCount(speed);

            // Spread radius scales with velocity
            float spread = Mathf.Lerp(0.1f, 0.6f,
                Mathf.InverseLerp(minImpactVelocity, maxImpactVelocity, speed));

            GameObject burst = ObjectPooler.Instance.Get(
                impactParticlePrefab, position, Quaternion.identity);

            if (burst == null) return;

            ParticleSystem ps = burst.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                var main   = ps.main;
                var shape  = ps.shape;
                var emission = ps.emission;

                main.startSpeedMultiplier  = Mathf.Lerp(1f, 4f,
                    Mathf.InverseLerp(minImpactVelocity, maxImpactVelocity, speed));
                shape.radius               = spread;

                ps.Emit(count);
            }

            ObjectPooler.Instance.ReturnAfter(burst, impactParticleLifetime);
        }

        private void SpawnCrystalFlash(Vector3 position)
        {
            if (crystalFlashPrefab == null || ObjectPooler.Instance == null) return;

            GameObject flash = ObjectPooler.Instance.Get(
                crystalFlashPrefab, position, Quaternion.identity);

            if (flash == null) return;

            // Pulse the Light component if present
            Light flashLight = flash.GetComponent<Light>();
            if (flashLight != null)
                StartCoroutine(PulseLightCoroutine(flashLight, 6f, 0f, 0.05f));

            // Also emit crystal particles
            ParticleSystem ps = flash.GetComponent<ParticleSystem>();
            if (ps != null)
                ps.Emit(15);

            ObjectPooler.Instance.ReturnAfter(flash, crystalFlashLifetime);
        }

        private void SpawnBoneDebris(Vector3 position, float speed)
        {
            if (boneDebrisPrefab == null || ObjectPooler.Instance == null) return;

            GameObject debris = ObjectPooler.Instance.Get(
                boneDebrisPrefab, position, Quaternion.identity);

            if (debris == null) return;

            ParticleSystem ps = debris.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                int count = Mathf.RoundToInt(Mathf.Lerp(3f, 12f,
                    Mathf.InverseLerp(minImpactVelocity, maxImpactVelocity, speed)));
                ps.Emit(count);
            }

            ObjectPooler.Instance.ReturnAfter(debris, boneDebrisLifetime);
        }

        private void SpawnRopeSnapTrail(Vector3 origin, Vector3 direction)
        {
            if (ropeSnapTrailPrefab == null || ObjectPooler.Instance == null) return;

            Quaternion rot = direction != Vector3.zero
                ? Quaternion.LookRotation(direction)
                : Quaternion.identity;

            GameObject trail = ObjectPooler.Instance.Get(ropeSnapTrailPrefab, origin, rot);

            if (trail == null) return;

            ParticleSystem ps = trail.GetComponent<ParticleSystem>();
            if (ps != null)
                ps.Emit(20);

            ObjectPooler.Instance.ReturnAfter(trail, snapTrailLifetime);
        }

        // ── Utilities ─────────────────────────────────────────────────────────

        private int ComputeParticleCount(float speed)
        {
            float t = Mathf.InverseLerp(minImpactVelocity, maxImpactVelocity, speed);
            return Mathf.RoundToInt(Mathf.Lerp(minParticleCount, maxParticleCount, t));
        }

        private SurfaceType ResolveSurfaceType(Vector3 worldPosition)
        {
            // Small overlap sphere at impact point to find a SurfaceAnchorPoint
            Collider[] hits = Physics.OverlapSphere(worldPosition, 0.5f);
            foreach (Collider col in hits)
            {
                SurfaceAnchorPoint anchor = col.GetComponent<SurfaceAnchorPoint>();
                if (anchor != null)
                    return anchor.AnchorSurfaceType;

                SurfaceProperties sp = col.GetComponent<SurfaceProperties>();
                if (sp != null)
                    return sp.Type;
            }
            return SurfaceType.ScaleArmor;
        }

        private IEnumerator PulseLightCoroutine(
            Light light, float peakIntensity, float targetIntensity, float duration)
        {
            if (light == null) yield break;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                light.intensity = Mathf.Lerp(peakIntensity, targetIntensity, elapsed / duration);
                yield return null;
            }
            light.intensity = targetIntensity;
        }
    }
}
