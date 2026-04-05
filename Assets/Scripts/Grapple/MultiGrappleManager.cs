using UnityEngine;
using UnityEngine.Events;
using System.Collections;

namespace TitanAscent.Grapple
{
    /// <summary>
    /// Manages the dual-grapple advanced mechanic (unlocked after first summit or via
    /// challenge mode).  The player retains one standard swinging grapple (slot 0,
    /// managed by GrappleController) and gains one "anchor" grapple (slot 1) that
    /// stays planted at the surface while the player swings on the main rope.
    ///
    /// The V-shape setup constrains the player's maximum distance from the anchor
    /// point each FixedUpdate via <see cref="UpdateDualConstraints"/>.
    ///
    /// The second rope is rendered through a dedicated LineRenderer in a gold/orange
    /// colour so players can visually distinguish the two ropes.
    ///
    /// Input: Middle Mouse Button or Left Shoulder (Button4) fires/releases slot 1.
    /// Double-tap release (Q twice within 0.3 s) drops both grapples simultaneously.
    /// </summary>
    public class MultiGrappleManager : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────────

        [Header("Feature Toggle")]
        [SerializeField] private bool isEnabled = false;

        [Header("Secondary Grapple Physics")]
        [SerializeField] private float secondaryMaxRopeLength  = 50f;
        [SerializeField] private float secondaryMinRopeLength  = 2f;
        [SerializeField] private float secondaryConstraintForce = 600f;
        [SerializeField] private float projectileSpeed          = 80f;
        [SerializeField] private LayerMask grappleLayerMask    = ~0;

        [Header("Visuals")]
        [SerializeField] private LineRenderer secondaryRopeRenderer;
        [SerializeField] private Color        secondaryRopeColor = new Color(1f, 0.65f, 0f, 1f); // gold/orange

        [Header("Input")]
        [SerializeField] private KeyCode secondaryFireKey       = KeyCode.Mouse2;
        [SerializeField] private KeyCode secondaryReleaseKey    = KeyCode.Q;
        [SerializeField] private float   doubleTapWindow        = 0.3f;

        [Header("Events")]
        public UnityEvent OnSecondaryAttached;
        public UnityEvent OnSecondaryReleased;

        // ── Private state ─────────────────────────────────────────────────────────

        private GrappleController primaryGrapple;
        private Rigidbody          rb;
        private Transform          firePoint;

        private bool    secondaryAttached   = false;
        private Vector3 secondaryAnchorPoint;
        private float   secondaryRopeLength;

        // Flying head simulation for secondary
        private bool    secondaryInFlight   = false;
        private Vector3 secondaryHeadPos;

        // Double-tap release tracking
        private float lastReleaseTapTime    = -1f;

        // ── Properties ────────────────────────────────────────────────────────────

        public bool IsEnabled         => isEnabled;
        public bool IsSecondaryActive => secondaryAttached || secondaryInFlight;

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            primaryGrapple = GetComponent<GrappleController>();
            rb             = GetComponentInParent<Rigidbody>();
            if (rb == null) rb = GetComponent<Rigidbody>();

            Transform fp = transform.Find("FirePoint");
            firePoint = fp != null ? fp : transform;

            // Auto-configure secondary rope renderer if not assigned
            if (secondaryRopeRenderer == null)
            {
                GameObject ropeObj = new GameObject("SecondaryRopeRenderer");
                ropeObj.transform.SetParent(transform, false);
                secondaryRopeRenderer              = ropeObj.AddComponent<LineRenderer>();
                secondaryRopeRenderer.positionCount = 2;
                secondaryRopeRenderer.startWidth    = 0.03f;
                secondaryRopeRenderer.endWidth      = 0.02f;
                secondaryRopeRenderer.useWorldSpace = true;
            }

            secondaryRopeRenderer.startColor = secondaryRopeColor;
            secondaryRopeRenderer.endColor   = secondaryRopeColor;
            secondaryRopeRenderer.enabled    = false;
        }

        private void Update()
        {
            if (!isEnabled) return;

            HandleInput();
            UpdateSecondaryHeadFlight();
            UpdateSecondaryRopeVisual();
        }

        private void FixedUpdate()
        {
            if (!isEnabled) return;
            if (secondaryAttached)
                UpdateDualConstraints();
        }

        // ── Input ─────────────────────────────────────────────────────────────────

        private void HandleInput()
        {
            // Fire / release secondary grapple
            if (Input.GetKeyDown(secondaryFireKey))
            {
                if (secondaryAttached || secondaryInFlight)
                    ReleaseSecondaryGrapple();
                else
                    FireSecondaryGrapple();
            }

            // Double-tap Q to release both grapples at once
            if (Input.GetKeyDown(secondaryReleaseKey))
            {
                float now = Time.time;
                if (now - lastReleaseTapTime <= doubleTapWindow)
                {
                    // Double-tap detected — release everything
                    ReleaseSecondaryGrapple();
                    if (primaryGrapple != null)
                        primaryGrapple.ReleaseGrapple();
                }
                lastReleaseTapTime = now;
            }

            // Also support Left Shoulder (Joystick Button4)
            if (Input.GetKeyDown(KeyCode.JoystickButton4))
            {
                if (secondaryAttached || secondaryInFlight)
                    ReleaseSecondaryGrapple();
                else
                    FireSecondaryGrapple();
            }
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Fires the anchor grapple toward the current aim direction.
        /// If it hits a valid surface, attaches as a non-swinging anchor.
        /// </summary>
        public void FireSecondaryGrapple()
        {
            if (!isEnabled) return;

            Vector3 origin = firePoint.position;
            Vector3 aimDir = GetAimDirection();

            if (Physics.Raycast(origin, aimDir, out RaycastHit hit, secondaryMaxRopeLength, grappleLayerMask))
            {
                float dist = Vector3.Distance(origin, hit.point);

                if (dist < 15f)
                {
                    // Attach immediately for short range
                    AttachSecondary(hit.point);
                }
                else
                {
                    // Launch with travel time
                    StartCoroutine(FlyToSecondaryTarget(hit.point));
                }
            }
        }

        /// <summary>Releases the anchor grapple.</summary>
        public void ReleaseSecondaryGrapple()
        {
            if (!IsSecondaryActive) return;

            secondaryAttached  = false;
            secondaryInFlight  = false;

            if (secondaryRopeRenderer != null)
                secondaryRopeRenderer.enabled = false;

            OnSecondaryReleased?.Invoke();
        }

        /// <summary>
        /// Applies constraint forces from both the primary and secondary anchor
        /// each FixedUpdate to simulate the dual-rope physics.
        /// Called automatically from FixedUpdate when secondary is active.
        /// </summary>
        public void UpdateDualConstraints()
        {
            if (rb == null || !secondaryAttached) return;

            Vector3 toSecondary  = secondaryAnchorPoint - rb.position;
            float   distToSecondary = toSecondary.magnitude;

            // Constrain: if player exceeds secondary rope length, apply inward force
            if (distToSecondary > secondaryRopeLength)
            {
                float   overshoot   = distToSecondary - secondaryRopeLength;
                float   forceMag    = secondaryConstraintForce * (overshoot / secondaryRopeLength);
                rb.AddForce(toSecondary.normalized * forceMag, ForceMode.Force);
            }

            // Additionally, if primary grapple is attached, blend the constraint so
            // the player is held within the intersection of both rope spheres.
            if (primaryGrapple != null && primaryGrapple.IsAttached)
            {
                Vector3 toPrimary     = primaryGrapple.AttachPoint - rb.position;
                float   distToPrimary = toPrimary.magnitude;

                if (distToPrimary > primaryGrapple.CurrentRopeLength)
                {
                    float overshoot = distToPrimary - primaryGrapple.CurrentRopeLength;
                    float forceMag  = secondaryConstraintForce * 0.5f * (overshoot / primaryGrapple.CurrentRopeLength);
                    rb.AddForce(toPrimary.normalized * forceMag, ForceMode.Force);
                }
            }
        }

        /// <summary>Enables or disables the dual-grapple feature at runtime.</summary>
        public void SetEnabled(bool value)
        {
            isEnabled = value;
            if (!value)
                ReleaseSecondaryGrapple();
        }

        // ── Private helpers ───────────────────────────────────────────────────────

        private void AttachSecondary(Vector3 point)
        {
            secondaryAnchorPoint = point;
            secondaryRopeLength  = Mathf.Clamp(
                Vector3.Distance(rb != null ? rb.position : transform.position, point),
                secondaryMinRopeLength,
                secondaryMaxRopeLength);

            secondaryAttached  = true;
            secondaryInFlight  = false;
            secondaryHeadPos   = point;

            if (secondaryRopeRenderer != null)
                secondaryRopeRenderer.enabled = true;

            OnSecondaryAttached?.Invoke();
        }

        private IEnumerator FlyToSecondaryTarget(Vector3 targetPoint)
        {
            secondaryInFlight = true;
            secondaryHeadPos  = firePoint.position;

            float elapsed    = 0f;
            float maxFlyTime = secondaryMaxRopeLength / projectileSpeed;

            while (elapsed < maxFlyTime && secondaryInFlight)
            {
                secondaryHeadPos = Vector3.MoveTowards(
                    secondaryHeadPos, targetPoint, projectileSpeed * Time.deltaTime);

                if (Vector3.Distance(secondaryHeadPos, targetPoint) < 0.5f)
                {
                    AttachSecondary(targetPoint);
                    yield break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            // Timed out
            if (secondaryInFlight)
            {
                secondaryInFlight = false;
                if (secondaryRopeRenderer != null)
                    secondaryRopeRenderer.enabled = false;
            }
        }

        private void UpdateSecondaryHeadFlight()
        {
            if (!secondaryInFlight && !secondaryAttached)
                return;

            if (secondaryAttached)
                secondaryHeadPos = secondaryAnchorPoint;
        }

        private void UpdateSecondaryRopeVisual()
        {
            if (secondaryRopeRenderer == null) return;

            bool showRope = secondaryAttached || secondaryInFlight;
            secondaryRopeRenderer.enabled = showRope;

            if (!showRope) return;

            Vector3 playerPos = rb != null ? rb.position : transform.position;
            secondaryRopeRenderer.positionCount = 2;
            secondaryRopeRenderer.SetPosition(0, secondaryAnchorPoint);
            secondaryRopeRenderer.SetPosition(1, playerPos);

            // Tint rope gold/orange, fade slightly when slack
            bool isSlack = Vector3.Distance(playerPos, secondaryAnchorPoint) < secondaryRopeLength * 0.95f;
            float alpha  = isSlack ? 0.5f : 1f;
            Color c      = new Color(secondaryRopeColor.r, secondaryRopeColor.g, secondaryRopeColor.b, alpha);
            secondaryRopeRenderer.startColor = c;
            secondaryRopeRenderer.endColor   = c;
        }

        private Vector3 GetAimDirection()
        {
            Camera cam = Camera.main;
            if (cam != null) return cam.transform.forward;
            return transform.forward;
        }

        private void OnDrawGizmosSelected()
        {
            if (!secondaryAttached) return;
            Gizmos.color = secondaryRopeColor;
            Gizmos.DrawLine(firePoint != null ? firePoint.position : transform.position, secondaryAnchorPoint);
            Gizmos.DrawWireSphere(secondaryAnchorPoint, 0.3f);
        }
    }
}
