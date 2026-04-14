using UnityEngine;

namespace TitanAscent.Environment
{
    public enum AnchorVisualState
    {
        Idle,
        Highlighted,
        Attached
    }

    public class SurfaceAnchorPoint : MonoBehaviour
    {
        [Header("Surface Configuration")]
        [SerializeField] private SurfaceType surfaceType = SurfaceType.ScaleArmor;
        [Range(0f, 1f)]
        [SerializeField] private float holdStrength = 1f;
        [SerializeField] private float maxApproachAngle = 75f; // Max degrees from normal for valid attachment

        [Header("Visual")]
        [SerializeField] private Renderer anchorRenderer;
        [SerializeField] private Color idleColor = new Color(0.5f, 0.5f, 1f, 0.5f);
        [SerializeField] private Color highlightColor = new Color(0f, 1f, 0.5f, 0.8f);
        [SerializeField] private Color attachedColor = new Color(1f, 0.8f, 0f, 1f);
        [SerializeField] private bool showInEditor = true;

        private AnchorVisualState visualState = AnchorVisualState.Idle;
        private MaterialPropertyBlock propBlock;
        private SurfaceProperties surfaceProperties;   // cached in Awake
        private bool isHighlighted;
        private bool isAttached;

        public SurfaceType AnchorSurfaceType => surfaceType;
        public float HoldStrength => holdStrength;
        /// <summary>
        /// Whether this anchor can accept a grapple. Reads from the co-located
        /// <see cref="SurfaceProperties"/> component (cached at Awake) when present;
        /// otherwise falls back to <see cref="holdStrength"/>.
        /// </summary>
        public bool IsGrappleable => surfaceProperties != null ? surfaceProperties.IsGrappleable : holdStrength > 0f;
        public AnchorVisualState VisualState => visualState;

        private void Awake()
        {
            propBlock = new MaterialPropertyBlock();
            if (anchorRenderer == null)
                anchorRenderer = GetComponent<Renderer>();
            surfaceProperties = GetComponent<SurfaceProperties>();
        }

        private void Start()
        {
            UpdateVisuals();
        }

        /// <summary>
        /// Validates whether a grapple can attach to this anchor from the given approach direction.
        /// </summary>
        public bool ValidateAttachment(Vector3 approachDirection)
        {
            if (!IsGrappleable) return false;
            if (holdStrength <= 0f) return false;

            // Check approach angle against the anchor's forward (surface outward normal)
            Vector3 surfaceNormal = transform.up;
            float angle = Vector3.Angle(-approachDirection, surfaceNormal);

            return angle <= maxApproachAngle;
        }

        public void SetHoldStrength(float strength)
        {
            holdStrength = Mathf.Clamp01(strength);
        }

        public void SetHighlighted(bool highlighted)
        {
            isHighlighted = highlighted;
            UpdateVisualState();
        }

        public void SetAttached(bool attached)
        {
            isAttached = attached;
            UpdateVisualState();
        }

        private void UpdateVisualState()
        {
            AnchorVisualState newState;

            if (isAttached)
                newState = AnchorVisualState.Attached;
            else if (isHighlighted)
                newState = AnchorVisualState.Highlighted;
            else
                newState = AnchorVisualState.Idle;

            if (newState != visualState)
            {
                visualState = newState;
                UpdateVisuals();
            }
        }

        private void UpdateVisuals()
        {
            if (anchorRenderer == null) return;

            Color targetColor;
            switch (visualState)
            {
                case AnchorVisualState.Highlighted: targetColor = highlightColor; break;
                case AnchorVisualState.Attached:    targetColor = attachedColor;  break;
                default:                            targetColor = idleColor;      break;
            }

            anchorRenderer.GetPropertyBlock(propBlock);
            propBlock.SetColor("_BaseColor", targetColor);
            propBlock.SetColor("_EmissionColor", targetColor * (visualState == AnchorVisualState.Idle ? 0.3f : 1.5f));
            anchorRenderer.SetPropertyBlock(propBlock);
        }

        private void OnDrawGizmosSelected()
        {
            if (!showInEditor) return;

            Gizmos.color = holdStrength > 0.5f ? Color.green : Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.4f);

            // Draw surface normal
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position, transform.up * 1f);
        }
    }
}
