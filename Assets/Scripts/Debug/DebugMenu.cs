#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using TitanAscent.Player;
using TitanAscent.Systems;
using TitanAscent.Environment;

namespace TitanAscent.Debug
{
    /// <summary>
    /// In-game developer debug menu. Toggle with backtick key.
    /// Shows player state, teleport, toggles, narration tests, wind control and save reset.
    /// Only compiled in Editor and Development builds.
    /// </summary>
    public class DebugMenu : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerController playerController;
        [SerializeField] private ThrusterSystem thrusterSystem;
        [SerializeField] private GripSystem gripSystem;
        [SerializeField] private FallTracker fallTracker;
        [SerializeField] private NarrationSystem narration;
        [SerializeField] private SaveManager saveManager;
        [SerializeField] private WindSystem windSystem;
        [SerializeField] private GameManager gameManager;

        private bool isVisible = false;
        private bool infiniteThruster = false;
        private bool noGravity = false;
        private bool godMode = false;
        private bool showAnchors = false;
        private Rect windowRect = new Rect(10f, 10f, 330f, 640f);
        private string teleportHeightInput = "0";
        private float windMultiplier = 1f;
        private AnchorValidator anchorValidator;

        private static readonly float[] ZoneHeights = { 0f, 800f, 1800f, 3000f, 4200f, 5500f, 6500f, 7800f, 9000f };
        private static readonly string[] ZoneNames  = { "Z1 Tail", "Z2 Spires", "Z3 Leg", "Z4 Wing", "Z5 Spine", "Z6 Grave", "Z7 Storm", "Z8 Neck", "Z9 Crown" };

        /// <summary>Exposed so other systems (e.g. fall damage handlers) can check god mode.</summary>
        public bool GodMode => godMode;

        private void Awake()
        {
            anchorValidator = FindFirstObjectByType<AnchorValidator>();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.BackQuote))
                isVisible = !isVisible;

            // Infinite thruster: force energy to max every frame while toggle is on
            if (infiniteThruster && thrusterSystem != null)
                thrusterSystem.SetEnergyToMax();

            // No gravity: zero out physics gravity each frame
            if (noGravity)
                Physics.gravity = Vector3.zero;
        }

        private void OnGUI()
        {
            if (!isVisible) return;
            windowRect = GUI.Window(9999, windowRect, DrawWindow, "TITAN ASCENT DEBUG");
        }

        private void DrawWindow(int id)
        {
            GUILayout.Space(4f);

            DrawPlayerState();
            GUILayout.Space(6f);
            DrawTeleport();
            GUILayout.Space(6f);
            DrawToggles();
            GUILayout.Space(6f);
            DrawNarrationTest();
            GUILayout.Space(6f);
            DrawWind();
            GUILayout.Space(6f);
            DrawReset();

            GUI.DragWindow(new Rect(0f, 0f, windowRect.width, 20f));
        }

        // ── PLAYER STATE ────────────────────────────────────────────────────

        private void DrawPlayerState()
        {
            GUILayout.Label("── PLAYER STATE ──");

            if (playerController != null)
            {
                Rigidbody rb = playerController.GetComponent<Rigidbody>();
                GUILayout.Label($"Height:   {playerController.transform.position.y:F1} m");
                if (rb != null)
                    GUILayout.Label($"Velocity: {rb.linearVelocity.magnitude:F1} m/s  (Y: {rb.linearVelocity.y:F1})");
            }

            if (fallTracker != null)
            {
                string fallStr = fallTracker.IsFalling ? $"  ▼ FALLING {fallTracker.CurrentFallDistance:F0}m" : "";
                GUILayout.Label($"Best: {fallTracker.BestHeightEver:F0}m  Falls: {fallTracker.TotalFalls}{fallStr}");
            }

            if (thrusterSystem != null)
                GUILayout.Label($"Energy:   {thrusterSystem.CurrentEnergy:F0} / {thrusterSystem.MaxEnergy:F0}");

            if (gripSystem != null)
                GUILayout.Label($"Grip:     {gripSystem.CurrentGrip:F0} / {gripSystem.MaxGrip:F0}");

            // Current zone
            ZoneManager zm = FindFirstObjectByType<ZoneManager>();
            if (zm != null && zm.CurrentZone != null)
                GUILayout.Label($"Zone:     {zm.CurrentZone.name}");
        }

        // ── TELEPORT ────────────────────────────────────────────────────────

        private void DrawTeleport()
        {
            GUILayout.Label("── TELEPORT ──");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Height (m):", GUILayout.Width(72f));
            teleportHeightInput = GUILayout.TextField(teleportHeightInput, GUILayout.Width(72f));
            if (GUILayout.Button("Go", GUILayout.Width(38f)))
                TeleportToHeight(teleportHeightInput);
            GUILayout.EndHorizontal();

            int cols = 3;
            for (int i = 0; i < ZoneHeights.Length; i++)
            {
                if (i % cols == 0) GUILayout.BeginHorizontal();
                if (GUILayout.Button(ZoneNames[i], GUILayout.Width(100f)))
                    TeleportToHeight(ZoneHeights[i]);
                if (i % cols == cols - 1 || i == ZoneHeights.Length - 1)
                    GUILayout.EndHorizontal();
            }
        }

        // ── TOGGLES ─────────────────────────────────────────────────────────

        private void DrawToggles()
        {
            GUILayout.Label("── TOGGLES ──");

            bool newInfinite = GUILayout.Toggle(infiniteThruster, " Infinite Thruster");
            if (newInfinite != infiniteThruster)
                infiniteThruster = newInfinite;

            bool newNoGrav = GUILayout.Toggle(noGravity, " No Gravity");
            if (newNoGrav != noGravity)
            {
                noGravity = newNoGrav;
                // Restore gravity when turning off
                if (!noGravity) Physics.gravity = new Vector3(0f, -9.81f, 0f);
            }

            bool newGodMode = GUILayout.Toggle(godMode, " God Mode (no fall damage)");
            if (newGodMode != godMode)
                godMode = newGodMode;

            bool newAnchors = GUILayout.Toggle(showAnchors, " Visualize Anchors");
            if (newAnchors != showAnchors)
            {
                showAnchors = newAnchors;
                if (anchorValidator == null)
                {
                    anchorValidator = FindFirstObjectByType<AnchorValidator>();
                    if (anchorValidator == null)
                    {
                        GameObject go = new GameObject("AnchorValidator");
                        anchorValidator = go.AddComponent<AnchorValidator>();
                    }
                }
                anchorValidator.ToggleVisualization();
            }
        }

        // ── NARRATION TEST ───────────────────────────────────────────────────

        private void DrawNarrationTest()
        {
            GUILayout.Label("── NARRATION TEST ──");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Small Fall"))    narration?.TriggerSmallFall();
            if (GUILayout.Button("Large Fall"))    narration?.TriggerLargeFall();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Catastrophic"))  narration?.TriggerCatastrophicFall();
            if (GUILayout.Button("Recovery"))      narration?.TriggerMajorRecovery();
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Victory"))       narration?.TriggerVictory();
        }

        // ── WIND ─────────────────────────────────────────────────────────────

        private void DrawWind()
        {
            GUILayout.Label("── WIND ──");
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Multiplier: {windMultiplier:F2}x", GUILayout.Width(115f));
            float newMult = GUILayout.HorizontalSlider(windMultiplier, 0f, 3f);
            GUILayout.EndHorizontal();

            if (!Mathf.Approximately(newMult, windMultiplier))
            {
                windMultiplier = newMult;
                windSystem?.SetGlobalWindStrength(windMultiplier);
            }
        }

        // ── RESET ────────────────────────────────────────────────────────────

        private void DrawReset()
        {
            GUILayout.Label("── RESET ──");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset Run"))
            {
                TeleportToHeight(0f);
                fallTracker?.ForceEndFall();
            }
            if (GUILayout.Button("Reset All Stats"))
                saveManager?.Reset();
            GUILayout.EndHorizontal();
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private void TeleportToHeight(string input)
        {
            if (float.TryParse(input, out float h))
                TeleportToHeight(h);
        }

        private void TeleportToHeight(float height)
        {
            if (playerController == null) return;
            Rigidbody rb = playerController.GetComponent<Rigidbody>();
            playerController.transform.position = new Vector3(0f, height, 0f);
            if (rb != null) rb.linearVelocity = Vector3.zero;
        }
    }
}
#endif
