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
                fallTracker.OnFallCompleted.AddListener(HandleFallLanding);
        }

        private void OnDisable()
        {
            if (fallTracker != null)
                fallTracker.OnFallCompleted.RemoveListener(HandleFallLanding);
        }

        private void Update()
        {
            HandleMouseLook();
            UpdateFOV();
            UpdateShake();
        }

        private void LateUpdate()
        {
            if (target == null) return;
            FollowTarget();
        }

        private void HandleMouseLook()
        {
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

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
            if (playerController == null) return;

            Player.PlayerState state = playerController.CurrentState;

            if (state == Player.PlayerState.Swinging)
            {
                targetFOV = swingFOV;
            }
            else if (state == Player.PlayerState.Falling && fallTracker != null)
            {
                float fallDist = fallTracker.CurrentFallDistance;
                float fallT = Mathf.Clamp01((fallDist - fallZoomStartDistance) / (fallZoomMaxDistance - fallZoomStartDistance));
                targetFOV = Mathf.Lerp(normalFOV, maxZoomOutFOV, fallT);
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
        }
    }
}
