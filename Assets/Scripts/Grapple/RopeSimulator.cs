using UnityEngine;
using System.Collections.Generic;

namespace TitanAscent.Grapple
{
    [RequireComponent(typeof(LineRenderer))]
    public class RopeSimulator : MonoBehaviour
    {
        [Header("Rope Configuration")]
        [SerializeField] private int segmentCount = 20;
        [SerializeField] private float stiffness = 0.8f;
        [SerializeField] private float damping = 0.05f;
        [SerializeField] private int constraintIterations = 5;
        [SerializeField] private float gravity = -9.81f;
        [SerializeField] private float ropeRadius = 0.02f;

        [Header("Tension")]
        [SerializeField] private float tensionThreshold = 0.8f;
        [SerializeField] private float maxTensionForce = 200f;

        private LineRenderer lineRenderer;
        private Vector3[] segmentPositions;
        private Vector3[] segmentOldPositions;
        private float[] segmentLengths;

        private Vector3 anchorPoint;
        private Vector3 playerAttachPoint;
        private float totalLength = 10f;
        private float segmentLength;

        private bool isAttached = false;
        private Rigidbody playerRigidbody;

        private float currentTension = 0f;
        private float lastKnownLength;

        public bool IsAttached => isAttached;
        public float CurrentTension => currentTension;

        private void Awake()
        {
            lineRenderer = GetComponent<LineRenderer>();
            lineRenderer.startWidth = ropeRadius * 2f;
            lineRenderer.endWidth = ropeRadius;
            lineRenderer.useWorldSpace = true;
            lineRenderer.positionCount = segmentCount;

            playerRigidbody = GetComponentInParent<Rigidbody>();
            if (playerRigidbody == null)
                playerRigidbody = FindFirstObjectByType<Player.PlayerController>()?.GetComponent<Rigidbody>();

            InitializeSegments();
        }

        private void InitializeSegments()
        {
            segmentPositions = new Vector3[segmentCount];
            segmentOldPositions = new Vector3[segmentCount];
            segmentLengths = new float[segmentCount - 1];

            segmentLength = totalLength / Mathf.Max(1, segmentCount - 1);

            for (int i = 0; i < segmentCount; i++)
            {
                segmentPositions[i] = transform.position + Vector3.down * i * segmentLength;
                segmentOldPositions[i] = segmentPositions[i];
            }

            for (int i = 0; i < segmentLengths.Length; i++)
                segmentLengths[i] = segmentLength;
        }

        private void FixedUpdate()
        {
            if (!isAttached) return;

            // Update player end
            Vector3 playerPos = playerRigidbody != null ? playerRigidbody.position : transform.position;
            playerAttachPoint = playerPos;

            SimulateVerlet();
            ApplyConstraints();
            ApplyTensionForce();
            UpdateLineRenderer();
        }

        private void SimulateVerlet()
        {
            float dt = Time.fixedDeltaTime;
            Vector3 gravityForce = new Vector3(0f, gravity, 0f);

            for (int i = 1; i < segmentCount - 1; i++) // Skip anchor (0) and player end (last)
            {
                Vector3 pos = segmentPositions[i];
                Vector3 oldPos = segmentOldPositions[i];

                // Verlet integration: pos = 2*pos - oldPos + accel * dt^2
                Vector3 velocity = (pos - oldPos) * (1f - damping);
                Vector3 newPos = pos + velocity + gravityForce * dt * dt;

                segmentOldPositions[i] = pos;
                segmentPositions[i] = newPos;
            }

            // Pin anchor segment to attachment point
            segmentPositions[0] = anchorPoint;
            segmentOldPositions[0] = anchorPoint;

            // Pin player segment to player position
            segmentPositions[segmentCount - 1] = playerAttachPoint;
            segmentOldPositions[segmentCount - 1] = playerAttachPoint;
        }

        private void ApplyConstraints()
        {
            for (int iteration = 0; iteration < constraintIterations; iteration++)
            {
                // Constraint: adjacent segments must maintain their rest length
                for (int i = 0; i < segmentCount - 1; i++)
                {
                    Vector3 p1 = segmentPositions[i];
                    Vector3 p2 = segmentPositions[i + 1];

                    float restLength = segmentLengths[i];
                    float currentDist = Vector3.Distance(p1, p2);

                    if (currentDist < 0.0001f) continue;

                    float error = (currentDist - restLength) / currentDist;
                    Vector3 correction = (p2 - p1) * (error * 0.5f * stiffness);

                    // Anchor is pinned (i == 0) and player end is pinned (i == segmentCount - 1)
                    if (i > 0)
                        segmentPositions[i] += correction;
                    if (i < segmentCount - 2)
                        segmentPositions[i + 1] -= correction;
                }

                // Re-pin fixed points after constraint pass
                segmentPositions[0] = anchorPoint;
                segmentPositions[segmentCount - 1] = playerAttachPoint;
            }
        }

        private void ApplyTensionForce()
        {
            if (playerRigidbody == null) return;

            // Calculate how stretched the rope is
            float totalCurrentLength = 0f;
            for (int i = 0; i < segmentCount - 1; i++)
                totalCurrentLength += Vector3.Distance(segmentPositions[i], segmentPositions[i + 1]);

            float stretch = totalCurrentLength / Mathf.Max(0.01f, totalLength);
            currentTension = Mathf.Clamp01((stretch - 1f) / 0.2f); // Tension starts building at 20% stretch

            if (currentTension > tensionThreshold)
            {
                // Rope is taut — apply constraint force pulling player toward anchor
                Vector3 toAnchor = anchorPoint - playerAttachPoint;
                float distanceToAnchor = toAnchor.magnitude;

                if (distanceToAnchor > totalLength)
                {
                    float excess = distanceToAnchor - totalLength;
                    float forceMagnitude = Mathf.Min(excess * maxTensionForce, maxTensionForce);
                    playerRigidbody.AddForce(toAnchor.normalized * forceMagnitude, ForceMode.Force);
                }
            }
        }

        private void UpdateLineRenderer()
        {
            lineRenderer.positionCount = segmentCount;
            lineRenderer.SetPositions(segmentPositions);

            // Tint rope based on tension
            Color baseColor = Color.white;
            Color tensionColor = Color.red;
            Color ropeColor = Color.Lerp(baseColor, tensionColor, currentTension);
            lineRenderer.startColor = ropeColor;
            lineRenderer.endColor = ropeColor;
        }

        public void SetAnchorPoint(Vector3 point)
        {
            anchorPoint = point;
            isAttached = true;

            // Re-distribute segments from anchor to player position
            Vector3 playerPos = playerRigidbody != null ? playerRigidbody.position : transform.position;
            for (int i = 0; i < segmentCount; i++)
            {
                float t = (float)i / (segmentCount - 1);
                segmentPositions[i] = Vector3.Lerp(anchorPoint, playerPos, t);
                segmentOldPositions[i] = segmentPositions[i];
            }

            lineRenderer.enabled = true;
        }

        public void SetLength(float length)
        {
            totalLength = Mathf.Max(0.1f, length);
            segmentLength = totalLength / Mathf.Max(1, segmentCount - 1);

            for (int i = 0; i < segmentLengths.Length; i++)
                segmentLengths[i] = segmentLength;
        }

        public float GetTension()
        {
            return currentTension;
        }

        public void Detach()
        {
            isAttached = false;
            currentTension = 0f;
            lineRenderer.enabled = false;
        }

        private void OnDrawGizmosSelected()
        {
            if (segmentPositions == null) return;
            Gizmos.color = Color.yellow;
            for (int i = 0; i < segmentPositions.Length - 1; i++)
                Gizmos.DrawLine(segmentPositions[i], segmentPositions[i + 1]);
        }
    }
}
