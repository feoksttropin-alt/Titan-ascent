using UnityEngine;

namespace TitanAscent.Environment
{
    public enum BoneAge
    {
        Ancient,
        Old,
        Recent
    }

    /// <summary>
    /// Component for bone ridge geometry. Manages anchor hold strength and visual
    /// representation based on age, exposure, and structural integrity.
    /// - Ancient bones: yellowing material tint.
    /// - Low integrity: reduces child SurfaceAnchorPoint hold strengths.
    /// - High integrity + exposed: faint white rim light on bone edges.
    /// </summary>
    public class BoneStructure : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        // Inspector
        // -----------------------------------------------------------------------

        [Header("Properties")]
        [SerializeField] private BoneAge boneAge            = BoneAge.Old;
        [SerializeField] private bool    isExposed          = true;
        [Range(0f, 1f)]
        [SerializeField] private float   structuralIntegrity = 1f;

        [Header("Visual")]
        [SerializeField] private float   yellowHueShift     = 0.08f;   // hue shift for ancient bones
        [SerializeField] private float   rimLightIntensity  = 0.1f;

        // -----------------------------------------------------------------------
        // Private state
        // -----------------------------------------------------------------------

        private MaterialPropertyBlock _propBlock;
        private Light                 _rimLight;

        // -----------------------------------------------------------------------
        // Lifecycle
        // -----------------------------------------------------------------------

        private void Start()
        {
            _propBlock = new MaterialPropertyBlock();

            ApplyAgeColoring();
            ApplyIntegrityEffects();
            ConfigureRimLight();
        }

        // -----------------------------------------------------------------------
        // Age coloring
        // -----------------------------------------------------------------------

        private void ApplyAgeColoring()
        {
            if (boneAge != BoneAge.Ancient) return;

            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            foreach (Renderer rend in renderers)
            {
                if (rend.sharedMaterial == null) continue;

                rend.GetPropertyBlock(_propBlock);

                Color baseColor = rend.sharedMaterial.color;
                Color.RGBToHSV(baseColor, out float h, out float s, out float v);

                // Shift hue toward yellow, slightly desaturate
                h = Mathf.Repeat(h + yellowHueShift, 1f);
                s = Mathf.Clamp01(s - 0.05f);
                v = Mathf.Clamp01(v * 0.9f);

                Color aged = Color.HSVToRGB(h, s, v);
                _propBlock.SetColor("_BaseColor", aged);
                _propBlock.SetColor("_Color",     aged);
                rend.SetPropertyBlock(_propBlock);
            }
        }

        // -----------------------------------------------------------------------
        // Structural integrity: reduce anchor hold strengths
        // -----------------------------------------------------------------------

        private void ApplyIntegrityEffects()
        {
            if (structuralIntegrity >= 0.5f) return;

            float penalty = 1f - structuralIntegrity;

            SurfaceAnchorPoint[] anchors = GetComponentsInChildren<SurfaceAnchorPoint>();
            foreach (SurfaceAnchorPoint anchor in anchors)
            {
                // Reduce hold strength by the integrity deficit factor
                float newStrength = Mathf.Clamp01(anchor.HoldStrength * (1f - penalty));
                anchor.SetHoldStrength(newStrength);
            }

            if (anchors.Length > 0)
                Debug.Log($"[BoneStructure] {gameObject.name}: Reduced {anchors.Length} anchor(s) hold strength (integrity={structuralIntegrity:F2}).");
        }

        // -----------------------------------------------------------------------
        // Rim light for exposed high-integrity bones
        // -----------------------------------------------------------------------

        private void ConfigureRimLight()
        {
            // Only show rim light when fully exposed and high integrity
            bool showRim = isExposed && structuralIntegrity > 0.8f;

            // Find an existing child Light, or create one
            _rimLight = GetComponentInChildren<Light>();

            if (showRim)
            {
                if (_rimLight == null)
                {
                    GameObject lightGO = new GameObject("BoneRimLight");
                    lightGO.transform.SetParent(transform, false);
                    _rimLight = lightGO.AddComponent<Light>();
                    _rimLight.type  = LightType.Point;
                    _rimLight.range = 3f;
                }

                _rimLight.intensity = rimLightIntensity;
                _rimLight.color     = new Color(0.95f, 0.95f, 1f); // near white
                _rimLight.enabled   = true;
            }
            else if (_rimLight != null)
            {
                _rimLight.enabled = false;
            }
        }

        // -----------------------------------------------------------------------
        // Public accessors
        // -----------------------------------------------------------------------

        public BoneAge BoneAgeValue        => boneAge;
        public bool    IsExposed           => isExposed;
        public float   StructuralIntegrity => structuralIntegrity;

        /// <summary>Dynamically update structural integrity and reapply effects.</summary>
        public void SetStructuralIntegrity(float value)
        {
            structuralIntegrity = Mathf.Clamp01(value);
            ApplyIntegrityEffects();
            ConfigureRimLight();
        }
    }
}
