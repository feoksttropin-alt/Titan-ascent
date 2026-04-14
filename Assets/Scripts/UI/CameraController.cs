using UnityEngine;
using System.Collections;

namespace TitanAscent.UI
{
    [RequireComponent(typeof(Camera))]
    public class CameraController : MonoBehaviour
    {
        [Header("Follow Settings")]
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 followOffset = new Vector3(0f, 2f, -8f);
        [SerializeField] private float positionLag = 6f;
        [SerializeField] private float rotationLag = 8f;

        [Header("FOV")]
        [SerializeField] private float normalFOV = 65f;
        [SerializeField] private float swingFOV = 75f;
        [SerializeField] private float fovTransitionSpeed = 4f;

        [Header("Fall Zoom")]
        [SerializeField] private float maxZoomOutFOV = 95f;
        [SerializeField] private float fallZoomStartDistance = 50f;
        [SerializeField] private float fallZoomMaxDistance = 500f;
        [SerializeField] private float landingFOVSnapSpeed = 8f;   // ~0.5s to snap back

        [Header("Height Record")]
        [SerializeField] private float recordUpwardShift = 0.4f;   // metres upward
        [SerializeField] private float recordShiftDuration = 0.35f;

        [Header("Landing Shake")]
        [SerializeField] private float shakeDecaySpeed = 8f;
        [SerializeField] private float smallFallShakeAmplitude = 0.1f;
        [SerializeField] private float largeFallShakeAmplitude = 0.6f;
        [SerializeField] private float shakeFrequency = 25f;

        [Header("Mouse Look")]
        [SerializeField] private float mouseSensitivity = 2f;
        [SerializeField] private float pitchMin = -40f;
        [SerializeField] private float pitchMax = 80f;

        private Camera cam;
        private Player.PlayerController playerController;
        private Systems.FallTracker fallTracker;

        private float currentFOV;
        private float targetFOV;
        private float shakeAmplitude = 0f;
        private float shakeTime = 0f;
        private Vector3 shakeOffset = Vector3.zero;

        private float yaw = 0f;
        private float pitch = 15f;

        private Vector3 smoothVelocity;

        // Height record upward shift
        private float recordShiftAmount = 0f;
        private bool isSnappingFOVBack = false;
        private Coroutine snapFOVCoroutine;

        // Fall speed tracking
        private float _prevFallDistance = 0f;
        private float _fallSpeed = 0f;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            playerController = FindFirstObjectByType<Player.PlayerController>();
            fallTracker = FindFirstObjectByType<Systems.FallTracker>();

            currentFOV = normalFOV;
            targetFOV = normalFOV;
            cam.fieldOfView = normalFOV;

            if (target == null && playerController != null)
                target = playerController.transform;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void OnEnable()
        {
            if (fallTracker != null)
            {
                fallTracker.OnFallCompleted.AddListener(HandleFallLanding);
                fallTracker.OnNewHeightRecord.AddListener(HandleNewHeightRecord);
            }
        }

        private void OnDisable()
        {
            if (fallTracker != null)
            {
                fallTracker.OnFallCompleted.RemoveListener(HandleFallLanding);
                fallTracker.OnNewHeightRecord.RemoveListener(HandleNewHeightRecord);
            }
        }

        private void Update()
        {
            HandleMouseLook();
            TrackFallSpeed();
            UpdateFOV();
            UpdateShake();
        }

        private void TrackFallSpeed()
        {
            if (fallTracker != null && fallTracker.IsFalling)
            {
                float currentDist = fallTracker.CurrentFallDistance;
                float dt = Time.deltaTime;
                if (dt > 0f)
                    _fallSpeed = Mathf.Max(0f, (currentDist - _prevFallDistance) / dt);
                _prevFallDistance = currentDist;
            }
            else
            {
                _fallSpeed = 0f;
                _prevFallDistance = 0f;
            }
        }

        private void LateUpdate()
        {
            if (target == null) return;
            FollowTarget();
        }

        private void HandleMouseLook()
        {
            // Use InputHandler.MouseDelta (New Input System pixel delta) scaled to match
            // legacy Input.GetAxis("Mouse X/Y") magnitude (~pixels / 20).
            TitanAscent.Input.InputHandler ih = TitanAscent.Input.InputHandler.Instance;
            Vector2 delta = ih != null ? ih.MouseDelta * 0.05f : Vector2.zero;

            float mouseX = delta.x * mouseSensitivity;
            float mouseY = delta.y * mouseSensitivity;

            yaw += mouseX;
            pitch -= mouseY;
            pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);
        }

        private void FollowTarget()
        {
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 desiredPosition = target.position + rotation * followOffset;

            // Smooth follow with lag
            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref smoothVelocity, 1f / positionLag);

            // Apply shake offset
            transform.position += shakeOffset;

            // Look at target
            Quaternion targetRotation = Quaternion.LookRotation(target.position - transform.position + Vector3.up * 0.5f);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationLag);
        }

        private void UpdateFOV()
        {
            // While the landing snap coroutine is running, let it own the FOV.
            if (isSnappingFOVBack) return;

            if (cam == null) return;

            if (playerController == null)
            {
                // Minimal path: still apply fall FOV when we have a FallTracker
                ApplyFallFOVIfNeeded();
                return;
            }

            Player.PlayerState state = playerController.CurrentState;

            if (state == Player.PlayerState.Swinging)
            {
                targetFOV = swingFOV;
            }
            else if (fallTracker != null && fallTracker.IsFalling)
            {
                // Speed-based zoom: 60 + fallSpeed * 0.3, capped at 80
                float speedBasedFOV = Mathf.Clamp(60f + _fallSpeed * 0.3f, 60f, 80f);

                // Also keep the existing distance-based zoom for large falls (takes the wider value)
                float fallDist = fallTracker.CurrentFallDistance;
                float fallT = Mathf.Clamp01((fallDist - fallZoomStartDistance) / (fallZoomMaxDistance - fallZoomStartDistance));
                float distBasedFOV = Mathf.Lerp(normalFOV, maxZoomOutFOV, fallT);

                targetFOV = Mathf.Max(speedBasedFOV, distBasedFOV);
            }
            else
            {
                targetFOV = normalFOV;
            }

            currentFOV = Mathf.Lerp(currentFOV, targetFOV, Time.deltaTime * fovTransitionSpeed);
            cam.fieldOfView = currentFOV;
        }

        private void ApplyFallFOVIfNeeded()
        {
            if (fallTracker != null && fallTracker.IsFalling)
            {
                float speedBasedFOV = Mathf.Clamp(60f + _fallSpeed * 0.3f, 60f, 80f);
                targetFOV = speedBasedFOV;
            }
            else
            {
                targetFOV = normalFOV;
            }
            currentFOV = Mathf.Lerp(currentFOV, targetFOV, Time.deltaTime * fovTransitionSpeed);
            cam.fieldOfView = currentFOV;
        }

        private void UpdateShake()
        {
            if (shakeAmplitude > 0.001f)
            {
                shakeTime += Time.deltaTime * shakeFrequency;
                shakeOffset = new Vector3(
                    Mathf.Sin(shakeTime * 1.1f) * shakeAmplitude,
                    Mathf.Sin(shakeTime * 0.9f) * shakeAmplitude * 0.6f,
                    0f
                );
                shakeAmplitude = Mathf.Lerp(shakeAmplitude, 0f, Time.deltaTime * shakeDecaySpeed);
            }
            else
            {
                shakeOffset = Vector3.zero;
                shakeAmplitude = 0f;
            }
        }

        private void HandleFallLanding(Systems.FallData data)
        {
            float fallProportion = Mathf.Clamp01(data.distance / fallZoomMaxDistance);
            shakeAmplitude = Mathf.Lerp(smallFallShakeAmplitude, largeFallShakeAmplitude, fallProportion);
            shakeTime = 0f;

            // Snap FOV back to baseFOV over ~0.5s
            if (snapFOVCoroutine != null) StopCoroutine(snapFOVCoroutine);
            snapFOVCoroutine = StartCoroutine(SnapFOVBackCoroutine());
        }

        private void HandleNewHeightRecord(float newRecord)
        {
            // Trigger an upward shift for the height record visual
            recordShiftAmount = recordUpwardShift;
            StartCoroutine(ClearRecordShift());
        }

        private IEnumerator ClearRecordShift()
        {
            yield return new WaitForSeconds(recordShiftDuration);
            recordShiftAmount = 0f;
        }

        private IEnumerator SnapFOVBackCoroutine()
        {
            if (cam == null) yield break;

            const float baseFOV = 60f;
            const float snapDuration = 0.5f;
            float elapsed = 0f;
            float startFOV = currentFOV;

            isSnappingFOVBack = true;
            while (elapsed < snapDuration)
            {
                elapsed += Time.deltaTime;
                currentFOV = Mathf.Lerp(startFOV, baseFOV, elapsed / snapDuration);
                cam.fieldOfView = currentFOV;
                yield return null;
            }

            currentFOV = baseFOV;
            cam.fieldOfView = baseFOV;
            isSnappingFOVBack = false;
            snapFOVCoroutine = null;
        }
    }
}
