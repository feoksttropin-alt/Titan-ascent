using System.Collections;
using UnityEngine;

namespace TitanAscent.Environment
{
    /// <summary>
    /// A wind column trigger volume that gives the player a significant upward boost.
    /// Collider must be set to trigger. Uses sine-wave modulated force to feel organic.
    /// If isVisible, drives a child ParticleSystem scaled to liftForce.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class WindColumn : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────────

        [Header("Lift Settings")]
        [SerializeField] private float liftForce = 25f;

        [Tooltip("Optional sideways drift force applied while inside the column (X/Z world axes).")]
        [SerializeField] private Vector2 horizontalDrift = Vector2.zero;

        [Header("Column Properties")]
        [Tooltip("Logical/visual height of this column in world units.")]
        [SerializeField] private float columnHeight = 20f;

        [Tooltip("If true a child ParticleSystem emits upward particle stream.")]
        [SerializeField] private bool isVisible = true;

        // ── Constants ────────────────────────────────────────────────────────

        // Organic variation: 10% amplitude, 3-second period
        private const float WavePeriod    = 3f;
        private const float WaveAmplitude = 0.10f; // fraction of liftForce

        // ── Runtime state ─────────────────────────────────────────────────────

        private Rigidbody _playerRb;
        private bool _playerInside;
        private ParticleSystem _particles;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            // Ensure the collider is a trigger
            Collider col = GetComponent<Collider>();
            col.isTrigger = true;

            // Find optional child ParticleSystem
            _particles = GetComponentInChildren<ParticleSystem>(includeInactive: true);
        }

        private void Start()
        {
            if (isVisible && _particles != null)
            {
                ConfigureParticles();
                _particles.Play();
            }
            else if (!isVisible && _particles != null)
            {
                _particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        // ── Trigger Callbacks ─────────────────────────────────────────────────

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            Rigidbody rb = other.GetComponent<Rigidbody>();
            if (rb == null) rb = other.GetComponentInParent<Rigidbody>();
            if (rb == null) return;

            _playerRb = rb;
            _playerInside = true;

            // Notify audio layer
            Audio.WindAudioLayer audioLayer = FindFirstObjectByType<Audio.WindAudioLayer>();
            audioLayer?.SetInsideWindColumn(true);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            _playerInside = false;
            _playerRb = null;

            Audio.WindAudioLayer audioLayer = FindFirstObjectByType<Audio.WindAudioLayer>();
            audioLayer?.SetInsideWindColumn(false);
        }

        private void OnTriggerStay(Collider other)
        {
            // Force is applied in FixedUpdate when the rigidbody is cached
        }

        // ── Physics ───────────────────────────────────────────────────────────

        private void FixedUpdate()
        {
            if (!_playerInside || _playerRb == null) return;

            // Organic sine variation: ±10% of liftForce over a 3-second period
            float sineOffset = Mathf.Sin((Time.time / WavePeriod) * Mathf.PI * 2f) * liftForce * WaveAmplitude;
            float currentLift = liftForce + sineOffset;

            // Primary upward force
            _playerRb.AddForce(Vector3.up * currentLift, ForceMode.Force);

            // Optional horizontal drift
            if (horizontalDrift.sqrMagnitude > 0.0001f)
            {
                Vector3 driftForce = new Vector3(horizontalDrift.x, 0f, horizontalDrift.y);
                _playerRb.AddForce(driftForce, ForceMode.Force);
            }
        }

        // ── Particle Configuration ────────────────────────────────────────────

        private void ConfigureParticles()
        {
            if (_particles == null) return;

            // Emission rate scales with liftForce (25f → ~50 particles/s baseline)
            float emissionRate = liftForce * 2f;

            var emission = _particles.emission;
            emission.rateOverTime = emissionRate;

            // Upward velocity in local space
            var velocityOverLifetime = _particles.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.space = ParticleSystemSimulationSpace.Local;
            velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(columnHeight * 0.5f, columnHeight);

            // Match lifetime to column height
            var main = _particles.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(columnHeight * 0.3f, columnHeight * 0.6f);

            var shape = _particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cylinder;
        }

        // ── Gizmos ────────────────────────────────────────────────────────────

        private void OnDrawGizmos()
        {
            DrawColumnGizmo(new Color(0.4f, 0.8f, 1f, 0.3f));
        }

        private void OnDrawGizmosSelected()
        {
            DrawColumnGizmo(new Color(0.4f, 0.8f, 1f, 0.7f));

            // Draw drift arrow if present
            if (horizontalDrift.sqrMagnitude > 0.0001f)
            {
                Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.9f);
                Vector3 drift3 = new Vector3(horizontalDrift.x, 0f, horizontalDrift.y).normalized * 3f;
                Gizmos.DrawRay(transform.position, drift3);
            }
        }

        private void DrawColumnGizmo(Color color)
        {
            Gizmos.color = color;

            // Approximate capsule with top/bottom circles and vertical lines
            float radius = 2.5f;
            Collider col = GetComponent<Collider>();
            if (col is CapsuleCollider cap)
                radius = cap.radius;
            else if (col is SphereCollider sph)
                radius = sph.radius;

            Vector3 bottom = transform.position;
            Vector3 top    = transform.position + Vector3.up * columnHeight;

            // Bottom circle
            DrawCircle(bottom, radius, 16);
            // Top circle
            DrawCircle(top, radius, 16);
            // Vertical lines
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI * 2f / 8f;
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                Gizmos.DrawLine(bottom + offset, top + offset);
            }

            // Upward arrow
            Gizmos.color = new Color(color.r, color.g, color.b, color.a + 0.2f);
            Gizmos.DrawRay(transform.position + Vector3.up * (columnHeight * 0.5f), Vector3.up * 3f);
        }

        private static void DrawCircle(Vector3 center, float radius, int segments)
        {
            for (int i = 0; i < segments; i++)
            {
                float a0 = (float)i       / segments * Mathf.PI * 2f;
                float a1 = (float)(i + 1) / segments * Mathf.PI * 2f;
                Vector3 p0 = center + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius;
                Vector3 p1 = center + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius;
                Gizmos.DrawLine(p0, p1);
            }
        }
    }
}
