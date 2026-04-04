using UnityEngine;
using UnityEngine.Events;
using System.Collections;

namespace TitanAscent.Grapple
{
    public enum GrappleState
    {
        Idle,
        Flying,
        Attached,
        Retracting
    }

    [RequireComponent(typeof(LineRenderer))]
    public class GrappleController : MonoBehaviour
    {
        [Header("Grapple Configuration")]
        [SerializeField] private float maxRopeLength = 50f;
        [SerializeField] private float minRopeLength = 2f;
        [SerializeField] private float retractionSpeed = 12f;
        [SerializeField] private float grappleForce = 500f;
        [SerializeField] private float fireRate = 0.3f;
        [SerializeField] private float projectileSpeed = 80f;

        [Header("Input")]
        [SerializeField] private KeyCode fireKey = KeyCode.Mouse0;
        [SerializeField] private KeyCode releaseKey = KeyCode.Mouse0;
        [SerializeField] private KeyCode retractKey = KeyCode.E;

        [Header("Layer Masks")]
        [SerializeField] private LayerMask grappleLayerMask = ~0;
        [SerializeField] private LayerMask aimLayerMask = ~0;

        [Header("References")]
        [SerializeField] private Transform firePoint;
        [SerializeField] private RopeSimulator ropeSimulator;
        [SerializeField] private GrappleAimAssist aimAssist;

        [Header("Preview Line")]
        [SerializeField] private LineRenderer previewLine;
        [SerializeField] private Color previewValidColor = new Color(0f, 1f, 0.5f, 0.5f);
        [SerializeField] private Color previewInvalidColor = new Color(1f, 0.2f, 0.2f, 0.3f);

        [Header("Events")]
        public UnityEvent OnGrappleAttached;
        public UnityEvent OnGrappleReleased;
        public UnityEvent<float> OnRopeTensionChanged;

        private GrappleState currentState = GrappleState.Idle;
        private Rigidbody rb;
        private Player.PlayerController playerController;
        private Player.EmergencyRecovery emergencyRecovery;

        private Vector3 attachPoint;
        private SurfaceAnchorPoint attachedAnchor;
        private SpringJoint ropeJoint;
        private float currentRopeLength;
        private float lastFireTime;
        private bool isRetractHeld;

        // Flying grapple head simulation
        private Vector3 grappleHeadPosition;
        private Vector3 grappleHeadVelocity;
        private bool grappleHeadInFlight;

        public GrappleState CurrentState => currentState;
        public bool IsAttached => currentState == GrappleState.Attached;
        public bool IsActive => currentState != GrappleState.Idle;
        public Vector3 AttachPoint => attachPoint;
        public float CurrentRopeLength => currentRopeLength;

        private void Awake()
        {
            rb = GetComponentInParent<Rigidbody>();
            if (rb == null) rb = GetComponent<Rigidbody>();

            playerController = GetComponentInParent<Player.PlayerController>();
            if (playerController == null) playerController = GetComponent<Player.PlayerController>();

            emergencyRecovery = GetComponentInParent<Player.EmergencyRecovery>();
            if (emergencyRecovery == null) emergencyRecovery = GetComponent<Player.EmergencyRecovery>();

            if (firePoint == null) firePoint = transform;

            if (ropeSimulator == null)
                ropeSimulator = GetComponentInChildren<RopeSimulator>();
        }

        private void Update()
        {
            HandleInput();
            UpdateGrappleHead();
            UpdatePreviewLine();
            UpdateRopeTension();
        }

        private void FixedUpdate()
        {
            if (currentState == GrappleState.Attached)
            {
                ApplySwingForce();
                HandleRetraction();
            }
        }

        private void HandleInput()
        {
            bool canFire = Time.time - lastFireTime > GetAdjustedFireRate();

            if (Input.GetKeyDown(fireKey) && canFire)
            {
                if (currentState == GrappleState.Idle || currentState == GrappleState.Flying)
                {
                    TryFireGrapple();
                }
                else if (currentState == GrappleState.Attached)
                {
                    // Emergency re-fire: release and refire immediately
                    ReleaseGrapple();
                    TryFireGrapple();
                }
            }

            if (Input.GetKeyDown(KeyCode.Q) && currentState != GrappleState.Idle)
            {
                ReleaseGrapple();
            }

            isRetractHeld = Input.GetKey(retractKey) && currentState == GrappleState.Attached;
        }

        private float GetAdjustedFireRate()
        {
            // Emergency recovery can shorten fire rate
            if (emergencyRecovery != null && emergencyRecovery.IsActive)
                return fireRate * 0.5f;
            return fireRate;
        }

        private void TryFireGrapple()
        {
            Vector3 aimDirection = GetAimDirection();
            Vector3 fireOrigin = firePoint.position;

            // Check for valid grapple target
            float adjustedRange = maxRopeLength;
            if (emergencyRecovery != null && emergencyRecovery.IsActive)
                adjustedRange += emergencyRecovery.GrappleRangeBonus;

            if (Physics.Raycast(fireOrigin, aimDirection, out RaycastHit hit, adjustedRange, grappleLayerMask))
            {
                // Verify the surface is grappleable
                SurfaceAnchorPoint anchor = hit.collider.GetComponent<SurfaceAnchorPoint>();
                if (anchor != null && !anchor.ValidateAttachment(aimDirection))
                {
                    // Surface rejected the grapple
                    StartCoroutine(FireAndMissCoroutine(fireOrigin, hit.point));
                    return;
                }

                // Launch grapple head toward target
                lastFireTime = Time.time;
                currentState = GrappleState.Flying;
                grappleHeadPosition = fireOrigin;
                Vector3 toTarget = (hit.point - fireOrigin);
                float distance = toTarget.magnitude;
                grappleHeadVelocity = toTarget.normalized * projectileSpeed;
                grappleHeadInFlight = true;

                // For responsiveness, attach immediately if within short range
                if (distance < 15f)
                {
                    AttachGrapple(hit.point, hit.normal, anchor);
                }
                else
                {
                    StartCoroutine(FlyToTargetCoroutine(hit.point, hit.normal, anchor));
                }
            }
            else
            {
                // Missed — brief visual feedback
                lastFireTime = Time.time;
                StartCoroutine(FireAndMissCoroutine(fireOrigin, fireOrigin + aimDirection * adjustedRange));
            }
        }

        private IEnumerator FlyToTargetCoroutine(Vector3 targetPoint, Vector3 surfaceNormal, SurfaceAnchorPoint anchor)
        {
            float elapsed = 0f;
            float maxFlyTime = maxRopeLength / projectileSpeed;

            while (elapsed < maxFlyTime && currentState == GrappleState.Flying)
            {
                grappleHeadPosition = Vector3.MoveTowards(grappleHeadPosition, targetPoint, projectileSpeed * Time.deltaTime);

                if (Vector3.Distance(grappleHeadPosition, targetPoint) < 0.5f)
                {
                    AttachGrapple(targetPoint, surfaceNormal, anchor);
                    yield break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            // Timed out without attaching
            if (currentState == GrappleState.Flying)
            {
                currentState = GrappleState.Idle;
                grappleHeadInFlight = false;
            }
        }

        private IEnumerator FireAndMissCoroutine(Vector3 from, Vector3 to)
        {
            currentState = GrappleState.Flying;
            grappleHeadPosition = from;
            grappleHeadInFlight = true;
            float t = 0f;
            float missTime = 0.3f;

            while (t < missTime)
            {
                grappleHeadPosition = Vector3.Lerp(from, to, t / missTime);
                t += Time.deltaTime;
                yield return null;
            }

            currentState = GrappleState.Idle;
            grappleHeadInFlight = false;
        }

        private void AttachGrapple(Vector3 point, Vector3 normal, SurfaceAnchorPoint anchor)
        {
            attachPoint = point;
            attachedAnchor = anchor;
            currentState = GrappleState.Attached;
            grappleHeadInFlight = false;
            grappleHeadPosition = point;

            // Calculate initial rope length from current player position
            currentRopeLength = Vector3.Distance(rb.transform.position, attachPoint);
            currentRopeLength = Mathf.Clamp(currentRopeLength, minRopeLength, maxRopeLength);

            // Create spring joint for swing physics
            if (ropeJoint != null)
                Destroy(ropeJoint);

            ropeJoint = rb.gameObject.AddComponent<SpringJoint>();
            ropeJoint.autoConfigureConnectedAnchor = false;
            ropeJoint.connectedAnchor = attachPoint;
            ropeJoint.maxDistance = currentRopeLength;
            ropeJoint.minDistance = minRopeLength;
            ropeJoint.spring = 8f;
            ropeJoint.damper = 4f;
            ropeJoint.massScale = 4.5f;
            ropeJoint.enableCollision = false;

            // Notify rope simulator
            if (ropeSimulator != null)
            {
                ropeSimulator.SetAnchorPoint(attachPoint);
                ropeSimulator.SetLength(currentRopeLength);
            }

            // Notify anchor
            if (attachedAnchor != null)
                attachedAnchor.SetAttached(true);

            OnGrappleAttached?.Invoke();
        }

        public void ReleaseGrapple()
        {
            if (currentState == GrappleState.Idle) return;

            if (ropeJoint != null)
            {
                Destroy(ropeJoint);
                ropeJoint = null;
            }

            if (attachedAnchor != null)
            {
                attachedAnchor.SetAttached(false);
                attachedAnchor = null;
            }

            if (ropeSimulator != null)
                ropeSimulator.Detach();

            grappleHeadInFlight = false;
            currentState = GrappleState.Idle;

            OnGrappleReleased?.Invoke();
        }

        private void ApplySwingForce()
        {
            if (rb == null) return;

            // Extra force applied toward attach point to keep swing feeling snappy
            Vector3 toAnchor = attachPoint - rb.transform.position;
            float distance = toAnchor.magnitude;

            if (distance > currentRopeLength)
            {
                float overshoot = distance - currentRopeLength;
                float appliedForce = grappleForce * (overshoot / currentRopeLength);
                if (emergencyRecovery != null && emergencyRecovery.IsActive)
                    appliedForce += emergencyRecovery.GrappleForceBonus;

                rb.AddForce(toAnchor.normalized * appliedForce, ForceMode.Force);
            }
        }

        private void HandleRetraction()
        {
            if (!isRetractHeld) return;

            currentRopeLength = Mathf.Max(minRopeLength, currentRopeLength - retractionSpeed * Time.fixedDeltaTime);

            if (ropeJoint != null)
                ropeJoint.maxDistance = currentRopeLength;

            if (ropeSimulator != null)
                ropeSimulator.SetLength(currentRopeLength);
        }

        private void UpdateGrappleHead()
        {
            // Lerp head to current position for rope rendering
            if (currentState == GrappleState.Attached)
                grappleHeadPosition = attachPoint;
        }

        private void UpdatePreviewLine()
        {
            if (previewLine == null) return;

            if (currentState == GrappleState.Idle)
            {
                Vector3 aimDir = GetAimDirection();
                Vector3 origin = firePoint.position;

                if (Physics.Raycast(origin, aimDir, out RaycastHit hit, maxRopeLength, aimLayerMask))
                {
                    SurfaceAnchorPoint anchor = hit.collider.GetComponent<SurfaceAnchorPoint>();
                    bool valid = anchor == null || anchor.ValidateAttachment(aimDir);
                    previewLine.startColor = valid ? previewValidColor : previewInvalidColor;
                    previewLine.endColor = previewLine.startColor;
                    previewLine.SetPosition(0, origin);
                    previewLine.SetPosition(1, hit.point);
                    previewLine.enabled = true;
                }
                else
                {
                    previewLine.SetPosition(0, origin);
                    previewLine.SetPosition(1, origin + aimDir * maxRopeLength);
                    previewLine.startColor = previewInvalidColor;
                    previewLine.endColor = previewInvalidColor;
                    previewLine.enabled = true;
                }
            }
            else
            {
                previewLine.enabled = false;
            }
        }

        private void UpdateRopeTension()
        {
            if (ropeSimulator != null && currentState == GrappleState.Attached)
            {
                float tension = ropeSimulator.GetTension();
                OnRopeTensionChanged?.Invoke(tension);
            }
        }

        private Vector3 GetAimDirection()
        {
            if (aimAssist != null && aimAssist.HasTarget)
                return (aimAssist.BestTarget - firePoint.position).normalized;

            Camera cam = Camera.main;
            if (cam != null)
                return cam.transform.forward;

            return transform.forward;
        }

        /// <summary>Sets maximum rope length at runtime. Used by MovementTuner.</summary>
        public void SetMaxRopeLength(float value) => maxRopeLength = Mathf.Max(minRopeLength + 1f, value);

        /// <summary>Sets rope retraction speed at runtime. Used by MovementTuner.</summary>
        public void SetRetractionSpeed(float value) => retractionSpeed = Mathf.Max(0.1f, value);

        private void OnDrawGizmosSelected()
        {
            if (firePoint == null) return;
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(firePoint.position, 0.1f);

            if (currentState == GrappleState.Attached)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(firePoint.position, attachPoint);
                Gizmos.DrawWireSphere(attachPoint, 0.3f);
            }
        }
    }
}
