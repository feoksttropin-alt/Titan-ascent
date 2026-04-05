using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TitanAscent.Grapple;

namespace TitanAscent.UI
{
    public enum ReticleState
    {
        Idle,
        Targeting,
        Locked,
        Invalid,
        Attached
    }

    public class GrappleReticle : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────────

        [Header("UI References")]
        [SerializeField] private RectTransform  reticleRoot;
        [SerializeField] private Image          reticleImage;
        [SerializeField] private Image          lockRing;
        [SerializeField] private TextMeshProUGUI distanceText;

        [Header("State Colors")]
        [SerializeField] private Color idleColor      = new Color(0.7f, 0.7f, 0.7f, 0.5f);
        [SerializeField] private Color targetingColor = new Color(1.0f, 1.0f, 1.0f, 0.8f);
        [SerializeField] private Color lockedColor    = new Color(0.2f, 1.0f, 0.3f, 1.0f);
        [SerializeField] private Color invalidColor   = new Color(1.0f, 0.2f, 0.2f, 0.8f);

        [Header("Sizes")]
        [SerializeField] private float idleSize      = 12f;
        [SerializeField] private float targetingSize = 28f;
        [SerializeField] private float lockedSize    = 24f;
        [SerializeField] private float invalidSize   = 18f;

        [Header("Aim Assist Slide")]
        [SerializeField] private float aimAssistSlideSpeed = 15f;

        // ── Internal ─────────────────────────────────────────────────────────

        private GrappleController    grappleController;
        private GrappleAimAssist     aimAssist;
        private Camera               mainCamera;

        private ReticleState currentState = ReticleState.Idle;
        private Vector2 screenPosition;

        private Coroutine lockPulseCoroutine;
        private Coroutine attachFlashCoroutine;

        // ── Unity Lifecycle ──────────────────────────────────────────────────

        private void Awake()
        {
            mainCamera = Camera.main;

            if (reticleRoot == null)
                reticleRoot = GetComponent<RectTransform>();
        }

        private void Start()
        {
            grappleController = FindFirstObjectByType<GrappleController>();
            aimAssist         = FindFirstObjectByType<GrappleAimAssist>();

            if (grappleController != null)
            {
                grappleController.OnGrappleAttached.AddListener(HandleGrappleAttached);
                grappleController.OnGrappleReleased.AddListener(HandleGrappleReleased);
            }

            ApplyState(ReticleState.Idle, instant: true);
        }

        private void OnDestroy()
        {
            if (grappleController == null) return;
            grappleController.OnGrappleAttached.RemoveListener(HandleGrappleAttached);
            grappleController.OnGrappleReleased.RemoveListener(HandleGrappleReleased);
        }

        private void Update()
        {
            if (grappleController == null) return;

            UpdateScreenPosition();
            UpdateReticleState();
        }

        // ── Position ─────────────────────────────────────────────────────────

        private void UpdateScreenPosition()
        {
            Vector2 mousePos = Input.mousePosition;

            // If aim assist has a target, slide toward it
            if (aimAssist != null && aimAssist.HasTarget && mainCamera != null)
            {
                Vector3 anchorScreen = mainCamera.WorldToScreenPoint(aimAssist.BestTarget);
                if (anchorScreen.z > 0f)
                {
                    Vector2 targetScreen = new Vector2(anchorScreen.x, anchorScreen.y);
                    mousePos = Vector2.MoveTowards(mousePos, targetScreen, aimAssistSlideSpeed * Time.deltaTime
                                                   * Vector2.Distance(mousePos, targetScreen));
                }
            }

            screenPosition = mousePos;

            if (reticleRoot != null)
            {
                // Convert screen position to canvas-local position
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        reticleRoot.parent as RectTransform,
                        screenPosition,
                        null,   // null = screen space overlay
                        out Vector2 localPoint))
                {
                    reticleRoot.localPosition = localPoint;
                }
                else
                {
                    // Fallback: direct assignment
                    reticleRoot.position = screenPosition;
                }
            }
        }

        // ── State Logic ──────────────────────────────────────────────────────

        private void UpdateReticleState()
        {
            // Don't override attached / flashing
            if (currentState == ReticleState.Attached) return;

            if (grappleController.IsAttached)
            {
                ApplyState(ReticleState.Attached);
                return;
            }

            if (aimAssist != null && aimAssist.HasTarget)
            {
                // Locked via aim assist
                if (currentState != ReticleState.Locked)
                    ApplyState(ReticleState.Locked);

                UpdateDistanceText(aimAssist.BestTarget);
                return;
            }

            // Raycast from camera center for valid/invalid indication
            if (mainCamera != null)
            {
                Ray ray = mainCamera.ScreenPointToRay(screenPosition);
                if (Physics.Raycast(ray, out RaycastHit hit, 60f))
                {
                    Environment.SurfaceAnchorPoint anchor = hit.collider.GetComponent<Environment.SurfaceAnchorPoint>();
                    bool valid = anchor != null && anchor.IsGrappleable;
                    if (!valid)
                    {
                        Environment.SurfaceProperties props = hit.collider.GetComponent<Environment.SurfaceProperties>();
                        valid = props != null && props.IsGrappleable;
                    }

                    ReticleState target = valid ? ReticleState.Targeting : ReticleState.Invalid;
                    if (currentState != target) ApplyState(target);

                    if (valid && distanceText != null)
                    {
                        distanceText.gameObject.SetActive(true);
                        distanceText.text = Mathf.RoundToInt(hit.distance) + "m";
                    }
                    else if (distanceText != null)
                    {
                        distanceText.gameObject.SetActive(false);
                    }
                }
                else
                {
                    if (currentState != ReticleState.Idle) ApplyState(ReticleState.Idle);
                    if (distanceText != null) distanceText.gameObject.SetActive(false);
                }
            }
        }

        private void UpdateDistanceText(Vector3 worldTarget)
        {
            if (distanceText == null) return;

            float dist = Vector3.Distance(
                grappleController.transform.position, worldTarget);

            distanceText.gameObject.SetActive(true);
            distanceText.text = Mathf.RoundToInt(dist) + "m";
        }

        // ── Apply Visual State ───────────────────────────────────────────────

        private void ApplyState(ReticleState state, bool instant = false)
        {
            currentState = state;

            Color  targetColor;
            float  targetSize;
            bool   showLockRing = false;
            bool   showRoot     = true;

            switch (state)
            {
                case ReticleState.Targeting:
                    targetColor  = targetingColor;
                    targetSize   = targetingSize;
                    break;

                case ReticleState.Locked:
                    targetColor  = lockedColor;
                    targetSize   = lockedSize;
                    showLockRing = true;

                    if (!instant)
                    {
                        if (lockPulseCoroutine != null) StopCoroutine(lockPulseCoroutine);
                        lockPulseCoroutine = StartCoroutine(LockPulseCoroutine());
                    }
                    break;

                case ReticleState.Invalid:
                    targetColor  = invalidColor;
                    targetSize   = invalidSize;
                    break;

                case ReticleState.Attached:
                    if (!instant)
                    {
                        if (attachFlashCoroutine != null) StopCoroutine(attachFlashCoroutine);
                        attachFlashCoroutine = StartCoroutine(AttachFlashCoroutine());
                        return;
                    }
                    showRoot = false;
                    targetColor = Color.clear;
                    targetSize  = idleSize;
                    break;

                default: // Idle
                    targetColor  = idleColor;
                    targetSize   = idleSize;
                    break;
            }

            if (lockRing != null) lockRing.gameObject.SetActive(showLockRing);

            if (reticleImage != null)
            {
                reticleImage.color = targetColor;
                reticleRoot.sizeDelta = new Vector2(targetSize, targetSize);
            }

            if (reticleRoot != null) reticleRoot.gameObject.SetActive(showRoot);
        }

        // ── Event Handlers ───────────────────────────────────────────────────

        private void HandleGrappleAttached()
        {
            ApplyState(ReticleState.Attached);
        }

        private void HandleGrappleReleased()
        {
            ApplyState(ReticleState.Idle);
        }

        // ── Coroutines ───────────────────────────────────────────────────────

        private IEnumerator LockPulseCoroutine()
        {
            if (lockRing == null) yield break;

            float duration = 0.25f;
            float elapsed  = 0f;

            Vector3 startScale = lockRing.rectTransform.localScale;
            Vector3 bigScale   = startScale * 1.4f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float pulse = Mathf.Sin(t * Mathf.PI);
                lockRing.rectTransform.localScale = Vector3.Lerp(startScale, bigScale, pulse);
                lockRing.color = Color.Lerp(lockedColor, Color.white, pulse * 0.5f);
                yield return null;
            }

            lockRing.rectTransform.localScale = startScale;
            lockRing.color = lockedColor;
            lockPulseCoroutine = null;
        }

        private IEnumerator AttachFlashCoroutine()
        {
            if (reticleImage == null) yield break;

            // Brief white flash
            reticleImage.color = Color.white;
            reticleRoot.sizeDelta = new Vector2(lockedSize * 1.3f, lockedSize * 1.3f);

            yield return new WaitForSeconds(0.12f);

            // Hide
            reticleRoot.gameObject.SetActive(false);
            attachFlashCoroutine = null;
        }
    }
}
