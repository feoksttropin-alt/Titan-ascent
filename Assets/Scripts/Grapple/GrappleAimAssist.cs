using UnityEngine;
using System.Collections.Generic;

namespace TitanAscent.Grapple
{
    public class GrappleAimAssist : MonoBehaviour
    {
        [Header("Aim Assist Settings")]
        [SerializeField] private float detectionRadius = 25f;
        [SerializeField] private float coneAngle = 30f;
        [SerializeField] private float snapStrength = 0.3f;
        [SerializeField] private float updateInterval = 0.05f;  // renamed from highlightUpdateRate; governs both scan and highlight

        [Header("Layers")]
        [SerializeField] private LayerMask anchorLayerMask = ~0;

        // Reusable buffer — avoids per-frame allocation from Physics.OverlapSphere
        private static readonly Collider[] _overlapBuffer = new Collider[64];

        private Camera mainCamera;
        private SurfaceAnchorPoint currentBestTarget;
        private SurfaceAnchorPoint lastHighlightedTarget;
        private readonly List<SurfaceAnchorPoint> candidateTargets = new List<SurfaceAnchorPoint>();
        private float lastUpdateTime;

        public bool HasTarget => currentBestTarget != null;
        public Vector3 BestTarget => currentBestTarget != null ? currentBestTarget.transform.position : Vector3.zero;
        public SurfaceAnchorPoint BestAnchor => currentBestTarget;

        /// <summary>Exposed for temporary frustration-detection forgiveness adjustments.</summary>
        public float DetectionRadius
        {
            get => detectionRadius;
            set => detectionRadius = value;
        }

        private void Awake()
        {
            mainCamera = Camera.main;
        }

        private void Update()
        {
            if (mainCamera == null)
                mainCamera = Camera.main;

            // Throttle the expensive OverlapSphere scan and highlight update together
            if (Time.time - lastUpdateTime >= updateInterval)
            {
                FindBestTarget();
                UpdateHighlight();
                lastUpdateTime = Time.time;
            }
        }

        private void FindBestTarget()
        {
            Vector3 aimOrigin    = transform.position;
            Vector3 aimDirection = mainCamera != null ? mainCamera.transform.forward : transform.forward;

            candidateTargets.Clear();

            // Non-allocating overlap using static buffer
            int count = Physics.OverlapSphereNonAlloc(aimOrigin, detectionRadius, _overlapBuffer, anchorLayerMask);
            for (int i = 0; i < count; i++)
            {
                SurfaceAnchorPoint anchor = _overlapBuffer[i].GetComponent<SurfaceAnchorPoint>();
                if (anchor == null || !anchor.IsGrappleable) continue;

                Vector3 toAnchor = (anchor.transform.position - aimOrigin).normalized;
                float angle = Vector3.Angle(aimDirection, toAnchor);

                if (angle <= coneAngle)
                {
                    // Line-of-sight check
                    if (!Physics.Linecast(aimOrigin, anchor.transform.position, out RaycastHit hit, anchorLayerMask)
                        || hit.collider.gameObject == anchor.gameObject)
                    {
                        candidateTargets.Add(anchor);
                    }
                }
            }

            if (candidateTargets.Count == 0)
            {
                currentBestTarget = null;
                return;
            }

            // Score targets: lower angle = higher score; closer = higher score
            SurfaceAnchorPoint best = null;
            float bestScore = float.MaxValue;

            foreach (SurfaceAnchorPoint anchor in candidateTargets)
            {
                Vector3 toAnchor = anchor.transform.position - aimOrigin;
                float angle    = Vector3.Angle(aimDirection, toAnchor.normalized);
                float distance = toAnchor.magnitude;

                // Weight angle heavily over distance for aim-assist feel
                float score = angle * 2f + distance * 0.1f;

                if (score < bestScore)
                {
                    bestScore = score;
                    best = anchor;
                }
            }

            currentBestTarget = best;
        }

        private void UpdateHighlight()
        {
            if (lastHighlightedTarget != null && lastHighlightedTarget != currentBestTarget)
                lastHighlightedTarget.SetHighlighted(false);

            if (currentBestTarget != null)
            {
                currentBestTarget.SetHighlighted(true);
                lastHighlightedTarget = currentBestTarget;
            }
        }

        /// <summary>
        /// Returns an aim direction slightly snapped toward the best target.
        /// Pass in the raw aim direction; get back the assisted direction.
        /// </summary>
        public Vector3 GetAssistedAimDirection(Vector3 rawDirection)
        {
            if (currentBestTarget == null) return rawDirection;

            Vector3 toTarget = (currentBestTarget.transform.position - transform.position).normalized;
            return Vector3.Slerp(rawDirection, toTarget, snapStrength).normalized;
        }

        /// <summary>Sets the aim assist cone angle at runtime. Used by MovementTuner.</summary>
        public void SetConeAngle(float degrees) => coneAngle = Mathf.Clamp(degrees, 0f, 90f);

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.15f);
            Gizmos.DrawWireSphere(transform.position, detectionRadius);

            if (currentBestTarget != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, currentBestTarget.transform.position);
                Gizmos.DrawWireSphere(currentBestTarget.transform.position, 0.5f);
            }
        }
    }
}
