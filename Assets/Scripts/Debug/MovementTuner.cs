#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using TitanAscent.Player;
using TitanAscent.Grapple;

namespace TitanAscent.Debug
{
    public class MovementTuner : MonoBehaviour
    {
        [SerializeField] private ThrusterSystem thrusterSystem;
        [SerializeField] private GripSystem gripSystem;
        [SerializeField] private GrappleController grappleController;
        [SerializeField] private GrappleAimAssist aimAssist;
        [SerializeField] private EmergencyRecovery emergencyRecovery;

        private bool isVisible = false;
        private Rect windowRect = new Rect(340f, 10f, 300f, 520f);

        // Tunable values with defaults
        private float gravityScale = 1f;
        private float swingDamping = 0.05f;
        private float retractionSpeed = 12f;
        private float maxRopeLength = 50f;
        private float thrusterForce = 8f;
        private float thrusterRegenRate = 15f;
        private float gripDrainRate = 25f;
        private float aimConeDegrees = 30f;
        private float grappleForgivenessRadius = 1f;
        private float emergencyWindowDuration = 2f;

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f2Key.wasPressedThisFrame)
                isVisible = !isVisible;
        }

        private void OnGUI()
        {
            if (!isVisible) return;
            windowRect = GUI.Window(9998, windowRect, DrawWindow, "MOVEMENT TUNER  [F2]");
        }

        private void DrawWindow(int id)
        {
            GUILayout.Space(4f);

            gravityScale       = DrawSlider("Gravity Scale",       gravityScale,       0.5f, 3f);
            swingDamping       = DrawSlider("Swing Damping",       swingDamping,       0f,   0.5f);
            retractionSpeed    = DrawSlider("Retraction Speed",    retractionSpeed,    1f,   25f);
            maxRopeLength      = DrawSlider("Max Rope Length",     maxRopeLength,      10f,  100f);
            thrusterForce      = DrawSlider("Thruster Force",      thrusterForce,      1f,   20f);
            thrusterRegenRate  = DrawSlider("Thruster Regen",      thrusterRegenRate,  5f,   40f);
            gripDrainRate      = DrawSlider("Grip Drain Rate",     gripDrainRate,      5f,   60f);
            aimConeDegrees     = DrawSlider("Aim Cone (deg)",      aimConeDegrees,     0f,   60f);
            grappleForgivenessRadius = DrawSlider("Grapple Forgiveness", grappleForgivenessRadius, 0f, 3f);
            emergencyWindowDuration  = DrawSlider("Emergency Window",    emergencyWindowDuration,  0.5f, 3f);

            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply to Scene")) ApplyToScene();
            if (GUILayout.Button("Reset Defaults")) ResetDefaults();
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Export Settings")) ExportSettings();

            GUI.DragWindow(new Rect(0f, 0f, windowRect.width, 20f));
        }

        private float DrawSlider(string label, float value, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{label}:", GUILayout.Width(140f));
            float newVal = GUILayout.HorizontalSlider(value, min, max, GUILayout.Width(100f));
            GUILayout.Label($"{newVal:F2}", GUILayout.Width(40f));
            GUILayout.EndHorizontal();
            return newVal;
        }

        private void ApplyToScene()
        {
            Physics.gravity = new Vector3(0f, -9.81f * gravityScale, 0f);

            if (thrusterSystem != null)
            {
                thrusterSystem.SetThrustForce(thrusterForce);
                thrusterSystem.SetRegenRate(thrusterRegenRate);
            }
            if (gripSystem != null)
                gripSystem.SetDrainRate(gripDrainRate);
            if (grappleController != null)
            {
                grappleController.SetMaxRopeLength(maxRopeLength);
                grappleController.SetRetractionSpeed(retractionSpeed);
            }
            if (aimAssist != null)
                aimAssist.SetConeAngle(aimConeDegrees);
            if (emergencyRecovery != null)
                emergencyRecovery.SetWindowDuration(emergencyWindowDuration);

            UnityEngine.Debug.Log("[MovementTuner] Applied settings to scene.");
        }

        private void ResetDefaults()
        {
            gravityScale = 1f; swingDamping = 0.05f; retractionSpeed = 12f;
            maxRopeLength = 50f; thrusterForce = 8f; thrusterRegenRate = 15f;
            gripDrainRate = 25f; aimConeDegrees = 30f;
            grappleForgivenessRadius = 1f; emergencyWindowDuration = 2f;
            Physics.gravity = new Vector3(0f, -9.81f, 0f);
        }

        private void ExportSettings()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("// MovementTuner Export");
            sb.AppendLine($"[SerializeField] private float gravityScale = {gravityScale:F2}f;");
            sb.AppendLine($"[SerializeField] private float swingDamping = {swingDamping:F3}f;");
            sb.AppendLine($"[SerializeField] private float retractionSpeed = {retractionSpeed:F1}f;");
            sb.AppendLine($"[SerializeField] private float maxRopeLength = {maxRopeLength:F1}f;");
            sb.AppendLine($"[SerializeField] private float thrusterForce = {thrusterForce:F1}f;");
            sb.AppendLine($"[SerializeField] private float thrusterRegenRate = {thrusterRegenRate:F1}f;");
            sb.AppendLine($"[SerializeField] private float gripDrainRate = {gripDrainRate:F1}f;");
            sb.AppendLine($"[SerializeField] private float aimConeDegrees = {aimConeDegrees:F1}f;");
            sb.AppendLine($"[SerializeField] private float grappleForgivenessRadius = {grappleForgivenessRadius:F2}f;");
            sb.AppendLine($"[SerializeField] private float emergencyWindowDuration = {emergencyWindowDuration:F2}f;");
            UnityEngine.Debug.Log(sb.ToString());
        }
    }
}
#endif
