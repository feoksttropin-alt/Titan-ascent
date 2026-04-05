using UnityEngine;

namespace TitanAscent.Systems
{
    /// <summary>
    /// ScriptableObject that stores a complete named movement feel profile.
    /// Create new assets via the menu: TitanAscent / Movement Preset.
    /// </summary>
    [CreateAssetMenu(fileName = "MovementPreset", menuName = "TitanAscent/Movement Preset")]
    public class MovementPresets : ScriptableObject
    {
        [Header("Identity")]
        public string presetName = "Default";
        [TextArea(2, 4)]
        public string description = "Default movement feel for Titan Ascent.";

        // -----------------------------------------------------------------------
        // Rope
        // -----------------------------------------------------------------------
        [Header("Rope")]
        public float maxRopeLength = 50f;
        public float minRopeLength = 2f;
        public float ropeRetractionSpeed = 12f;
        public float ropeElasticity = 0.15f;
        public float ropeDamping = 0.05f;
        public int ropeConstraintIterations = 5;

        // -----------------------------------------------------------------------
        // Grapple
        // -----------------------------------------------------------------------
        [Header("Grapple")]
        public float grappleFireForce = 500f;
        public float grappleAttachForgiveness = 1.2f;
        public float grappleAimAssistAngle = 30f;
        public float fireRate = 0.3f;

        // -----------------------------------------------------------------------
        // Swing
        // -----------------------------------------------------------------------
        [Header("Swing")]
        public float swingMomentumConservation = 0.92f;
        public float maxSwingSpeed = 22f;
        public float swingDampingAtPeak = 0.08f;

        // -----------------------------------------------------------------------
        // Thrusters
        // -----------------------------------------------------------------------
        [Header("Thrusters")]
        public float maxThrusterEnergy = 100f;
        public float thrusterForce = 8f;
        public float thrusterRegenRate = 15f;
        public float thrusterRegenDelay = 1.2f;

        // -----------------------------------------------------------------------
        // Grip
        // -----------------------------------------------------------------------
        [Header("Grip")]
        public float maxGrip = 100f;
        public float gripDrainRate = 25f;
        public float gripRegenDelay = 1.5f;

        // -----------------------------------------------------------------------
        // Physics
        // -----------------------------------------------------------------------
        [Header("Physics")]
        public float gravityScale = 1.8f;
        public float playerMass = 75f;
        public float airDrag = 0.08f;

        // -----------------------------------------------------------------------
        // Emergency
        // -----------------------------------------------------------------------
        [Header("Emergency Recovery")]
        public float emergencyWindowDuration = 2f;
        public float emergencyGrappleRangeBonus = 15f;
        public float emergencyGrappleForceBonus = 1.5f;

        // -----------------------------------------------------------------------
        // Factory
        // -----------------------------------------------------------------------

        /// <summary>
        /// Returns a new <see cref="MovementPresets"/> instance initialised with
        /// the canonical default values. Used as a code-side fallback when no
        /// asset is assigned.
        /// </summary>
        public static MovementPresets GetDefault()
        {
            MovementPresets preset = CreateInstance<MovementPresets>();

            preset.presetName   = "Default";
            preset.description  = "Fallback preset constructed at runtime — assign a MovementPreset asset for designer-tuned values.";

            // Rope
            preset.maxRopeLength          = 50f;
            preset.minRopeLength          = 2f;
            preset.ropeRetractionSpeed    = 12f;
            preset.ropeElasticity         = 0.15f;
            preset.ropeDamping            = 0.05f;
            preset.ropeConstraintIterations = 5;

            // Grapple
            preset.grappleFireForce         = 500f;
            preset.grappleAttachForgiveness = 1.2f;
            preset.grappleAimAssistAngle    = 30f;
            preset.fireRate                 = 0.3f;

            // Swing
            preset.swingMomentumConservation = 0.92f;
            preset.maxSwingSpeed             = 22f;
            preset.swingDampingAtPeak        = 0.08f;

            // Thrusters
            preset.maxThrusterEnergy  = 100f;
            preset.thrusterForce      = 8f;
            preset.thrusterRegenRate  = 15f;
            preset.thrusterRegenDelay = 1.2f;

            // Grip
            preset.maxGrip         = 100f;
            preset.gripDrainRate   = 25f;
            preset.gripRegenDelay  = 1.5f;

            // Physics
            preset.gravityScale = 1.8f;
            preset.playerMass   = 75f;
            preset.airDrag      = 0.08f;

            // Emergency
            preset.emergencyWindowDuration    = 2f;
            preset.emergencyGrappleRangeBonus = 15f;
            preset.emergencyGrappleForceBonus = 1.5f;

            return preset;
        }
    }
}
