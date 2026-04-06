using System.Collections;
using UnityEngine;
using TMPro;

namespace TitanAscent.Grapple
{
    /// <summary>
    /// Renders a dotted predictive arc showing the swing trajectory the player
    /// would follow if the grapple attached at the current aim point.
    ///
    /// Arc is visible only while the player is aiming (grapple not attached,
    /// holding aim input) at a valid surface within range.  Fades in over 0.3 s
    /// when a valid target is found and fades out over 0.2 s when lost.
    /// </summary>
    [RequireComponent(typeof(GrappleController))]
    public class GrapplePredictor : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────────

        [Header("Rendering")]
        [SerializeField] private LineRenderer arcLineRenderer;
        [SerializeField] private TextMeshPro  apexLabel;

        [Header("Colors")]
        [SerializeField] private Color validColor   = new Color(0.2f, 1f,   0.2f, 1f); // upward
        [SerializeField] private Color neutralColor = new Color(1f,   0.9f, 0.1f, 1f); // sideways
        [SerializeField] private Color badColor     = new Color(1f,   0.2f, 0.2f, 1f); // downward

        [Header("Simulation")]
        [SerializeField] private LayerMask aimLayerMask  = ~0;
        [SerializeField] private float     maxRange      = 50f;
        [SerializeField] private float     simDuration   = 2f;
        [SerializeField] private float     simStep       = 0.05f;

        [Header("Fade")]
        [SerializeField] private float fadeInDuration  = 0.3f;
        [SerializeField] private float fadeOutDuration = 0.2f;

        // ── Private state ─────────────────────────────────────────────────────────

        private const int ArcPoints = 40;

        private GrappleController grappleController;
        private Rigidbody         playerRb;
        private Transform         firePoint;

        private Vector3[] arcBuffer = new Vector3[ArcPoints];
        private float     currentAlpha  = 0f;
        private float     targetAlpha   = 0f;
        private float     fadeTimer     = 0f;
        private bool      hasValidTarget = false;

        // Cached per-frame results
        private Vector3 cachedAttachPoint;
        private Color   cachedArcColor;
        private float   cachedApexY;
        private int     cachedApexIndex;

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            grappleController = GetComponent<GrappleController>();
            playerRb          = GetComponentInParent<Rigidbody>();
            if (playerRb == null)
                playerRb = GetComponent<Rigidbody>();

            // Locate the fire point via the GrappleController field (reflection-free: use child search)
            Transform fp = transform.Find("FirePoint");
            firePoint = fp != null ? fp : transform;

            if (arcLineRenderer == null)
            {
                arcLineRenderer = gameObject.AddComponent<LineRenderer>();
                arcLineRenderer.positionCount = ArcPoints;
                arcLineRenderer.startWidth    = 0.04f;
                arcLineRenderer.endWidth      = 0.04f;
                arcLineRenderer.useWorldSpace = true;
            }

            arcLineRenderer.positionCount = ArcPoints;
            arcLineRenderer.enabled       = false;

            if (apexLabel != null)
                apexLabel.enabled = false;
        }

        private void Update()
        {
            bool shouldShow = EvaluateArc();

            // Decide fade direction
            targetAlpha = shouldShow ? 1f : 0f;

            float speed = shouldShow
                ? 1f / Mathf.Max(0.001f, fadeInDuration)
                : 1f / Mathf.Max(0.001f, fadeOutDuration);

            currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, speed * Time.deltaTime);

            bool visible = currentAlpha > 0.001f;
            arcLineRenderer.enabled = visible;

            if (visible)
                RenderArc(currentAlpha);
            else if (apexLabel != null)
                apexLabel.enabled = false;
        }

        // ── Arc evaluation ────────────────────────────────────────────────────────

        /// <summary>
        /// Raycasts toward aim direction. If a valid surface is found, simulates
        /// pendulum swing and stores results in the arc buffer.
        /// Returns true if a valid arc was computed.
        /// </summary>
        private bool EvaluateArc()
        {
            // Only show while grapple is idle (aiming phase)
            if (grappleController.IsAttached || grappleController.CurrentState != GrappleState.Idle)
                return false;

            Vector3 origin   = firePoint.position;
            Vector3 aimDir   = GetAimDirection();

            if (!Physics.Raycast(origin, aimDir, out RaycastHit hit, maxRange, aimLayerMask))
                return false;

            cachedAttachPoint = hit.point;

            // Simulate pendulum swing
            SimulatePendulum(cachedAttachPoint);

            return true;
        }

        private void SimulatePendulum(Vector3 anchor)
        {
            // Initial conditions: player's current position and velocity
            Vector3 pos = playerRb != null ? playerRb.position : transform.position;
            Vector3 vel = playerRb != null ? playerRb.velocity : Vector3.zero;

            float ropeLength = Vector3.Distance(pos, anchor);
            ropeLength = Mathf.Max(ropeLength, 0.5f);

            float apexY      = pos.y;
            int   apexIndex  = 0;
            float startY     = pos.y;

            int totalSteps = Mathf.RoundToInt(simDuration / simStep);
            // We'll sample ArcPoints evenly spaced across totalSteps
            float stepRatio = (float)totalSteps / ArcPoints;

            Vector3 simPos = pos;
            Vector3 simVel = vel;
            const float gravity = -9.81f;

            for (int arcIdx = 0; arcIdx < ArcPoints; arcIdx++)
            {
                // Store current position for this arc point
                arcBuffer[arcIdx] = simPos;

                if (simPos.y > apexY)
                {
                    apexY     = simPos.y;
                    apexIndex = arcIdx;
                }

                // Advance simulation by one "arc step" (multiple physics sub-steps)
                int subSteps = Mathf.Max(1, Mathf.RoundToInt(stepRatio));
                for (int s = 0; s < subSteps; s++)
                {
                    // Gravity
                    simVel.y += gravity * simStep;

                    simPos += simVel * simStep;

                    // Rope constraint: project back onto sphere of radius ropeLength around anchor
                    Vector3 offset = simPos - anchor;
                    float   dist   = offset.magnitude;
                    if (dist > ropeLength)
                    {
                        Vector3 dir      = offset / dist;
                        simPos           = anchor + dir * ropeLength;

                        // Remove velocity component along the rope (tension)
                        Vector3 radial   = Vector3.Project(simVel, dir);
                        if (Vector3.Dot(radial, dir) > 0f)
                            simVel -= radial;
                    }
                }
            }

            // Classify arc direction
            float endY      = arcBuffer[ArcPoints - 1].y;
            float heightDelta = endY - startY;

            if (heightDelta > 1f)
                cachedArcColor = validColor;
            else if (heightDelta < -1f)
                cachedArcColor = badColor;
            else
                cachedArcColor = neutralColor;

            cachedApexY     = apexY;
            cachedApexIndex = apexIndex;
        }

        // ── Rendering ─────────────────────────────────────────────────────────────

        private void RenderArc(float alpha)
        {
            arcLineRenderer.positionCount = ArcPoints;
            arcLineRenderer.SetPositions(arcBuffer);

            // Alternate alpha for dotted appearance
            // Unity LineRenderer does not natively support per-point alpha, but we can
            // simulate a dotted look using a gradient with alternating alpha bands.
            // Here we set the gradient to two-tone with the color tinted by alpha.
            Color highAlpha = new Color(cachedArcColor.r, cachedArcColor.g, cachedArcColor.b, cachedArcColor.a * alpha * 0.8f);
            Color lowAlpha  = new Color(cachedArcColor.r, cachedArcColor.g, cachedArcColor.b, cachedArcColor.a * alpha * 0.2f);

            // Build a repeating gradient with 8 bands to create dotted effect
            var gradient = new Gradient();
            var colorKeys = new GradientColorKey[]
            {
                new GradientColorKey(cachedArcColor, 0f),
                new GradientColorKey(cachedArcColor, 1f)
            };
            var alphaKeys = new GradientAlphaKey[16];
            for (int i = 0; i < 8; i++)
            {
                float t0 = (float)(i * 2)     / 16f;
                float t1 = (float)(i * 2 + 1) / 16f;
                alphaKeys[i * 2]     = new GradientAlphaKey(highAlpha.a, t0);
                alphaKeys[i * 2 + 1] = new GradientAlphaKey(lowAlpha.a,  t1);
            }
            gradient.SetKeys(colorKeys, alphaKeys);
            arcLineRenderer.colorGradient = gradient;

            // Apex label
            if (apexLabel != null)
            {
                float heightGain = cachedApexY - (playerRb != null ? playerRb.position.y : transform.position.y);
                apexLabel.enabled  = alpha > 0.3f;
                apexLabel.text     = $"+{heightGain:F1}m";
                apexLabel.color    = new Color(cachedArcColor.r, cachedArcColor.g, cachedArcColor.b, alpha);
                apexLabel.transform.position = arcBuffer[Mathf.Clamp(cachedApexIndex, 0, ArcPoints - 1)] + Vector3.up * 0.4f;
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private Vector3 GetAimDirection()
        {
            Camera cam = Camera.main;
            if (cam != null) return cam.transform.forward;
            return transform.forward;
        }

        private void OnDisable()
        {
            if (arcLineRenderer != null) arcLineRenderer.enabled = false;
            if (apexLabel != null)       apexLabel.enabled       = false;
            currentAlpha = 0f;
        }
    }
}
