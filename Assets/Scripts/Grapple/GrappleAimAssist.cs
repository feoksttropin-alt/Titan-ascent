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
        [SerializeField] private float highlightUpdateRate = 0.05f;

        [Header("Layers")]
        [SerializeField] private LayerMask anchorLayerMask = ~0;

        private Camera mainCamera;
        private SurfaceAnchorPoint currentBestTarget;
        private SurfaceAnchorPoint lastHighlightedTarget;
        private List<SurfaceAnchorPoint> candidateTargets = new List<SurfaceAnchorPoint>();
        private float lastHighlightUpdateTime;

        public bool HasTarget => currentBestTarget != null;
        public Vector3 BestTarget => currentBestTarget != null ? currentBestTarget.transform.position : Vector3.zero;
        public SurfaceAnchorPoint BestAnchor => currentBestTarget;

        private void Awake()
        {
            mainCamera = Camera.main;
        }

        private void Update()
        {
            if (mainCamera == null)
                mainCamera = Camera.main;

            FindBestTarget();

            if (Time.time - lastHighlightUpdateTime > highlightUpdateRate)
            {
                UpdateHighlight();
                lastHighlightUpdateTime = Time.time;
            }
        }

        private void FindBestTarget()
        {
            Vector3 aimOrigin = transform.position;
            Vector3 aimDirection = mainCamera != null ? mainCamera.transform.forward : transform.forward;

            candidateTargets.Clear();

            // Find all anchor points in range
            Collider[] nearbyColliders = Physics.OverlapSphere(aimOrigin, detectionRadius, anchorLayerMask);
            foreach (Collider col in nearbyColliders)
            {
                SurfaceAnchorPoint anchor = col.GetComponent<SurfaceAnchorPoint>();
                if (anchor == null) continue;
                if (!anchor.IsGrappleable) continue;

                Vector3 toAnchor = (anchor.transform.position - aimOrigin).normalized;
                float angle = Vector3.Angle(aimDirection, toAnchor);

                if (angle <= coneAngle)
                {
                    // Check line of sight
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
                float angle = Vector3.Angle(aimDirection, toAnchor.normalized);
                float distance = toAnchor.magnitude;

                // Weight angle heavily over distance for aim assist feel
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
            // Unhighlight the previous target
            if (lastHighlightedTarget != null && lastHighlightedTarget != currentBestTarget)
            {
                lastHighlightedTarget.SetHighlighted(false);
            }

            // Highlight the new best target
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
