using UnityEngine;
using TitanAscent.Player;

namespace TitanAscent.Scene
{
    /// <summary>
    /// Renders a distant full-titan silhouette mesh at 500m+ draw distance.
    /// The titan is 10 000m tall and provides an enormous sense of scale.
    /// Visible from zones 1–6; fades out as the player climbs above zone 6
    /// (alpha lerp between fadeStartHeight and fadeEndHeight).
    /// Subtle breathing animation oscillates localScale.y ±0.002 on a 4s cycle.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    public class BackgroundTitanRenderer : MonoBehaviour
    {
        [Header("Mesh")]
        [SerializeField] private MeshRenderer titanSilhouetteMesh;

        [Header("Appearance")]
        [SerializeField] private Color silhouetteColor = new Color(0.08f, 0.08f, 0.08f, 1f);

        [Header("Fade Heights")]
        [SerializeField] private float fadeStartHeight = 5500f;
        [SerializeField] private float fadeEndHeight   = 6500f;

        // ------------------------------------------------------------------
        // Constants
        // ------------------------------------------------------------------

        private const float BreathingAmplitude = 0.002f;
        private const float BreathingPeriod    = 4f;

        // ------------------------------------------------------------------
        // Private state
        // ------------------------------------------------------------------

        private Material silhouetteMat;
        private Vector3  baseScale;
        private PlayerController player;

        // ------------------------------------------------------------------
        // Lifecycle
        // ------------------------------------------------------------------

        private void Awake()
        {
            if (titanSilhouetteMesh == null)
                titanSilhouetteMesh = GetComponent<MeshRenderer>();

            // Create a dedicated material instance so we can modify alpha
            silhouetteMat = new Material(titanSilhouetteMesh.sharedMaterial
                            ? titanSilhouetteMesh.sharedMaterial
                            : new Material(Shader.Find("Standard")));

            // Configure silhouette look: dark color, slight rim highlight
            silhouetteMat.color = silhouetteColor;
            silhouetteMat.SetFloat("_Glossiness", 0f);
            silhouetteMat.SetFloat("_Metallic", 0f);

            titanSilhouetteMesh.material = silhouetteMat;
            baseScale = transform.localScale;
        }

        private void Start()
        {
            player = FindFirstObjectByType<PlayerController>();
        }

        private void Update()
        {
            UpdateFadeAlpha();
            UpdateBreathingScale();
        }

        // ------------------------------------------------------------------
        // Fade
        // ------------------------------------------------------------------

        private void UpdateFadeAlpha()
        {
            float playerHeight = player != null ? player.transform.position.y : 0f;

            float alpha = 1f - Mathf.InverseLerp(fadeStartHeight, fadeEndHeight, playerHeight);
            alpha = Mathf.Clamp01(alpha);

            Color c = silhouetteMat.color;
            c.a = alpha * silhouetteColor.a;
            silhouetteMat.color = c;

            // Disable renderer entirely when invisible to save draw calls
            titanSilhouetteMesh.enabled = alpha > 0.001f;
        }

        // ------------------------------------------------------------------
        // Breathing animation
        // ------------------------------------------------------------------

        private void UpdateBreathingScale()
        {
            float t     = Time.time;
            float yOff  = Mathf.Sin(t * (Mathf.PI * 2f / BreathingPeriod)) * BreathingAmplitude;

            transform.localScale = new Vector3(
                baseScale.x,
                baseScale.y * (1f + yOff),
                baseScale.z);
        }

        // ------------------------------------------------------------------
        // Gizmo
        // ------------------------------------------------------------------

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(transform.position, new Vector3(200f, 10000f, 200f));
        }
    }
}
